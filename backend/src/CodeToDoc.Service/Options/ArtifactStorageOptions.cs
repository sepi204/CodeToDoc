namespace CodeToDoc.Service.Options;

public class ArtifactStorageOptions
{
    public const string SectionName = "ArtifactStorage";

    /// <summary>
    /// Root directory for compiled artifacts. When empty, the Phase 1
    /// workspace root (TempRootPath) is used so artifacts remain co-located
    /// with the existing skeleton/manifest files.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Maximum number of bytes that may be served in a single download.</summary>
    public long MaxDownloadBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Maximum number of bytes a single generated PDF/DOCX may occupy.</summary>
    public long MaxArtifactBytes { get; set; } = 50L * 1024 * 1024;
}
