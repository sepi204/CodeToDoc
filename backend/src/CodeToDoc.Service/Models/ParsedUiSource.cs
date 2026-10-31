using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

public sealed class ParsedUiSource
{
    public string? Title { get; set; }

    public List<UiForm> Forms { get; } = new();

    public List<UiField> StandaloneFields { get; } = new();

    public List<UiAction> Actions { get; } = new();

    public List<UiTable> Tables { get; } = new();

    public List<NavigationItem> Navigation { get; } = new();

    public List<ParsedImport> Imports { get; } = new();

    public List<ParsedFetch> Fetches { get; } = new();

    public List<ParsedRouterPush> RouterPushes { get; } = new();

    public List<UiEvidence> Evidence { get; } = new();
}

public sealed class ParsedImport
{
    public string ModulePath { get; init; } = string.Empty;

    public string? DefaultImport { get; init; }

    public List<string> NamedImports { get; init; } = new();

    public int Line { get; init; }
}

public sealed class ParsedFetch
{
    public string? Method { get; init; }

    public string? Url { get; init; }

    public string? UrlExpression { get; init; }

    public int Line { get; init; }
}

public sealed class ParsedRouterPush
{
    public string? Target { get; init; }

    public string? Expression { get; init; }

    public int Line { get; init; }
}

public sealed class ParsedComponent
{
    public string Name { get; init; } = string.Empty;

    public ParsedUiSource Source { get; init; } = new();
}
