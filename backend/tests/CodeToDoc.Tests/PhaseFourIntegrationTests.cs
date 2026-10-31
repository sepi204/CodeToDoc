using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeToDoc.Tests;

public class PhaseFourIntegrationTests : IDisposable
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

    private async Task<(HttpStatusCode Status, CorrelationPipelineResult? Result)> PostCorrelationAsync(
        HttpClient client, params (string FileName, byte[] Content)[] files)
    {
        using var response = await client.PostAsync("/api/projects/process-correlation", CreateMultipart(files));
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        var result = JsonSerializer.Deserialize<CorrelationPipelineResult>(body, JsonOptions);
        return (response.StatusCode, result);
    }

    private static byte[] InvoiceBackendZip() => TestHelpers.CreateZip(
        ("MyApi.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
                <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """),
        ("Controllers/InvoicesController.cs", """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/invoices")]
            [Authorize(Roles = "Admin")]
            public class InvoicesController : ControllerBase
            {
                [HttpPost]
                public ActionResult<InvoiceResponse> CreateInvoice([FromBody] CreateInvoiceRequest request)
                {
                    _db.Invoices.Add(new Invoice { Id = System.Guid.NewGuid() });
                    _db.SaveChangesAsync();
                    return Ok(new InvoiceResponse());
                }
            }
            """),
        ("Models/CreateInvoiceRequest.cs", """
            using System.ComponentModel.DataAnnotations;
            public class CreateInvoiceRequest
            {
                [Required] public string CustomerId { get; set; } = "";
                [Required] public decimal TotalAmount { get; set; }
            }
            """),
        ("Models/InvoiceResponse.cs", "public class InvoiceResponse { public System.Guid Id { get; set; } }"));

    private static byte[] InvoiceFrontendZip() => TestHelpers.CreateZip(
        ("package.json", """{"dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
        ("src/app/invoices/new/page.tsx", """
            import InvoiceForm from './InvoiceForm';
            export default function Page() { return <div><h1>فاکتور جدید</h1><InvoiceForm /></div>; }
            """),
        ("src/app/invoices/new/InvoiceForm.tsx", """
            export default function InvoiceForm() {
              const handleSubmit = async () => {
                await fetch("/api/invoices", { method: "POST" });
              };
              return (
                <form onSubmit={handleSubmit}>
                  <label htmlFor="customerId">شناسه مشتری</label>
                  <input id="customerId" name="customerId" required />
                  <label htmlFor="totalAmount">مبلغ کل</label>
                  <input id="totalAmount" name="totalAmount" required />
                  <button type="submit">صدور فاکتور</button>
                </form>
              );
            }
            """),
        ("node_modules/dummy/index.js", "garbage"),
        (".next/cache/page.js", "garbage"));

    // ---- A. Full stack correlation end-to-end ----

    [Fact]
    public async Task FullStack_Correlation_EndToEnd()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (status, result) = await PostCorrelationAsync(
            client, ("backend.zip", InvoiceBackendZip()), ("frontend.zip", InvoiceFrontendZip()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);

        // Phase 2 produced the endpoint.
        Assert.Contains(result.TechnicalSkeleton.Endpoints, e => e.Path == "/api/invoices" && e.Method == "POST");

        // Phase 3 produced the page and action.
        var page = Assert.Single(result.UiSkeleton.Pages, p => p.Route == "/invoices/new");
        var action = Assert.Single(page.Actions, a => a.Label == "صدور فاکتور");

        // Phase 4 correlated them.
        Assert.Equal(CorrelationStatus.Complete, result.CorrelationSkeleton.Status);
        var correlation = Assert.Single(result.CorrelationSkeleton.Correlations, c => c.ActionId == action.Id);
        Assert.Equal("/api/invoices", correlation.Backend.PathTemplate);
        Assert.True(correlation.Confidence >= 0.7, $"confidence {correlation.Confidence} should be >= 0.7");
        Assert.True(correlation.Backend.AuthRequired);
        Assert.Contains("Admin", correlation.Backend.Roles);

        Assert.Contains(correlation.FieldMapping.Mappings, m => m.UiFieldName == "customerId" && m.DtoProperty == "CustomerId");
        Assert.Contains(correlation.FieldMapping.Mappings, m => m.UiFieldName == "totalAmount" && m.DtoProperty == "TotalAmount");

        Assert.Equal(PersistenceKind.Evidence, correlation.Persistence.Kind);
        Assert.Equal("create", correlation.Persistence.Operation);

        // Artifact exists in the workspace.
        Assert.True(File.Exists(Path.Combine(_tempRoot, result.JobId, "correlation_skeleton.json")));
    }

    // ---- B. Ambiguous match ----

    [Fact]
    public async Task AmbiguousMatch_EndToEnd()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var backend = TestHelpers.CreateZip(
            ("MyApi.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>"),
            ("Controllers/InvoicesController.cs", """
                using Microsoft.AspNetCore.Mvc;
                [ApiController] [Route("api/invoices")]
                public class InvoicesController : ControllerBase
                {
                    [HttpPost] public IActionResult Create() => Ok();
                }
                """),
            ("Controllers/LegacyInvoicesController.cs", """
                using Microsoft.AspNetCore.Mvc;
                [ApiController] [Route("api/invoices")]
                public class LegacyInvoicesController : ControllerBase
                {
                    [HttpPost] public IActionResult CreateLegacy() => Ok();
                }
                """));

        var frontend = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0"}}"""),
            ("src/app/invoices/new/page.tsx", """
                export default function Page() {
                  const handleSubmit = () => fetch("/api/invoices", { method: "POST" });
                  return <form onSubmit={handleSubmit}><button type="submit">ثبت</button></form>;
                }
                """));

        var (status, result) = await PostCorrelationAsync(client, ("backend.zip", backend), ("frontend.zip", frontend));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);

        Assert.True(result.CorrelationSkeleton.Correlations.Count >= 2 ||
            result.CorrelationSkeleton.Diagnostics.Any(d => d.Code == "corr.ambiguous"));
    }

    // ---- C. Backend only -> notApplicable ----

    [Fact]
    public async Task BackendOnly_NotApplicableCorrelation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (status, result) = await PostCorrelationAsync(client, ("backend.zip", InvoiceBackendZip()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);
        Assert.Equal(UiStatus.NotApplicable, result.UiSkeleton.Status);
        Assert.Equal(CorrelationStatus.NotApplicable, result.CorrelationSkeleton.Status);
        Assert.Empty(result.CorrelationSkeleton.Correlations);
    }

    // ---- D. Frontend only -> notApplicable ----

    [Fact]
    public async Task FrontendOnly_NotApplicableCorrelation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (status, result) = await PostCorrelationAsync(client, ("frontend.zip", InvoiceFrontendZip()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);
        Assert.Empty(result.TechnicalSkeleton.Endpoints);
        Assert.Equal(CorrelationStatus.NotApplicable, result.CorrelationSkeleton.Status);
        Assert.Empty(result.CorrelationSkeleton.Correlations);
    }

    // ---- E. Malicious zip stops all ----

    [Fact]
    public async Task MaliciousZip_StopsAll()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var malicious = TestHelpers.CreateZip(
            ("Controllers/XController.cs", "public class X {}"),
            ("../evil.txt", "evil"));

        var (status, result) = await PostCorrelationAsync(client, ("malicious.zip", malicious));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Null(result);
        Assert.False(File.Exists(Path.Combine(_tempRoot, "evil.txt")));
        Assert.Empty(Directory.GetDirectories(_tempRoot));
    }
}
