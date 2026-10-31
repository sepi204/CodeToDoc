using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class CSharpAstParserTests
{
    private readonly CSharpAstParser _parser = new();

    private static SourceFile Cs(string path, string content) =>
        new() { RelativePath = path, Content = content };

    [Fact]
    public void ExtractsControllerRouteCombinedWithHttpPost()
    {
        var controller = """
            [ApiController]
            [Route("api/[controller]")]
            public class UsersController : ControllerBase
            {
                [HttpPost]
                public ActionResult<UserResponse> CreateUser([FromBody] CreateUserRequest request) => Ok();
            }
            """;

        var endpoints = _parser.Parse(new[] { Cs("UsersController.cs", controller) });

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("POST", endpoint.Method);
        Assert.Equal("/api/users", endpoint.Path);
        Assert.Equal("UsersController", endpoint.ControllerName);
        Assert.Equal("CreateUser", endpoint.ActionName);
    }

    [Fact]
    public void ExtractsAuthorizeOnClass()
    {
        var controller = """
            [ApiController]
            [Route("api/admin")]
            [Authorize(Roles = "Admin")]
            public class AdminController : ControllerBase
            {
                [HttpGet]
                public IActionResult Get() => Ok();
            }
            """;

        var endpoints = _parser.Parse(new[] { Cs("AdminController.cs", controller) });

        var endpoint = Assert.Single(endpoints);
        Assert.True(endpoint.AuthRequired);
        Assert.Contains("Admin", endpoint.Roles);
    }

    [Fact]
    public void ExtractsFromBodyDtoPropertiesWithValidation()
    {
        var controller = """
            [ApiController]
            [Route("api/users")]
            public class UsersController : ControllerBase
            {
                [HttpPost]
                public IActionResult Create([FromBody] CreateUserRequest request) => Ok();
            }
            """;
        var dto = """
            public class CreateUserRequest
            {
                [Required]
                public string Name { get; set; } = "";
                [EmailAddress]
                public string Email { get; set; } = "";
            }
            """;

        var endpoints = _parser.Parse(new[]
        {
            Cs("Controllers/UsersController.cs", controller),
            Cs("Models/CreateUserRequest.cs", dto),
        });

        var endpoint = Assert.Single(endpoints);
        Assert.NotNull(endpoint.RequestDto);
        Assert.Equal("CreateUserRequest", endpoint.RequestDto.TypeName);

        var name = Assert.Single(endpoint.RequestDto.Properties, p => p.Name == "Name");
        Assert.True(name.IsRequired);
        Assert.Contains("required", name.Rules);
        Assert.Equal("string", name.Type);

        var email = Assert.Single(endpoint.RequestDto.Properties, p => p.Name == "Email");
        Assert.Contains("valid email", email.Rules);
    }

    [Fact]
    public void IdentifiesActionResultReturnType()
    {
        var controller = """
            [ApiController]
            [Route("api/users")]
            public class UsersController : ControllerBase
            {
                [HttpPost]
                public ActionResult<UserResponse> Create([FromBody] CreateUserRequest request) => Ok();
            }
            """;
        var response = """
            public class UserResponse
            {
                public Guid Id { get; set; }
            }
            """;

        var endpoints = _parser.Parse(new[]
        {
            Cs("UsersController.cs", controller),
            Cs("UserResponse.cs", response),
        });

        var endpoint = Assert.Single(endpoints);
        Assert.NotNull(endpoint.ResponseDto);
        Assert.Equal("UserResponse", endpoint.ResponseDto.TypeName);

        var id = Assert.Single(endpoint.ResponseDto.Properties);
        Assert.Equal("Id", id.Name);
        Assert.Equal("guid", id.Type);
    }

    [Fact]
    public void SyntaxErrorDoesNotThrow()
    {
        var controller = """
            [ApiController]
            [Route("api/broken")]
            public class BrokenController : ControllerBase
            {
                [HttpGet]
                public IActionResult Get( => Ok();
            }
            """;

        var endpoints = _parser.Parse(new[] { Cs("BrokenController.cs", controller) });

        // Roslyn is lenient: it still yields a syntax tree without throwing.
        Assert.NotNull(endpoints);
    }

    [Fact]
    public void ExtractsEndpointsFromPrimaryConstructorController()
    {
        // C# 12 primary constructors (Roslyn >= 4.8). Regression: Roslyn 4.5
        // failed to parse these, silently yielding zero endpoints.
        var controller = """
            [ApiController]
            [Route("api/orders")]
            [Authorize]
            public sealed class OrdersController(ISender sender) : ControllerBase
            {
                [HttpPost("drafts")]
                [Authorize(Policy = "OrdersCreate")]
                public async Task<ActionResult<OrderDto>> CreateDraft(CreateDraftOrderCommand command, CancellationToken ct) =>
                    Ok(await sender.Send(command, ct));
            }
            """;

        var endpoints = _parser.Parse(new[] { Cs("OrdersController.cs", controller) });

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("POST", endpoint.Method);
        Assert.Equal("/api/orders/drafts", endpoint.Path);
        Assert.Equal("OrdersController", endpoint.ControllerName);
        Assert.Equal("CreateDraft", endpoint.ActionName);
        Assert.True(endpoint.AuthRequired);
    }
}
