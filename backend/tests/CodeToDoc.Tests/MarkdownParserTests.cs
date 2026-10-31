using CodeToDoc.Service.Services.Documents;

namespace CodeToDoc.Tests;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void HeadingsProduceStableAnchors()
    {
        var doc = _parser.Parse("# Overview\n\n## Getting Started\n\n# Overview\n");

        var headings = doc.Headings;
        Assert.Equal(3, headings.Count);
        Assert.Equal("overview", headings[0].Id);
        Assert.Equal("getting-started", headings[1].Id);
        // Duplicate heading gets a distinct, deterministic anchor.
        Assert.Equal("overview-2", headings[2].Id);
    }

    [Fact]
    public void PersianTextIsPreserved()
    {
        var doc = _parser.Parse("# راهنمای کاربر\n\nدر صفحه «صدور فاکتور» فیلد «شناسه مشتری» را تکمیل کنید.\n");

        Assert.Contains("راهنمای کاربر", doc.Headings[0].Text);
        Assert.Contains("شناسه مشتری", Assert.IsType<DocParagraph>(doc.Blocks[1]).Text);
    }

    [Fact]
    public void RawHtmlScriptIsStripped()
    {
        var doc = _parser.Parse("# Title\n\nHello <script>alert('x')</script> world <b onclick=\"evil()\">text</b>\n");

        var paragraph = Assert.IsType<DocParagraph>(doc.Blocks[1]);
        Assert.DoesNotContain("<script>", paragraph.Text);
        Assert.DoesNotContain("alert", paragraph.Text);
        Assert.DoesNotContain("onclick", paragraph.Text);
    }

    [Fact]
    public void JavaScriptLinkIsRejected()
    {
        // The label is kept; the dangerous URL is never emitted.
        Assert.True(MarkdownParser.IsSafeUrl("https://example.com"));
        Assert.True(MarkdownParser.IsSafeUrl("/relative"));
        Assert.True(MarkdownParser.IsSafeUrl("mailto:a@b.com"));
        Assert.False(MarkdownParser.IsSafeUrl("javascript:alert(1)"));
        Assert.False(MarkdownParser.IsSafeUrl("data:text/html,<script>"));
        Assert.False(MarkdownParser.IsSafeUrl("vbscript:evil"));
    }

    [Fact]
    public void TablesAreParsed()
    {
        var doc = _parser.Parse("| A | B |\n|---|---|\n| 1 | 2 |\n");

        var table = Assert.IsType<DocTable>(Assert.Single(doc.Blocks));
        Assert.Equal(new[] { "A", "B" }, table.Headers);
        Assert.Single(table.Rows);
        Assert.Equal(new[] { "1", "2" }, table.Rows[0]);
    }

    [Fact]
    public void CodeBlocksAreParsed()
    {
        var doc = _parser.Parse("```csharp\nvar x = 1;\n```\n");

        var code = Assert.IsType<DocCode>(Assert.Single(doc.Blocks));
        Assert.Equal("csharp", code.Language);
        Assert.Contains("var x = 1;", code.Code);
    }

    [Fact]
    public void MissingHeadingsProduceNoTocEntries()
    {
        var doc = _parser.Parse("Just a paragraph with no headings.\n");

        Assert.Empty(doc.Headings);
    }

    [Fact]
    public void SlugifyIsDeterministic()
    {
        Assert.Equal("my-section", MarkdownParser.Slugify("  My Section!  "));
        Assert.Equal("section", MarkdownParser.Slugify("!!!"));
    }
}
