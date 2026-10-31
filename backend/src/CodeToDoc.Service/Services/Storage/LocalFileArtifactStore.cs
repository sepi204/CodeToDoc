using System.Security.Cryptography;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services.Storage;

/// <summary>
/// Local-filesystem artifact store. Artifacts are scoped to a job directory,
/// written atomically (temp file + move), and described by safe metadata only.
/// </summary>
public sealed class LocalFileArtifactStore : IArtifactStore
{
    private readonly string _root;
    private readonly long _maxDownloadBytes;

    public LocalFileArtifactStore(
        IOptions<ArtifactStorageOptions> options,
        IOptions<PhaseOneProcessingOptions> phaseOneOptions)
    {
        var configured = options.Value.RootPath;
        _root = string.IsNullOrWhiteSpace(configured)
            ? (string.IsNullOrWhiteSpace(phaseOneOptions.Value.TempRootPath)
                ? Path.Combine(Path.GetTempPath(), "codetodoc")
                : phaseOneOptions.Value.TempRootPath)
            : configured;

        _maxDownloadBytes = options.Value.MaxDownloadBytes;
    }

    public Task<StoredArtifact> StoreAsync(
        string jobId, string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!ArtifactIdValidator.IsValidFileName(fileName))
        {
            throw new ArgumentException("Invalid artifact file name.", nameof(fileName));
        }

        var jobDir = JobDirectory(jobId);
        Directory.CreateDirectory(jobDir);

        var finalPath = Path.Combine(jobDir, fileName);
        var tempPath = finalPath + ".tmp";

        File.WriteAllBytes(tempPath, content);
        File.Move(tempPath, finalPath, overwrite: true);

        var artifact = new StoredArtifact
        {
            ArtifactId = fileName,
            DocumentType = InferDocumentType(fileName),
            Format = InferFormat(fileName),
            Status = ArtifactStatus.Available,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            Checksum = Checksum(content),
            CreatedAtUtc = DateTime.UtcNow,
        };

        return Task.FromResult(artifact);
    }

    public Task<byte[]?> ReadAsync(string jobId, string fileName, CancellationToken cancellationToken = default)
    {
        if (!ArtifactIdValidator.IsValidFileName(fileName))
        {
            return Task.FromResult<byte[]?>(null);
        }

        var path = Path.Combine(JobDirectory(jobId), fileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<byte[]?>(null);
        }

        var info = new FileInfo(path);
        if (info.Length > _maxDownloadBytes)
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.FromResult<byte[]?>(File.ReadAllBytes(path));
    }

    public IReadOnlyList<StoredArtifact> List(string jobId)
    {
        var jobDir = JobDirectory(jobId);
        if (!Directory.Exists(jobDir))
        {
            return Array.Empty<StoredArtifact>();
        }

        var artifacts = new List<StoredArtifact>();
        foreach (var file in Directory.EnumerateFiles(jobDir))
        {
            var fileName = Path.GetFileName(file);
            if (!ArtifactIdValidator.IsValidFileName(fileName))
            {
                continue;
            }

            var info = new FileInfo(file);
            artifacts.Add(new StoredArtifact
            {
                ArtifactId = fileName,
                DocumentType = InferDocumentType(fileName),
                Format = InferFormat(fileName),
                Status = ArtifactStatus.Available,
                FileName = fileName,
                ContentType = InferContentType(fileName),
                SizeBytes = info.Length,
                Checksum = null, // computed on demand; listing avoids hashing large files
                CreatedAtUtc = info.CreationTimeUtc,
            });
        }

        return artifacts.OrderBy(a => a.FileName, StringComparer.Ordinal).ToList();
    }

    public StoredArtifact? GetMetadata(string jobId, string fileName)
    {
        if (!ArtifactIdValidator.IsValidFileName(fileName))
        {
            return null;
        }

        var path = Path.Combine(JobDirectory(jobId), fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        return new StoredArtifact
        {
            ArtifactId = fileName,
            DocumentType = InferDocumentType(fileName),
            Format = InferFormat(fileName),
            Status = ArtifactStatus.Available,
            FileName = fileName,
            ContentType = InferContentType(fileName),
            SizeBytes = info.Length,
            Checksum = null,
            CreatedAtUtc = info.CreationTimeUtc,
        };
    }

    public bool Exists(string jobId, string fileName)
    {
        if (!ArtifactIdValidator.IsValidFileName(fileName))
        {
            return false;
        }

        return File.Exists(Path.Combine(JobDirectory(jobId), fileName));
    }

    private string JobDirectory(string jobId) => Path.Combine(_root, jobId);

    private static string Checksum(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string InferContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".md" => "text/markdown; charset=utf-8",
        ".json" => "application/json",
        _ => "application/octet-stream",
    };

    private static ArtifactFormat InferFormat(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => ArtifactFormat.Pdf,
        ".docx" => ArtifactFormat.Docx,
        _ => ArtifactFormat.Markdown,
    };

    private static string InferDocumentType(string fileName)
    {
        if (fileName.StartsWith("user_guide", StringComparison.OrdinalIgnoreCase))
        {
            return "userGuide";
        }

        if (fileName.StartsWith("technical_document", StringComparison.OrdinalIgnoreCase))
        {
            return "technical";
        }

        return fileName;
    }
}
