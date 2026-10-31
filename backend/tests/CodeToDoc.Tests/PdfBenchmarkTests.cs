using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services.Documents;
using Xunit.Abstractions;

namespace CodeToDoc.Tests;

/// <summary>
/// Reproducible benchmark for the PDF renderer using a fixed representative
/// fixture. Reports measured results; does not assert a strict 2-second target
/// (that is environment-dependent).
/// </summary>
[Trait("Category", "Performance")]
public class PdfBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PdfBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string Fixture = """
        # Technical Specification

        ## 1. System Overview

        This module provides an API for managing invoices. It uses ASP.NET Core
        and Entity Framework Core with a PostgreSQL database.

        ## 2. Technology Stack

        - ASP.NET Core
        - Entity Framework Core
        - PostgreSQL
        - JWT Bearer authentication

        ## 3. API Reference

        ### POST /api/invoices

        - Controller: `InvoicesController`
        - Authentication: required (Admin)

        | Field | Type | Required |
        |-------|------|----------|
        | CustomerId | string | yes |
        | TotalAmount | decimal | yes |

        ```csharp
        public ActionResult<InvoiceResponse> Create(CreateInvoiceRequest request) => Ok();
        ```

        ## 4. Analysis Limitations

        - Runtime versions were not detected.
        """;

    [Fact]
    public void PdfRender_Benchmark_ReportsDuration()
    {
        var renderer = new PdfDocumentRenderer(new MarkdownParser());
        var input = new DocumentRenderInput
        {
            DocumentType = "technical",
            Title = "Technical Specification",
            Language = "en",
            Markdown = Fixture,
        };

        // Warm-up run (JIT + font discovery).
        _ = renderer.Render(input);

        var sw = Stopwatch.StartNew();
        var bytes = renderer.Render(input);
        sw.Stop();

        var pageCount = Regex.Matches(Encoding.Latin1.GetString(bytes), @"/Type\s*/Page\b").Count;

        _output.WriteLine($"PDF benchmark: fixture={Encoding.UTF8.GetByteCount(Fixture)} bytes, " +
            $"output={bytes.Length} bytes, pages={pageCount}, renderer=QuestPDF 2026.9.1, " +
            $"elapsedMs={sw.ElapsedMilliseconds} (warm)");

        // Sanity guard only; not a performance guarantee.
        Assert.True(sw.ElapsedMilliseconds < 30_000, $"Render took unexpectedly long: {sw.ElapsedMilliseconds}ms");
        Assert.True(pageCount >= 3);
    }
}
