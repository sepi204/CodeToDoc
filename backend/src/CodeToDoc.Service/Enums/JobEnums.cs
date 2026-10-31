namespace CodeToDoc.Service.Enums;

/// <summary>Overall lifecycle state of a processing job.</summary>
public enum JobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    CompletedWithWarnings = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>The active phase of a running job (used for progress reporting).</summary>
public enum JobPhase
{
    Queued = 0,
    Phase1 = 1,
    Phase2 = 2,
    Phase3 = 3,
    Phase4 = 4,
    Phase5 = 5,
    Rendering = 6,
    Storing = 7,
    Completed = 8
}

/// <summary>Output file format of a compiled artifact.</summary>
public enum ArtifactFormat
{
    Markdown = 0,
    Pdf = 1,
    Docx = 2
}

/// <summary>Publication status of a single compiled artifact.</summary>
public enum ArtifactStatus
{
    Available = 0,
    Failed = 1,
    NotApplicable = 2
}
