using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeToDoc.Tests;

public class PhaseTwoIntegrationTests : IDisposable
{
    private readonly string _tempRoot = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_tempRoot);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private WebApplicationFactory<global::Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["PhaseOneProcessingOptions:TempRootPath"] = _tempRoot,
        };

        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            });
    }

    private static MultipartFormDataContent CreateMultipart(params (string FileName, byte[] Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (fileName, content) in files)
        {
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "archives", fileName);
        }

        return form;
    }

    private async Task<TechnicalSkeleton> PostTechnicalAsync(
        HttpClient client,
        params (string FileName, byte[] Content)[] files)
    {
        using var response = await client.PostAsync(
            "/api/projects/process-technical",
            CreateMultipart(files));

        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TechnicalSkeleton>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;
    }

    // ---- Scenario 1: .NET Web API end-to-end ----

    [Fact]
    public async Task TestScenario_DotNet_E2E()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(
            ("MyApi.csproj", """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
                  </ItemGroup>
                </Project>
                """),
            ("Controllers/UsersController.cs", """
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/users")]
                [Authorize]
                public class UsersController : ControllerBase
                {
                    [HttpPost]
                    public ActionResult<UserResponse> CreateUser([FromBody] CreateUserRequest request)
                    {
                        var user = new UserResponse { Id = System.Guid.NewGuid() };
                        return CreatedAtAction(nameof(CreateUser), new { id = user.Id }, user);
                    }
                }
                """),
            ("Models/CreateUserRequest.cs", """
                using System.ComponentModel.DataAnnotations;

                public class CreateUserRequest
                {
                    [Required]
                    public string Name { get; set; } = "";

                    [EmailAddress]
                    public string Email { get; set; } = "";
                }
                """),
            ("Models/UserResponse.cs", """
                public class UserResponse
                {
                    public System.Guid Id { get; set; }
                }
                """),
            ("bin/Debug/net10.0/MyApi.dll", "garbage-that-must-be-filtered"),
            ("node_modules/dummy/index.js", "garbage-that-must-be-filtered"));

        var skeleton = await PostTechnicalAsync(client, ("MyApi.zip", zip));

        // Phase 2 architecture inference
        Assert.Equal("ASP.NET Core", skeleton.Summary.Framework);
        Assert.Equal("PostgreSQL", skeleton.Summary.Database);
        Assert.Equal("Entity Framework Core", skeleton.Summary.Orm);
        Assert.Equal("JWT Bearer", skeleton.Summary.Auth);

        // Endpoint extraction
        var endpoint = Assert.Single(skeleton.Endpoints, e => e.Path == "/api/users" && e.Method == "POST");
        Assert.True(endpoint.AuthRequired);
        Assert.Equal("UsersController", endpoint.ControllerName);
        Assert.Equal("CreateUser", endpoint.ActionName);

        // Request DTO
        Assert.NotNull(endpoint.RequestDto);
        var name = Assert.Single(endpoint.RequestDto.Properties, p => p.Name == "Name");
        Assert.True(name.IsRequired);
        Assert.Contains("required", name.Rules);

        var email = Assert.Single(endpoint.RequestDto.Properties, p => p.Name == "Email");
        Assert.Contains("valid email", email.Rules);

        // technical_skeleton.json physically written inside the job workspace
        var workspaceDir = Path.Combine(_tempRoot, skeleton.JobId);
        var skeletonPath = Path.Combine(workspaceDir, "technical_skeleton.json");
        Assert.True(File.Exists(skeletonPath));

        var fileJson = await File.ReadAllTextAsync(skeletonPath);
        var deserialized = JsonSerializer.Deserialize<TechnicalSkeleton>(fileJson, JsonOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(skeleton.JobId, deserialized.JobId);
    }

    // ---- Scenario 2: malicious ZIP (Zip Slip) must abort before Phase 2 ----

    [Fact]
    public async Task TestScenario_MaliciousZip_E2E()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(
            ("Controllers/UsersController.cs", """
                [ApiController]
                [Route("api/users")]
                public class UsersController : Microsoft.AspNetCore.Mvc.ControllerBase
                {
                    [Microsoft.AspNetCore.Mvc.HttpPost]
                    public Microsoft.AspNetCore.Mvc.IActionResult Create() => Ok();
                }
                """),
            ("../evil.txt", "should-never-be-written"));

        using var response = await client.PostAsync(
            "/api/projects/process-technical",
            CreateMultipart(("malicious.zip", zip)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Phase 1 aborts on Zip Slip; the entire workspace is cleaned up.
        Assert.False(File.Exists(Path.Combine(_tempRoot, "evil.txt")));
        Assert.Empty(Directory.GetDirectories(_tempRoot));
    }

    // ---- Scenario 3: multi-repo (backend .NET + frontend Next.js) ----

    [Fact]
    public async Task TestScenario_MultiRepo_Frontend_Backend_E2E()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var backendZip = TestHelpers.CreateZip(
            ("MyApi.csproj", """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
                  </ItemGroup>
                </Project>
                """),
            ("Controllers/OrdersController.cs", """
                using Microsoft.AspNetCore.Mvc;

                [ApiController]
                [Route("api/orders")]
                public class OrdersController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult List() => Ok();
                }
                """));

        var frontendZip = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
            ("src/app/page.tsx", "export default function Page() { return <div />; }"));

        var skeleton = await PostTechnicalAsync(
            client,
            ("backend.zip", backendZip),
            ("frontend.zip", frontendZip));

        // Backend endpoint extracted
        Assert.Contains(skeleton.Endpoints, e => e.Path == "/api/orders" && e.Method == "GET");

        // Frontend dependencies captured
        Assert.Contains(skeleton.Dependencies, d => d.Name == "next");
        Assert.Contains(skeleton.Dependencies, d => d.Name == "react");

        // Both archives extracted into separate source folders.
        var workspaceDir = Path.Combine(_tempRoot, skeleton.JobId);
        Assert.True(Directory.Exists(Path.Combine(workspaceDir, "source", "backend")));
        Assert.True(Directory.Exists(Path.Combine(workspaceDir, "source", "frontend")));
    }
}
