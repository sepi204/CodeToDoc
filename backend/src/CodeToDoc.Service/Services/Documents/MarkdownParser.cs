using System.Text.RegularExpressions;
using CodeToDoc.Service.Interfaces;
using Markdig;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeToDoc.Service.Services.Documents;

/// <summary>
/// Parses Markdown (untrusted content produced by Phase 5) into a sanitized,
/// renderer-agnostic document model. Raw HTML is stripped, dangerous link
/// schemes are rejected, and headings receive stable deduplicated anchors.
/// </summary>
public sealed class MarkdownParser : IMarkdownParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .UseAutoLinks()
        .Build();

    public MarkdownDocument Parse(string markdown)
    {
        var sanitized = StripScriptAndStyle(markdown ?? string.Empty);
        var document = Markdown.Parse(sanitized, Pipeline);

        var result = new MarkdownDocument();
        var usedAnchors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in document)
        {
            switch (block)
            {
                case Markdig.Syntax.HeadingBlock heading:
                    var text = InlineToPlainText(heading.Inline);
                    result.Blocks.Add(new DocHeading
                    {
                        Level = heading.Level,
                        Text = text,
                        Id = UniqueAnchor(text, usedAnchors),
                    });
                    break;

                case Markdig.Syntax.ParagraphBlock paragraph:
                    var paragraphText = InlineToPlainText(paragraph.Inline);
                    if (paragraphText.Length > 0)
                    {
                        result.Blocks.Add(new DocParagraph { Text = paragraphText });
                    }
                    break;

                case FencedCodeBlock fenced:
                    result.Blocks.Add(new DocCode
                    {
                        Language = string.IsNullOrWhiteSpace(fenced.Info) ? null : fenced.Info,
                        Code = fenced.Lines.ToString(),
                    });
                    break;

                case Markdig.Syntax.CodeBlock indented:
                    result.Blocks.Add(new DocCode { Code = indented.Lines.ToString() });
                    break;

                case Table table:
                    result.Blocks.Add(ParseTable(table));
                    break;

                case Markdig.Syntax.ListBlock list:
                    result.Blocks.Add(ParseList(list));
                    break;

                case Markdig.Syntax.QuoteBlock quote:
                    var quoteText = string.Join(" ", quote.OfType<Block>().Select(BlockText)).Trim();
                    if (quoteText.Length > 0)
                    {
                        result.Blocks.Add(new DocQuote { Text = quoteText });
                    }
                    break;

                case HtmlBlock:
                case ThematicBreakBlock:
                    // Raw HTML is stripped; thematic breaks carry no document content.
                    break;

                default:
                    // Unknown blocks are skipped rather than emitted unsafely.
                    break;
            }
        }

        return result;
    }

    private static DocTable ParseTable(Table table)
    {
        var result = new DocTable();

        foreach (var child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            var cells = row.OfType<TableCell>()
                .Select(cell => string.Join(" ", cell.OfType<Block>().Select(BlockText)).Trim())
                .ToList();

            if (row.IsHeader)
            {
                result.Headers.AddRange(cells);
            }
            else
            {
                result.Rows.Add(cells);
            }
        }

        return result;
    }

    private static DocList ParseList(Markdig.Syntax.ListBlock list)
    {
        var result = new DocList { Ordered = list.IsOrdered };

        foreach (var child in list)
        {
            if (child is ListItemBlock item)
            {
                var text = string.Join(" ", item.OfType<Block>().Select(BlockText)).Trim();
                if (text.Length > 0)
                {
                    result.Items.Add(text);
                }
            }
        }

        return result;
    }

    /// <summary>Extracts plain text from a block (paragraphs, headings, code, containers).</summary>
    private static string BlockText(Block? block)
    {
        switch (block)
        {
            case Markdig.Syntax.ParagraphBlock paragraph:
                return InlineToPlainText(paragraph.Inline);

            case Markdig.Syntax.HeadingBlock heading:
                return InlineToPlainText(heading.Inline);

            case Markdig.Syntax.CodeBlock code:
                return code.Lines.ToString();

            case ContainerBlock container:
                return string.Join(" ", container.OfType<Block>().Select(BlockText)).Trim();

            default:
                return string.Empty;
        }
    }

    /// <summary>Converts inline Markdown to plain text, stripping raw HTML and unsafe links.</summary>
    public static string InlineToPlainText(Inline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        switch (inline)
        {
            case LiteralInline literal:
                return literal.Content.ToString();

            case CodeInline code:
                return code.Content;

            case LineBreakInline:
                return "\n";

            case LinkInline link:
                // Keep only the visible label; a link's URL is never emitted as prose.
                return InlineToPlainTextChildren(link);

            case HtmlInline:
            case HtmlEntityInline:
                return string.Empty;

            case LeafInline:
                return string.Empty;

            default:
                return inline is ContainerInline container
                    ? InlineToPlainTextChildren(container)
                    : string.Empty;
        }
    }

    private static string InlineToPlainTextChildren(ContainerInline container)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var child in container)
        {
            builder.Append(InlineToPlainText(child as Inline));
        }

        return builder.ToString();
    }

    /// <summary>Removes script/style elements and their content before parsing.</summary>
    private static string StripScriptAndStyle(string markdown)
    {
        markdown = Regex.Replace(
            markdown,
            @"<\s*(script|style)\b[^>]*>.*?<\s*/\s*\1\s*>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        markdown = Regex.Replace(
            markdown,
            @"<\s*(script|style)\b[^>]*/?>",
            string.Empty,
            RegexOptions.IgnoreCase);

        return markdown;
    }

    /// <summary>Only http(s), mailto and relative URLs are considered safe.</summary>
    public static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (url.StartsWith('#') || url.StartsWith('/') || url.StartsWith("./") || url.StartsWith("../"))
        {
            return true;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string UniqueAnchor(string text, Dictionary<string, int> used)
    {
        var slug = Slugify(text);
        if (!used.TryGetValue(slug, out var count))
        {
            used[slug] = 1;
            return slug;
        }

        used[slug] = count + 1;
        return $"{slug}-{count + 1}";
    }

    public static string Slugify(string text)
    {
        var builder = new System.Text.StringBuilder();
        var lastWasDash = false;

        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "section" : slug;
    }
}
