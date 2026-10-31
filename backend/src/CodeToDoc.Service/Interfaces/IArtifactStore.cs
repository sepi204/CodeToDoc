using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>
/// Storage abstraction for compiled artifacts. Keys are always scoped to a
/// JobId and a validated artifact filename; no absolute paths are exposed.
/// </summary>
public interface IArtifactStore
{
    /// <summary>Atomically stores an artifact for a job and returns its metadata.</summary>
    Task<StoredArtifact> StoreAsync(string jobId, string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Reads an artifact's bytes, or returns null when absent.</summary>
    Task<byte[]?> ReadAsync(string jobId, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Lists all stored artifacts for a job.</summary>
    IReadOnlyList<StoredArtifact> List(string jobId);

    /// <summary>Returns metadata for a single artifact, or null when absent.</summary>
    StoredArtifact? GetMetadata(string jobId, string fileName);

    bool Exists(string jobId, string fileName);
}
