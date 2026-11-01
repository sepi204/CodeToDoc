namespace CodeToDoc.Service.Models;

/// <summary>
/// The structured, validated subset of skeleton data that is sent to the LLM.
/// It contains only small code-like values (methods, routes, labels, field names),
/// never source files or secrets.
/// </summary>
public sealed class SynthesisPayload
{
    public string JobId { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string ModuleId { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public List<PayloadEndpoint> Endpoints { get; set; } = new();

    public List<PayloadPage> Pages { get; set; } = new();

    public List<PayloadProject> Projects { get; set; } = new();

    public List<PayloadEntity> Entities { get; set; } = new();

    public List<string> Dependencies { get; set; } = new();
}

public sealed class PayloadProject
{
    public string Root { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string? Language { get; set; }
}

public sealed class PayloadEntity
{
    public string Name { get; set; } = string.Empty;

    public List<PayloadField> Properties { get; set; } = new();
}

public sealed class PayloadEndpoint
{
    public string EndpointId { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? ControllerName { get; set; }

    public string? ActionName { get; set; }

    public bool AuthRequired { get; set; }

    public List<string> Roles { get; set; } = new();

    public List<PayloadField> RequestFields { get; set; } = new();

    public List<PayloadField> ResponseFields { get; set; } = new();

    public string? PersistenceKind { get; set; }

    public string? PersistenceOperation { get; set; }

    public string? PersistenceEntity { get; set; }
}

public sealed class PayloadField
{
    public string Name { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string Type { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}

public sealed class PayloadPage
{
    public string PageId { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string? Title { get; set; }

    public List<PayloadForm> Forms { get; set; } = new();

    public List<PayloadAction> Actions { get; set; } = new();

    public List<PayloadTable> Tables { get; set; } = new();
}

public sealed class PayloadForm
{
    public string FormId { get; set; } = string.Empty;

    public List<PayloadField> Fields { get; set; } = new();

    public List<PayloadAction> SubmitActions { get; set; } = new();
}

public sealed class PayloadAction
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string? NavigationTarget { get; set; }

    public string? PersistenceKind { get; set; }
}

public sealed class PayloadTable
{
    public string TableId { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new();
}
