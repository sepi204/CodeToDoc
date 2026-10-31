using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Api.Controllers;

[ApiController]
[Route("api/projects/jobs")]
public class JobsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IJobQueue _queue;
    private readonly IJobStatusStore _statusStore;
    private readonly IArtifactStore _artifactStore;
    private readonly IArchitectureDiagramBuilder _diagramBuilder;
    private readonly LlmOptions _llmOptions;

    public JobsController(
        IJobQueue queue,
        IJobStatusStore statusStore,
        IArtifactStore artifactStore,
        IArchitectureDiagramBuilder diagramBuilder,
        IOptions<LlmOptions> llmOptions)
    {
        _queue = queue;
        _statusStore = statusStore;
        _artifactStore = artifactStore;
        _diagramBuilder = diagramBuilder;
        _llmOptions = llmOptions.Value;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<JobStartResponse>> Start(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        if (files.Count == 0)
        {
            return BadRequest("At least one archive is required.");
        }

        var jobId = Guid.NewGuid().ToString("N");
        var data = new List<JobArchiveData>(files.Count);

        foreach (var file in files)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            data.Add(new JobArchiveData { FileName = file.FileName, Content = stream.ToArray() });
        }

        _statusStore.Upsert(new JobRecord
        {
            JobId = jobId,
            Status = JobStatus.Queued,
            Phase = JobPhase.Queued,
            ProgressMessage = "Queued",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _queue.EnqueueAsync(new JobRequest { JobId = jobId, Archives = data }, cancellationToken);

        return Ok(new JobStartResponse { JobId = jobId, Status = "queued" });
    }

    [HttpGet("{jobId}")]
    public ActionResult<JobStatusResponse> GetStatus(string jobId)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out var record) || record is null)
        {
            return NotFound("Unknown job.");
        }

        return Ok(JobStatusResponse.From(record));
    }

    [HttpGet("{jobId}/artifacts")]
    public ActionResult<IReadOnlyList<ArtifactFileEntry>> ListArtifacts(string jobId)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out var record) || record is null)
        {
            return NotFound("Unknown job.");
        }

        return Ok(record.Artifacts);
    }

    [HttpGet("{jobId}/artifacts/{artifactId}")]
    public async Task<IActionResult> Download(string jobId, string artifactId, CancellationToken cancellationToken)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!ArtifactIdValidator.IsValidFileName(artifactId))
        {
            return BadRequest("Invalid artifact id.");
        }

        if (!_statusStore.TryGet(jobId, out var record) || record is null)
        {
            return NotFound("Unknown job.");
        }

        var bytes = await _artifactStore.ReadAsync(jobId, artifactId, cancellationToken);
        if (bytes is null)
        {
            return NotFound("Artifact not found or not ready.");
        }

        var contentType = _artifactStore.GetMetadata(jobId, artifactId)?.ContentType ?? "application/octet-stream";
        return File(bytes, contentType, artifactId);
    }

    [HttpGet("{jobId}/documents/{documentType}")]
    public async Task<ActionResult<DocumentPreviewResponse>> GetDocument(string jobId, string documentType, CancellationToken cancellationToken)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out var record) || record is null)
        {
            return NotFound("Unknown job.");
        }

        var isTechnical = documentType.Equals("technical", StringComparison.OrdinalIgnoreCase);
        var isUserGuide = documentType.Equals("userGuide", StringComparison.OrdinalIgnoreCase);
        if (!isTechnical && !isUserGuide)
        {
            return BadRequest("Unknown document type.");
        }

        var baseName = isTechnical ? "technical_document" : "user_guide";
        var fileName = $"{baseName}.md";
        var bytes = await _artifactStore.ReadAsync(jobId, fileName, cancellationToken);

        var availableFormats = record.Artifacts
            .Where(a => a.DocumentType == (isTechnical ? "technical" : "userGuide") && a.Status == ArtifactStatus.Available)
            .Select(a => a.Format.ToString().ToLowerInvariant())
            .Distinct()
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (bytes is null)
        {
            return Ok(new DocumentPreviewResponse
            {
                DocumentType = isTechnical ? "technical" : "userGuide",
                Language = isTechnical ? _llmOptions.TechnicalDocumentLanguage : _llmOptions.UserGuideLanguage,
                Status = "notApplicable",
                AvailableFormats = availableFormats,
            });
        }

        return Ok(new DocumentPreviewResponse
        {
            DocumentType = isTechnical ? "technical" : "userGuide",
            Language = isTechnical ? _llmOptions.TechnicalDocumentLanguage : _llmOptions.UserGuideLanguage,
            Content = System.Text.Encoding.UTF8.GetString(bytes),
            AvailableFormats = availableFormats,
            Status = "available",
        });
    }

    [HttpGet("{jobId}/technical")]
    public async Task<ActionResult<TechnicalSkeleton>> GetTechnical(string jobId, CancellationToken cancellationToken)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out _))
        {
            return NotFound("Unknown job.");
        }

        var bytes = await _artifactStore.ReadAsync(jobId, "technical_skeleton.json", cancellationToken);
        if (bytes is null)
        {
            return NotFound("Technical skeleton not available.");
        }

        return Ok(JsonSerializer.Deserialize<TechnicalSkeleton>(bytes, JsonOptions));
    }

    [HttpGet("{jobId}/diagram")]
    public async Task<ActionResult<ArchitectureDiagram>> GetDiagram(string jobId, CancellationToken cancellationToken)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out _))
        {
            return NotFound("Unknown job.");
        }

        var technicalBytes = await _artifactStore.ReadAsync(jobId, "technical_skeleton.json", cancellationToken);
        if (technicalBytes is null)
        {
            return Ok(new ArchitectureDiagram { Available = false, UnavailableReason = "No technical skeleton available." });
        }

        var technical = JsonSerializer.Deserialize<TechnicalSkeleton>(technicalBytes, JsonOptions)!;

        UiSkeleton? ui = null;
        var uiBytes = await _artifactStore.ReadAsync(jobId, "ui_skeleton.json", cancellationToken);
        if (uiBytes is not null)
        {
            ui = JsonSerializer.Deserialize<UiSkeleton>(uiBytes, JsonOptions);
        }

        CorrelationSkeleton? correlation = null;
        var correlationBytes = await _artifactStore.ReadAsync(jobId, "correlation_skeleton.json", cancellationToken);
        if (correlationBytes is not null)
        {
            correlation = JsonSerializer.Deserialize<CorrelationSkeleton>(correlationBytes, JsonOptions);
        }

        return Ok(_diagramBuilder.Build(technical, ui, correlation));
    }

    [HttpPost("{jobId}/cancel")]
    public ActionResult Cancel(string jobId)
    {
        if (!IsValidJobId(jobId))
        {
            return BadRequest("Invalid job id.");
        }

        if (!_statusStore.TryGet(jobId, out _))
        {
            return NotFound("Unknown job.");
        }

        var cancelled = _queue.Cancel(jobId);
        return cancelled ? Ok(new { status = "cancelling" }) : Conflict("Job is not running.");
    }

    private static bool IsValidJobId(string jobId) =>
        jobId.Length == 32 && jobId.All(char.IsAsciiHexDigit);
}
