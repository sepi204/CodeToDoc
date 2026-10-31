using System.Text;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services.Documents;
using DocumentFormat.OpenXml.Packaging;

namespace CodeToDoc.Tests;

public class DocumentRendererTests
{
    private static readonly MarkdownParser Parser = new();

    private static DocumentRenderInput TechnicalInput(string markdown) => new()
    {
        DocumentType = "technical",
        Title = "Technical Specification",
        Language = "en",
        Markdown = markdown,
    };

    private static DocumentRenderInput UserGuideInput(string markdown) => new()
    {
        DocumentType = "userGuide",
        Title = "راهنمای کاربر",
        Language = "fa",
        Markdown = markdown,
    };

    // ---- PDF ----

    [Fact]
    public void Pdf_ProducesValidSignature()
    {
        var renderer = new PdfDocumentRenderer(Parser);
        var bytes = renderer.Render(TechnicalInput("# Title\n\nHello world.\n"));

        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Pdf_ContainsCoverAndContentPages()
    {
        var renderer = new PdfDocumentRenderer(Parser);
        var markdown = "# Overview\n\n## Details\n\nSome content.\n\n### Deeper\n\nMore content.\n";
        var bytes = renderer.Render(TechnicalInput(markdown));

        // Cover page + table of contents page + content page(s).
        var pages = CountPdfPages(bytes);
        Assert.True(pages >= 3, $"Expected at least 3 pages, got {pages}");
    }

    [Fact]
    public void Pdf_PersianMarkdown_RendersWithoutError()
    {
        var renderer = new PdfDocumentRenderer(Parser);
        var markdown = "# راهنمای کاربر\n\nدر صفحه «صدور فاکتور»، فیلد «شناسه مشتری» را تکمیل کنید.\n";
        var bytes = renderer.Render(UserGuideInput(markdown));

        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Pdf_EmptyMarkdown_RendersCoverOnly()
    {
        var renderer = new PdfDocumentRenderer(Parser);
        var bytes = renderer.Render(TechnicalInput(""));

        Assert.True(bytes.Length > 4);
        Assert.True(CountPdfPages(bytes) >= 2);
    }

    // ---- DOCX ----

    [Fact]
    public void Docx_IsValidOpenXmlPackage()
    {
        var renderer = new DocxDocumentRenderer(Parser);
        var bytes = renderer.Render(TechnicalInput("# Title\n\nHello **bold** world.\n"));

        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        Assert.NotNull(doc.MainDocumentPart);
        Assert.NotNull(doc.MainDocumentPart!.Document);
        Assert.NotNull(doc.MainDocumentPart.Document.Body);
    }

    [Fact]
    public void Docx_PreservesHeadingsTableAndCode()
    {
        var renderer = new DocxDocumentRenderer(Parser);
        var markdown = """
            # Overview

            A paragraph.

            | A | B |
            |---|---|
            | 1 | 2 |

            ```csharp
            var x = 1;
            ```
            """;
        var bytes = renderer.Render(TechnicalInput(markdown));

        var text = ExtractDocxText(bytes);
        Assert.Contains("Overview", text);
        Assert.Contains("A", text);
        Assert.Contains("var x = 1;", text);
    }

    [Fact]
    public void Docx_PreservesPersianText()
    {
        var renderer = new DocxDocumentRenderer(Parser);
        var markdown = "# راهنمای کاربر\n\nدر صفحه «صدور فاکتور»، فیلد «شناسه مشتری» را تکمیل کنید.\n";
        var bytes = renderer.Render(UserGuideInput(markdown));

        var text = ExtractDocxText(bytes);
        Assert.Contains("راهنمای کاربر", text);
        Assert.Contains("شناسه مشتری", text);
    }

    [Fact]
    public void Docx_IncludesFooterPart()
    {
        var renderer = new DocxDocumentRenderer(Parser);
        var bytes = renderer.Render(TechnicalInput("# Title\n\nContent.\n"));

        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        Assert.NotNull(doc.MainDocumentPart);
        Assert.NotNull(doc.MainDocumentPart!.FooterParts);
        Assert.True(doc.MainDocumentPart.FooterParts.Any());
    }

    // ---- helpers ----

    private static int CountPdfPages(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"/Type\s*/Page\b").Count;
    }

    private static string ExtractDocxText(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        return body.InnerText;
    }
}
