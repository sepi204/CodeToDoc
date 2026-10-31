using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IManifestParser
{
    Task<ProjectManifest> ParseAsync(string sourcePath, CancellationToken cancellationToken = default);
}
