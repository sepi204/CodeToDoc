using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Phase 6: compiles the Phase 5 Markdown documents into PDF and DOCX,
/// publishes all final artifacts, and writes the artifact manifest.
/// A failed PDF render never discards a successful DOCX (and vice versa).
/// </summary>
public sealed class PhaseSixProcessor : IPhaseSixProcessor
{
    public const string ManifestArtifact = "artifact_manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IReadOnlyList<IDocumentRenderer> _renderers;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<PhaseSixProcessor> _logger;

    public PhaseSixProcessor(
        IEnumerable<IDocumentRenderer> renderers,
        IOptions<LlmOptions> llmOptions,
        ILogger<PhaseSixProcessor> logger)
    {
        _renderers = renderers.ToList();
        _llmOptions = llmOptions.Value;
        _logger = logger;
    }

    public async Task<ArtifactManifest> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var manifest = new ArtifactManifest
        {
            JobId = workspace.JobId,
            GeneratedAtUtc = DateTime.UtcNow,
        };

        await CompileDocumentAsync(workspace, manifest,
            documentType: "technical",
            baseName: "technical_document",
            title: "Technical Specification",
            language: _llmOptions.TechnicalDocumentLanguage,
            cancellationToken);

        await CompileDocumentAsync(workspace, manifest,
            documentType: "userGuide",
            baseName: "user_guide",
            title: "راهنمای کاربر",
            language: _llmOptions.UserGuideLanguage,
            cancellationToken);

        manifest.Status = Aggregate(manifest.Files);

        await WriteAtomicAsync(workspace.JobRootPath, ManifestArtifact,
            JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

        _logger.LogInformation("Phase 6 completed for job {JobId} (status {Status}).", workspace.JobId, manifest.Status);

        return manifest;
    }

    private async Task CompileDocumentAsync(
        JobWorkspace workspace,
        ArtifactManifest manifest,
        string documentType,
        string baseName,
        string title,
        string language,
        CancellationToken cancellationToken)
    {
        var mdFileName = $"{baseName}.md";
        var mdPath = Path.Combine(workspace.JobRootPath, mdFileName);

        if (!File.Exists(mdPath))
        {
            manifest.Files.Add(NotApplicable(documentType, baseName, ArtifactFormat.Markdown, mdFileName));
            manifest.Files.Add(NotApplicable(documentType, baseName, ArtifactFormat.Pdf, $"{baseName}.pdf"));
            manifest.Files.Add(NotApplicable(documentType, baseName, ArtifactFormat.Docx, $"{baseName}.docx"));
            return;
        }

        var markdown = File.ReadAllText(mdPath);

        // Markdown is already the canonical artifact produced by Phase 5.
        var markdownBytes = System.Text.Encoding.UTF8.GetBytes(markdown);
        AddArtifact(manifest, documentType, ArtifactFormat.Markdown, mdFileName, "text/markdown; charset=utf-8", markdownBytes);

        var input = new DocumentRenderInput
        {
            DocumentType = documentType,
            Title = title,
            Language = language,
            Markdown = markdown,
        };

        foreach (var renderer in _renderers.OrderBy(r => r.Format, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = renderer.Format;
            var fileName = $"{baseName}.{extension}";
            var contentType = extension == "pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            try
            {
                var bytes = renderer.Render(input, cancellationToken);
                if (bytes.Length == 0)
                {
                    throw new InvalidOperationException("Renderer produced an empty document.");
                }

                var path = Path.Combine(workspace.JobRootPath, fileName);
                var tempPath = path + ".tmp";
                await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
                File.Move(tempPath, path, overwrite: true);

                AddArtifact(manifest, documentType, ToFormat(extension), fileName, contentType, bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to render {Format} for {DocumentType}.", extension, documentType);
                manifest.Files.Add(new ArtifactFileEntry
                {
                    DocumentType = documentType,
                    Format = ToFormat(extension),
                    Status = ArtifactStatus.Failed,
                    ArtifactId = fileName,
                    FileName = fileName,
                    ContentType = contentType,
                    Diagnostics =
                    {
                        new UiDiagnostic
                        {
                            Code = "render.failed",
                            Severity = DiagnosticSeverity.Error,
                            Message = $"Failed to render {extension}: {ex.Message}",
                        },
                    },
                });
            }
        }
    }

    private static void AddArtifact(
        ArtifactManifest manifest,
        string documentType,
        ArtifactFormat format,
        string fileName,
        string contentType,
        byte[] content)
    {
        manifest.Files.Add(new ArtifactFileEntry
        {
            DocumentType = documentType,
            Format = format,
            Status = ArtifactStatus.Available,
            ArtifactId = fileName,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            Checksum = Checksum(content),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static ArtifactFileEntry NotApplicable(
        string documentType, string baseName, ArtifactFormat format, string fileName)
    {
        var contentType = format switch
        {
            ArtifactFormat.Pdf => "application/pdf",
            ArtifactFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "text/markdown; charset=utf-8",
        };

        return new ArtifactFileEntry
        {
            DocumentType = documentType,
            Format = format,
            Status = ArtifactStatus.NotApplicable,
            ArtifactId = fileName,
            FileName = fileName,
            ContentType = contentType,
        };
    }

    private static ArtifactFormat ToFormat(string extension) => extension switch
    {
        "pdf" => ArtifactFormat.Pdf,
        "docx" => ArtifactFormat.Docx,
        _ => ArtifactFormat.Markdown,
    };

    private static string Aggregate(IEnumerable<ArtifactFileEntry> files)
    {
        var list = files.ToList();
        var applicable = list.Where(f => f.Status != ArtifactStatus.NotApplicable).ToList();

        if (applicable.Count == 0)
        {
            return "failed";
        }

        if (applicable.Any(f => f.Status == ArtifactStatus.Failed))
        {
            return "partial";
        }

        return "complete";
    }

    private static string Checksum(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task WriteAtomicAsync(
        string jobRootPath, string fileName, string content, CancellationToken cancellationToken)
    {
        var finalPath = Path.Combine(jobRootPath, fileName);
        var tempPath = finalPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}
