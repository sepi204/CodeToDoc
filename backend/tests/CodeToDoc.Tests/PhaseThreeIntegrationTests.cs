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

public class PhaseThreeIntegrationTests : IDisposable
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

    private async Task<(HttpStatusCode Status, FullPipelineResult? Result)> PostProcessAsync(
        HttpClient client, params (string FileName, byte[] Content)[] files)
    {
        using var response = await client.PostAsync("/api/projects/process", CreateMultipart(files));
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        var result = JsonSerializer.Deserialize<FullPipelineResult>(body, JsonOptions);
        return (response.StatusCode, result);
    }

    private static byte[] FrontendZip() => TestHelpers.CreateZip(
        ("package.json", """{"name":"frontend","dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
        ("src/app/layout.tsx", "export default function Layout({children}) { return <div>{children}</div>; }"),
        ("src/app/customers/new/page.tsx", """
            import CustomerForm from './CustomerForm';
            export default function NewCustomerPage() {
              return (
                <div>
                  <h1>مشتری جدید</h1>
                  <CustomerForm />
                </div>
              );
            }
            """),
        ("src/app/customers/new/CustomerForm.tsx", """
            export default function CustomerForm() {
              const handleSubmit = async () => {
                await fetch("/api/customers", { method: "POST" });
              };
              return (
                <form onSubmit={handleSubmit}>
                  <label htmlFor="lastName">نام خانوادگی</label>
                  <input id="lastName" name="lastName" required />
                  <button type="submit">ثبت نهایی</button>
                </form>
              );
            }
            """),
        ("src/app/customers/page.tsx", """
            import Link from 'next/link';
            export default function CustomersPage() {
              return (
                <div>
                  <h1>مشتریان</h1>
                  <Link href="/customers/new">مشتری جدید</Link>
                  <table>
                    <thead><tr><th>شناسه</th><th>نام</th><th>وضعیت</th></tr></thead>
                  </table>
                </div>
              );
            }
            """),
        ("node_modules/dummy/index.js", "garbage"),
        (".next/cache/page.js", "garbage"),
        ("bin/Debug/app.dll", "garbage"),
        ("obj/Debug/app.dll", "garbage"));

    private static byte[] BackendZip() => TestHelpers.CreateZip(
        ("MyApi.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
                <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """),
        ("Controllers/CustomersController.cs", """
            using Microsoft.AspNetCore.Mvc;
            [ApiController]
            [Route("api/customers")]
            public class CustomersController : ControllerBase
            {
                [HttpPost]
                public ActionResult<CustomerResponse> Create([FromBody] CreateCustomerRequest request) => Ok(new CustomerResponse());
            }
            """),
        ("Models/CreateCustomerRequest.cs", """
            using System.ComponentModel.DataAnnotations;
            public class CreateCustomerRequest
            {
                [Required] public string Name { get; set; } = "";
                [EmailAddress] public string Email { get; set; } = "";
            }
            """),
        ("Models/CustomerResponse.cs", "public class CustomerResponse { public System.Guid Id { get; set; } }"));

    // ---- A. Full stack multi-archive end-to-end ----

    [Fact]
    public async Task FullStack_MultiArchive_EndToEnd()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (status, result) = await PostProcessAsync(client, ("backend.zip", BackendZip()), ("frontend.zip", FrontendZip()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);

        // Combined behavior
        Assert.Equal(result.JobId, result.TechnicalSkeleton.JobId);
        Assert.Equal(result.JobId, result.UiSkeleton.JobId);

        // Phase 2
        Assert.Contains(result.TechnicalSkeleton.Endpoints, e => e.Path == "/api/customers" && e.Method == "POST");
        Assert.Equal("PostgreSQL", result.TechnicalSkeleton.Summary.Database);
        Assert.Contains(result.TechnicalSkeleton.Dependencies, d => d.Name == "next");

        // Phase 3
        Assert.Equal(UiStatus.Complete, result.UiSkeleton.Status);
        var newPage = Assert.Single(result.UiSkeleton.Pages, p => p.Route == "/customers/new");
        var customersPage = Assert.Single(result.UiSkeleton.Pages, p => p.Route == "/customers");

        var field = Assert.Single(newPage.Forms.SelectMany(f => f.Fields), f => f.Name == "lastName");
        Assert.Equal("نام خانوادگی", field.Label);
        Assert.True(field.Required);

        var submit = Assert.Single(newPage.Actions, a => a.Label == "ثبت نهایی");
        Assert.Equal(ActionKind.SubmitForm, submit.Kind);
        Assert.Equal("handleSubmit", submit.HandlerExpression);
        Assert.Contains(submit.ApiReferences, r => r.Method == "POST" && r.Url == "/api/customers");

        var table = Assert.Single(customersPage.Tables);
        Assert.Equal(new[] { "شناسه", "نام", "وضعیت" }, table.Columns.Select(c => c.Label));

        Assert.Contains(customersPage.Actions, a => a.Label == "مشتری جدید" && a.NavigationTarget == "/customers/new");

        // Evidence points to the frontend repository.
        Assert.Contains(result.UiSkeleton.Evidence, e =>
            e.RepositoryId == "frontend" && e.FilePath.Contains("CustomerForm.tsx"));
        Assert.DoesNotContain(result.UiSkeleton.Evidence, e => e.FilePath.Contains("node_modules"));

        // Artifacts exist in the same job workspace.
        var workspaceDir = Path.Combine(_tempRoot, result.JobId);
        Assert.True(File.Exists(Path.Combine(workspaceDir, "technical_skeleton.json")));
        Assert.True(File.Exists(Path.Combine(workspaceDir, "ui_skeleton.json")));
        Assert.True(Directory.Exists(Path.Combine(workspaceDir, "source", "backend")));
        Assert.True(Directory.Exists(Path.Combine(workspaceDir, "source", "frontend")));
    }

    // ---- B. Malicious archive stops all downstream phases ----

    [Fact]
    public async Task MaliciousArchive_StopsAllDownstreamPhases()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var malicious = TestHelpers.CreateZip(
            ("Controllers/UsersController.cs", "public class C {}"),
            ("../evil.txt", "evil"));

        var (status, result) = await PostProcessAsync(client, ("malicious.zip", malicious));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Null(result);
        Assert.False(File.Exists(Path.Combine(_tempRoot, "evil.txt")));
        Assert.Empty(Directory.GetDirectories(_tempRoot));
    }

    // ---- C. Partial frontend parse preserves valid results ----

    [Fact]
    public async Task PartialFrontendParse_PreservesValidResults()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var frontend = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
            ("src/app/valid/page.tsx", "<h1>Valid</h1>"),
            ("src/app/broken/page.tsx", "<div><h1>Unclosed")); // malformed (unbalanced div)

        var (status, result) = await PostProcessAsync(client, ("backend.zip", BackendZip()), ("frontend.zip", frontend));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);

        // Phase 2 remains valid
        Assert.Contains(result.TechnicalSkeleton.Endpoints, e => e.Path == "/api/customers");

        // Phase 3 retains the valid page, status partial, diagnostic for malformed file.
        Assert.Equal(UiStatus.Partial, result.UiSkeleton.Status);
        Assert.Contains(result.UiSkeleton.Pages, p => p.Route == "/valid");
        Assert.Contains(result.UiSkeleton.Diagnostics, d =>
            d.Code == "ui.parse.malformed" && d.SourceReference?.FilePath?.Contains("broken") == true);
    }

    // ---- D. Backend-only project -> notApplicable ----

    [Fact]
    public async Task BackendOnly_NotApplicableUi()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (status, result) = await PostProcessAsync(client, ("backend.zip", BackendZip()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);
        Assert.Equal(UiStatus.NotApplicable, result.UiSkeleton.Status);
        Assert.Empty(result.UiSkeleton.Pages);
    }

    // ---- E. Repeated names across repositories ----

    [Fact]
    public async Task RepeatedNamesAcrossRepositories()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var frontendA = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0"}}"""),
            ("src/app/page.tsx", "<h1>Repo A</h1>"),
            ("src/app/Shared.tsx", "<form><input name=\"aField\" /></form>"));

        var frontendB = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0"}}"""),
            ("src/app/page.tsx", "<h1>Repo B</h1>"));

        var (status, result) = await PostProcessAsync(client, ("a.zip", frontendA), ("b.zip", frontendB));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);

        // Repository identities remain distinct.
        var repoIds = result.UiSkeleton.Repositories.Select(r => r.Id).ToList();
        Assert.Equal(2, repoIds.Count);
        Assert.Contains("a", repoIds);
        Assert.Contains("b", repoIds);
    }
}
