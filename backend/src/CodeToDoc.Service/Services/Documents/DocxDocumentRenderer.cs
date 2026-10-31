using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeToDoc.Service.Services.Documents;

/// <summary>
/// Renders a validated Markdown document to DOCX using the Open XML SDK.
/// Includes a cover, heading styles, a static table of contents, tables,
/// code blocks, page numbering, and Persian RTL support.
/// </summary>
public sealed class DocxDocumentRenderer : IDocumentRenderer
{
    public const string FormatName = "docx";

    private const string BodyFont = "Arial";
    private const string MonospaceFont = "Consolas";

    private readonly IMarkdownParser _parser;

    public DocxDocumentRenderer(IMarkdownParser parser)
    {
        _parser = parser;
    }

    public string Format => FormatName;

    public byte[] Render(DocumentRenderInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = _parser.Parse(input.Markdown);
        document.Title = input.Title;
        var rtl = input.Language.Equals("fa", StringComparison.OrdinalIgnoreCase);

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var body = mainPart.Document.Body!;

            body.Append(Cover(input.Title));
            body.Append(PageBreakParagraph());

            body.Append(Heading("Table of Contents", 1));
            foreach (var heading in document.Headings)
            {
                body.Append(Paragraph(heading.Text, headingStyle: heading.Level == 1 ? "Heading1" : "Heading2", rtl: rtl, indent: (heading.Level - 1) * 240));
            }

            body.Append(PageBreakParagraph());

            foreach (var block in document.Blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                body.Append(RenderBlock(block, rtl));
            }

            AddFooterWithPageNumber(mainPart);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph RenderBlock(DocBlock block, bool rtl)
    {
        switch (block)
        {
            case DocHeading heading:
                return Heading(heading.Text, heading.Level, rtl);

            case DocParagraph paragraph:
                return Paragraph(paragraph.Text, rtl: rtl);

            case DocCode code:
                return CodeParagraph(code.Code);

            case DocQuote quote:
                return Paragraph(quote.Text, rtl: rtl, italic: true, indent: 480);

            case DocList list:
                var items = list.Items.Select((item, index) =>
                    Paragraph(list.Ordered ? $"{index + 1}. {item}" : $"\u2022 {item}", rtl: rtl, indent: 480)).ToArray();
                var listParagraph = items[0];
                for (var i = 1; i < items.Length; i++)
                {
                    listParagraph.AppendChild(new Run(new Text() { Text = " " }));
                    listParagraph.Append(items[i]);
                }

                return listParagraph;

            case DocTable table:
                return TableParagraph(table);

            default:
                return new Paragraph();
        }
    }

    private static Paragraph Cover(string title)
    {
        var titleParagraph = Paragraph(title, headingStyle: "Title", fontSize: "56", rtl: false, center: true);
        titleParagraph.AppendChild(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        return titleParagraph;
    }

    private static Paragraph Heading(string text, int level, bool rtl = false)
    {
        var style = level switch
        {
            1 => "Heading1",
            2 => "Heading2",
            3 => "Heading3",
            _ => "Heading4",
        };

        return Paragraph(text, headingStyle: style, rtl: rtl);
    }

    private static Paragraph Paragraph(
        string text,
        string? headingStyle = null,
        string fontSize = "22",
        bool rtl = false,
        bool italic = false,
        bool center = false,
        int indent = 0)
    {
        var properties = new ParagraphProperties();

        if (!string.IsNullOrEmpty(headingStyle))
        {
            properties.Append(new ParagraphStyleId { Val = headingStyle });
        }

        if (indent > 0)
        {
            properties.Append(new Indentation { Left = indent.ToString() });
        }

        if (center)
        {
            properties.Append(new Justification { Val = JustificationValues.Center });
        }

        if (rtl)
        {
            properties.Append(new BiDi());
        }

        var runProperties = new RunProperties();
        if (rtl)
        {
            runProperties.Append(new RightToLeftText());
        }

        if (italic)
        {
            runProperties.Append(new Italic());
        }

        runProperties.Append(new RunFonts
        {
            Ascii = BodyFont,
            HighAnsi = BodyFont,
            ComplexScript = BodyFont,
        });
        runProperties.Append(new FontSize { Val = fontSize });
        runProperties.Append(new FontSizeComplexScript { Val = fontSize });

        var paragraph = new Paragraph(
            properties,
            new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        return paragraph;
    }

    private static Paragraph CodeParagraph(string code)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = MonospaceFont,
                HighAnsi = MonospaceFont,
                ComplexScript = MonospaceFont,
            },
            new FontSize { Val = "18" },
            new FontSizeComplexScript { Val = "18" });

        var paragraph = new Paragraph(
            new ParagraphProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }),
            new Run(runProperties, new Text(code) { Space = SpaceProcessingModeValues.Preserve }));

        return paragraph;
    }

    private static Paragraph TableParagraph(DocTable table)
    {
        var columnCount = Math.Max(table.Headers.Count, table.Rows.FirstOrDefault()?.Count ?? 0);
        if (columnCount == 0)
        {
            return new Paragraph();
        }

        var docTable = new Table();
        var grid = new TableGrid();
        for (var i = 0; i < columnCount; i++)
        {
            grid.Append(new GridColumn());
        }

        docTable.Append(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, grid));

        if (table.Headers.Count > 0)
        {
            var headerRow = new TableRow();
            foreach (var cell in table.Headers)
            {
                headerRow.Append(TableCell(cell, bold: true));
            }

            docTable.Append(headerRow);
        }

        foreach (var row in table.Rows)
        {
            var bodyRow = new TableRow();
            for (var i = 0; i < columnCount; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                bodyRow.Append(TableCell(value, bold: false));
            }

            docTable.Append(bodyRow);
        }

        return new Paragraph(docTable);
    }

    private static TableCell TableCell(string text, bool bold)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = BodyFont,
                HighAnsi = BodyFont,
                ComplexScript = BodyFont,
            });
        if (bold)
        {
            runProperties.Append(new Bold());
        }

        var cell = new TableCell(new Paragraph(new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        cell.Append(new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }));
        return cell;
    }

    private static Paragraph PageBreakParagraph() =>
        new(new Run(new Break { Type = BreakValues.Page }));

    private static void AddFooterWithPageNumber(MainDocumentPart mainPart)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footerId = mainPart.GetIdOfPart(footerPart);

        footerPart.Footer = new Footer(
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new Text("Page ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new FieldChar { FieldCharType = FieldCharValues.End })));

        var sectionProperties = mainPart.Document!.Body!.Elements<SectionProperties>().FirstOrDefault();
        if (sectionProperties is null)
        {
            sectionProperties = new SectionProperties();
            mainPart.Document.Body!.Append(sectionProperties);
        }

        sectionProperties.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = footerId });
    }
}
