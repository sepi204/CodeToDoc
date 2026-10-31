using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeToDoc.Tests;

public class PhaseSixIntegrationTests : IDisposable
{
    private readonly string _tempRoot = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_tempRoot);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private WebApplicationFactory<global::Program> CreateFactory(Action<IServiceCollection>? configureServices = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["PhaseOneProcessingOptions:TempRootPath"] = _tempRoot,
            ["ArtifactStorage:RootPath"] = _tempRoot,
            ["LlmOptions:Provider"] = "Fake",
            ["LlmOptions:Model"] = "fake-model",
            ["LlmOptions:MaxRetries"] = "1",
        };

        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
                if (configureServices is not null)
                {
                    builder.ConfigureServices(configureServices);
                }
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

    private async Task<(JobStatusResponse Status, HttpClient Client)> RunJobAsync(
        WebApplicationFactory<global::Program> factory, params (string FileName, byte[] Content)[] files)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/projects/jobs", CreateMultipart(files));
        response.EnsureSuccessStatusCode();
        var start = JsonSerializer.Deserialize<JobStartResponse>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        var status = await WaitForCompletionAsync(client, start.JobId, TimeSpan.FromSeconds(60));
        return (status, client);
    }

    private static async Task<JobStatusResponse> WaitForCompletionAsync(HttpClient client, string jobId, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var response = await client.GetAsync($"/api/projects/jobs/{jobId}");
            response.EnsureSuccessStatusCode();
            var status = JsonSerializer.Deserialize<JobStatusResponse>(
                await response.Content.ReadAsStringAsync(), JsonOptions)!;

            if (status.Status is JobStatus.Completed or JobStatus.CompletedWithWarnings or JobStatus.Failed or JobStatus.Cancelled)
            {
                return status;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Job {jobId} did not reach a terminal state within {timeout}.");
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
        ("Models/InvoiceResponse.cs", """
            public class InvoiceResponse
            {
                public System.Guid Id { get; set; }
                public string CustomerId { get; set; } = "";
                public decimal TotalAmount { get; set; }
            }
            """),
        ("bin/Debug/app.dll", "garbage"));

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
        ("src/app/invoices/page.tsx", """
            import Link from 'next/link';
            export default function Page() { return <div><h1>فاکتورها</h1><Link href="/invoices/new">فاکتور جدید</Link></div>; }
            """),
        ("node_modules/dummy/index.js", "garbage"));

    // ---- A. Full pipeline Phase 1-6 end-to-end ----

    [Fact]
    public async Task FullPipeline_Phase1To6_EndToEnd()
    {
        using var factory = CreateFactory();
        var (status, client) = await RunJobAsync(factory, ("backend.zip", InvoiceBackendZip()), ("frontend.zip", InvoiceFrontendZip()));

        Assert.Equal(JobStatus.Completed, status.Status);

        // All six phases recorded, in order.
        Assert.Equal(new[] { "phase1", "phase2", "phase3", "phase4", "phase5", "phase6" }, status.Phases.Select(p => p.Phase).ToArray());

        // Final artifacts present.
        var artifacts = status.Artifacts;
        Assert.Contains(artifacts, a => a.FileName == "technical_document.md" && a.Status == ArtifactStatus.Available);
        Assert.Contains(artifacts, a => a.FileName == "technical_document.pdf" && a.Status == ArtifactStatus.Available);
        Assert.Contains(artifacts, a => a.FileName == "technical_document.docx" && a.Status == ArtifactStatus.Available);
        Assert.Contains(artifacts, a => a.FileName == "user_guide.md" && a.Status == ArtifactStatus.Available);
        Assert.Contains(artifacts, a => a.FileName == "user_guide.pdf" && a.Status == ArtifactStatus.Available);
        Assert.Contains(artifacts, a => a.FileName == "user_guide.docx" && a.Status == ArtifactStatus.Available);

        // Download the technical PDF and verify it is a valid PDF and checksum matches.
        var pdfEntry = artifacts.Single(a => a.FileName == "technical_document.pdf");
        var downloaded = await client.GetAsync($"/api/projects/jobs/{status.JobId}/artifacts/technical_document.pdf");
        downloaded.EnsureSuccessStatusCode();
        var pdfBytes = await downloaded.Content.ReadAsByteArrayAsync();

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));
        Assert.Equal(pdfEntry.SizeBytes, pdfBytes.LongLength);
        Assert.Equal(pdfEntry.Checksum, Sha256(pdfBytes));

        // DOCX downloads and is a valid package.
        var docx = await client.GetAsync($"/api/projects/jobs/{status.JobId}/artifacts/technical_document.docx");
        docx.EnsureSuccessStatusCode();
        var docxBytes = await docx.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK", Encoding.ASCII.GetString(docxBytes, 0, 2));

        // No absolute path leaks in any status or artifact payload.
        var statusJson = await (await client.GetAsync($"/api/projects/jobs/{status.JobId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(_tempRoot, statusJson);

        // The technical skeleton and diagram endpoints are reachable.
        var technical = await client.GetAsync($"/api/projects/jobs/{status.JobId}/technical");
        technical.EnsureSuccessStatusCode();
        var diagram = await client.GetAsync($"/api/projects/jobs/{status.JobId}/diagram");
        diagram.EnsureSuccessStatusCode();
    }

    // ---- B. Polling / progress ----

    [Fact]
    public async Task PollingOrProgress_FullJob()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var startResponse = await client.PostAsync("/api/projects/jobs", CreateMultipart(("backend.zip", InvoiceBackendZip()), ("frontend.zip", InvoiceFrontendZip())));
        startResponse.EnsureSuccessStatusCode();
        var start = JsonSerializer.Deserialize<JobStartResponse>(await startResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.Equal("queued", start.Status);

        var terminal = await WaitForCompletionAsync(client, start.JobId, TimeSpan.FromSeconds(60));

        Assert.True(terminal.Status is JobStatus.Completed or JobStatus.CompletedWithWarnings);
        Assert.Equal(JobPhase.Completed, terminal.Phase);
        Assert.Equal(6, terminal.Phases.Count);
        Assert.NotNull(terminal.CompletedAtUtc);
    }

    // ---- C. Malicious zip stops all phases ----

    [Fact]
    public async Task MaliciousZip_StopsAllPhases()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var fake = factory.Services.GetRequiredService<FakeLlmClient>();
        var before = fake.InvocationCount;

        var malicious = TestHelpers.CreateZip(
            ("Controllers/XController.cs", "public class X {}"),
            ("../evil.txt", "evil"));

        using var response = await client.PostAsync("/api/projects/jobs", CreateMultipart(("malicious.zip", malicious)));
        response.EnsureSuccessStatusCode();
        var start = JsonSerializer.Deserialize<JobStartResponse>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        var terminal = await WaitForCompletionAsync(client, start.JobId, TimeSpan.FromSeconds(60));

        Assert.Equal(JobStatus.Failed, terminal.Status);
        Assert.Equal(before, fake.InvocationCount); // no additional LLM calls
        Assert.False(File.Exists(Path.Combine(_tempRoot, "evil.txt")));
        Assert.Empty(terminal.Artifacts);
    }

    // ---- D. Renderer failure preserves other formats ----

    [Fact]
    public async Task RendererFailure_PartialArtifacts()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IDocumentRenderer>();
            services.AddSingleton<IDocumentRenderer>(new CodeToDoc.Service.Services.Documents.PdfDocumentRenderer(new CodeToDoc.Service.Services.Documents.MarkdownParser()));
            services.AddSingleton<IDocumentRenderer>(new ThrowingRenderer("docx"));
        });

        var (status, _) = await RunJobAsync(factory, ("backend.zip", InvoiceBackendZip()), ("frontend.zip", InvoiceFrontendZip()));

        Assert.Equal(JobStatus.CompletedWithWarnings, status.Status);

        Assert.Contains(status.Artifacts, a => a.FileName == "technical_document.pdf" && a.Status == ArtifactStatus.Available);
        Assert.Contains(status.Artifacts, a => a.FileName == "technical_document.md" && a.Status == ArtifactStatus.Available);
        Assert.Contains(status.Artifacts, a => a.FileName == "technical_document.docx" && a.Status == ArtifactStatus.Failed);
    }

    // ---- E. Cancellation during rendering ----

    [Fact]
    public async Task Cancellation_DuringRendering()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IDocumentRenderer>();
            services.AddSingleton<IDocumentRenderer>(new SlowRenderer("pdf"));
            services.AddSingleton<IDocumentRenderer>(new CodeToDoc.Service.Services.Documents.DocxDocumentRenderer(new CodeToDoc.Service.Services.Documents.MarkdownParser()));
        });

        var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/projects/jobs", CreateMultipart(("backend.zip", InvoiceBackendZip()), ("frontend.zip", InvoiceFrontendZip())));
        response.EnsureSuccessStatusCode();
        var start = JsonSerializer.Deserialize<JobStartResponse>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        // Wait until the job is running/rendering, then cancel.
        await Task.Delay(400);
        var cancel = await client.PostAsync($"/api/projects/jobs/{start.JobId}/cancel", null);
        Assert.True(cancel.IsSuccessStatusCode);

        var terminal = await WaitForCompletionAsync(client, start.JobId, TimeSpan.FromSeconds(60));

        Assert.Equal(JobStatus.Cancelled, terminal.Status);

        // No truncated PDF artifact was published.
        var pdfEntry = terminal.Artifacts.FirstOrDefault(a => a.FileName == "technical_document.pdf");
        Assert.True(pdfEntry is null || pdfEntry.Status == ArtifactStatus.Failed);
        Assert.False(File.Exists(Path.Combine(_tempRoot, start.JobId, "technical_document.pdf.tmp")));
    }

    // ---- F. Backend-only and frontend-only ----

    [Fact]
    public async Task BackendOnly_TechnicalDocumentOnly()
    {
        using var factory = CreateFactory();
        var (status, client) = await RunJobAsync(factory, ("backend.zip", InvoiceBackendZip()));

        Assert.Contains(status.Artifacts, a => a.FileName == "technical_document.pdf" && a.Status == ArtifactStatus.Available);
        Assert.Contains(status.Artifacts, a => a.FileName == "user_guide.pdf" && a.Status == ArtifactStatus.NotApplicable);

        var userGuide = await client.GetAsync($"/api/projects/jobs/{status.JobId}/documents/userGuide");
        userGuide.EnsureSuccessStatusCode();
        var preview = JsonSerializer.Deserialize<DocumentPreviewResponse>(await userGuide.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal("notApplicable", preview.Status);
        Assert.Empty(preview.Content);
    }

    [Fact]
    public async Task FrontendOnly_LimitedUserGuide()
    {
        using var factory = CreateFactory();
        var (status, client) = await RunJobAsync(factory, ("frontend.zip", InvoiceFrontendZip()));

        Assert.Contains(status.Artifacts, a => a.FileName == "user_guide.pdf" && a.Status == ArtifactStatus.Available);
        Assert.Contains(status.Artifacts, a => a.FileName == "technical_document.pdf" && a.Status == ArtifactStatus.NotApplicable);

        var preview = await client.GetAsync($"/api/projects/jobs/{status.JobId}/documents/userGuide");
        preview.EnsureSuccessStatusCode();
        var userGuidePreview = JsonSerializer.Deserialize<DocumentPreviewResponse>(await preview.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal("available", userGuidePreview.Status);
        Assert.Contains("شناسه مشتری", userGuidePreview.Content);
    }

    private static string Sha256(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class ThrowingRenderer : IDocumentRenderer
    {
        public ThrowingRenderer(string format) => Format = format;

        public string Format { get; }

        public byte[] Render(DocumentRenderInput input, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated render failure.");
    }

    private sealed class SlowRenderer : IDocumentRenderer
    {
        public SlowRenderer(string format) => Format = format;

        public string Format { get; }

        public byte[] Render(DocumentRenderInput input, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 200; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(20);
            }

            return new byte[] { 1, 2, 3 };
        }
    }
}
