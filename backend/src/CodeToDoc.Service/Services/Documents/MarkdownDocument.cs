namespace CodeToDoc.Service.Services.Documents;

/// <summary>A sanitized, renderer-agnostic Markdown document model.</summary>
public sealed class MarkdownDocument
{
    public string? Title { get; set; }

    public List<DocBlock> Blocks { get; } = new();

    public IReadOnlyList<DocHeading> Headings => Blocks.OfType<DocHeading>().ToList();
}

public abstract class DocBlock
{
}

public sealed class DocHeading : DocBlock
{
    public int Level { get; init; }

    public string Text { get; init; } = string.Empty;

    /// <summary>Stable, deduplicated anchor derived from <see cref="Text"/>.</summary>
    public string Id { get; init; } = string.Empty;
}

public sealed class DocParagraph : DocBlock
{
    public string Text { get; init; } = string.Empty;
}

public sealed class DocCode : DocBlock
{
    public string? Language { get; init; }

    public string Code { get; init; } = string.Empty;
}

public sealed class DocTable : DocBlock
{
    public List<string> Headers { get; } = new();

    public List<List<string>> Rows { get; } = new();
}

public sealed class DocList : DocBlock
{
    public bool Ordered { get; init; }

    public List<string> Items { get; } = new();
}

public sealed class DocQuote : DocBlock
{
    public string Text { get; init; } = string.Empty;
}
