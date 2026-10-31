using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class ArchiveEntryFilter : IArchiveEntryFilter
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "packages", ".vs", ".git", "dist", "build", "out",
        ".next", ".nuxt", ".turbo", "coverage", "__pycache__", ".pytest_cache", ".mypy_cache",
        ".idea", "venv", "env", ".env",
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg",
        ".mp4", ".mov", ".avi", ".mkv", ".mp3", ".wav",
        ".woff", ".woff2", ".ttf", ".eot", ".otf",
        ".dll", ".exe", ".pdb", ".so", ".dylib", ".class", ".jar",
        ".zip", ".rar", ".7z", ".tar", ".gz", ".pdf", ".map",
    };

    private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ds_store", "thumbs.db", "package-lock.json", "yarn.lock",
        "pnpm-lock.yaml", "bun.lockb", "poetry.lock",
    };

    public ArchiveFilterDecision Evaluate(string entryName, bool isDirectory)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            return ArchiveFilterDecision.Accept;
        }

        var segments = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (isDirectory)
        {
            foreach (var segment in segments)
            {
                if (IgnoredDirectories.Contains(segment))
                {
                    return ArchiveFilterDecision.Skip(SkipReason.IgnoredDirectory);
                }
            }

            return ArchiveFilterDecision.Accept;
        }

        // Check parent directories of a file entry.
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (IgnoredDirectories.Contains(segments[i]))
            {
                return ArchiveFilterDecision.Skip(SkipReason.IgnoredDirectory);
            }
        }

        var fileName = segments[^1];

        // Environment-secret files (.env and variants) must never be extracted.
        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFilterDecision.Skip(SkipReason.IgnoredFileName);
        }

        if (IgnoredFileNames.Contains(fileName))
        {
            return ArchiveFilterDecision.Skip(SkipReason.IgnoredFileName);
        }

        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && IgnoredExtensions.Contains(extension))
        {
            return ArchiveFilterDecision.Skip(SkipReason.IgnoredExtension);
        }

        return ArchiveFilterDecision.Accept;
    }
}
