namespace CodeToDoc.Service.Services.Storage;

/// <summary>Validates artifact identifiers and filenames to prevent path traversal.</summary>
public static class ArtifactIdValidator
{
    public static bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Length > 128)
        {
            return false;
        }

        if (fileName != fileName.Trim())
        {
            return false;
        }

        // Reject path separators and traversal segments.
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
        {
            return false;
        }

        if (Path.IsPathRooted(fileName))
        {
            return false;
        }

        // Only allow a conservative set of characters.
        foreach (var ch in fileName)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '_' && ch != '-')
            {
                return false;
            }
        }

        return true;
    }
}
