using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class UiForm
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? SubmitHandler { get; set; }

    public List<UiField> Fields { get; set; } = new();

    public List<string> SubmitActionIds { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiField
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string Component { get; set; } = string.Empty;

    public InputKind InputType { get; set; }

    public string? Label { get; set; }

    public string? Placeholder { get; set; }

    public bool? Required { get; set; }

    public List<UiValidationRule> ValidationRules { get; set; } = new();

    public List<UiOption> Options { get; set; } = new();

    public UiVisibility Visibility { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiValidationRule
{
    public string Kind { get; set; } = string.Empty;

    public object? Value { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiOption
{
    public string Value { get; set; } = string.Empty;

    public string? Label { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiVisibility
{
    public VisibilityState State { get; set; }

    public string? Expression { get; set; }
}
