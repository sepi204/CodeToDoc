namespace CodeToDoc.Service.Dtos;

public class UserGuideModuleResponse
{
    public string SchemaVersion { get; set; } = "1.0";

    public string DocumentType { get; set; } = "userGuide";

    public string ModuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<SynthesisStatement> Overview { get; set; } = new();

    public List<UserGuidePageSection> Pages { get; set; } = new();

    public List<string> Limitations { get; set; } = new();
}

public class UserGuidePageSection
{
    public string PageId { get; set; } = string.Empty;

    public string DisplayTitle { get; set; } = string.Empty;

    public List<SynthesisStatement> Introduction { get; set; } = new();

    public List<UserGuideFormSection> Forms { get; set; } = new();

    public List<UserGuideActionSection> Actions { get; set; } = new();

    public List<UserGuideTableSection> Tables { get; set; } = new();
}

public class UserGuideFormSection
{
    public string FormId { get; set; } = string.Empty;

    public List<UserGuideStep> Steps { get; set; } = new();

    public List<SynthesisStatement> Outcomes { get; set; } = new();
}

public class UserGuideStep
{
    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public List<string> EvidenceIds { get; set; } = new();
}

public class UserGuideActionSection
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string Certainty { get; set; } = "unknown";
}

public class UserGuideTableSection
{
    public string TableId { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new();
}
