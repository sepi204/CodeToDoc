using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodeToDoc.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IPhaseOneProcessor _phaseOneProcessor;
    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private readonly IFullPipelineOrchestrator _fullPipelineOrchestrator;
    private readonly ICorrelationPipelineOrchestrator _correlationPipelineOrchestrator;
    private readonly ISynthesisPipelineOrchestrator _synthesisPipelineOrchestrator;

    public ProjectsController(
        IProjectService projectService,
        IPhaseOneProcessor phaseOneProcessor,
        IPipelineOrchestrator pipelineOrchestrator,
        IFullPipelineOrchestrator fullPipelineOrchestrator,
        ICorrelationPipelineOrchestrator correlationPipelineOrchestrator,
        ISynthesisPipelineOrchestrator synthesisPipelineOrchestrator)
    {
        _projectService = projectService;
        _phaseOneProcessor = phaseOneProcessor;
        _pipelineOrchestrator = pipelineOrchestrator;
        _fullPipelineOrchestrator = fullPipelineOrchestrator;
        _correlationPipelineOrchestrator = correlationPipelineOrchestrator;
        _synthesisPipelineOrchestrator = synthesisPipelineOrchestrator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetAll(CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAllAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(id, cancellationToken);
        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto dto, CancellationToken cancellationToken)
    {
        var created = await _projectService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken)
    {
        var updated = await _projectService.UpdateAsync(id, dto, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/sources")]
    public async Task<ActionResult<ProjectSourceDto>> AddSource(Guid id, AddSourceDto dto, CancellationToken cancellationToken)
    {
        var source = await _projectService.AddSourceAsync(id, dto, cancellationToken);
        return Created($"/api/projects/{id}/sources/{source.Id}", source);
    }

    [HttpDelete("{id:guid}/sources/{sourceId:guid}")]
    public async Task<IActionResult> DeleteSource(Guid id, Guid sourceId, CancellationToken cancellationToken)
    {
        await _projectService.DeleteSourceAsync(id, sourceId, cancellationToken);
        return NoContent();
    }

    [HttpPost("process-synthesis")]
    public async Task<ActionResult<SynthesisPipelineResult>> ProcessSynthesis(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        var uploads = new List<PhaseOneArchiveUpload>(files.Count);
        try
        {
            foreach (var file in files)
            {
                uploads.Add(new PhaseOneArchiveUpload
                {
                    FileName = file.FileName,
                    Length = file.Length,
                    Stream = file.OpenReadStream(),
                });
            }

            var result = await _synthesisPipelineOrchestrator.ProcessAsync(uploads, cancellationToken);
            return Ok(result);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }

    [HttpPost("process-correlation")]
    public async Task<ActionResult<CorrelationPipelineResult>> ProcessCorrelation(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        var uploads = new List<PhaseOneArchiveUpload>(files.Count);
        try
        {
            foreach (var file in files)
            {
                uploads.Add(new PhaseOneArchiveUpload
                {
                    FileName = file.FileName,
                    Length = file.Length,
                    Stream = file.OpenReadStream(),
                });
            }

            var result = await _correlationPipelineOrchestrator.ProcessAsync(uploads, cancellationToken);
            return Ok(result);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }

    [HttpPost("process")]
    public async Task<ActionResult<FullPipelineResult>> Process(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        var uploads = new List<PhaseOneArchiveUpload>(files.Count);
        try
        {
            foreach (var file in files)
            {
                uploads.Add(new PhaseOneArchiveUpload
                {
                    FileName = file.FileName,
                    Length = file.Length,
                    Stream = file.OpenReadStream(),
                });
            }

            var result = await _fullPipelineOrchestrator.ProcessAsync(uploads, cancellationToken);
            return Ok(result);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }

    [HttpPost("process-technical")]
    public async Task<ActionResult<TechnicalSkeleton>> ProcessTechnical(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        var uploads = new List<PhaseOneArchiveUpload>(files.Count);
        try
        {
            foreach (var file in files)
            {
                uploads.Add(new PhaseOneArchiveUpload
                {
                    FileName = file.FileName,
                    Length = file.Length,
                    Stream = file.OpenReadStream(),
                });
            }

            var skeleton = await _pipelineOrchestrator.ProcessAsync(uploads, cancellationToken);
            return Ok(skeleton);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }

    [HttpPost("process-phase-one")]
    public async Task<ActionResult<PhaseOneResult>> ProcessPhaseOne(
        [FromForm(Name = "archives")] List<IFormFile>? archives,
        CancellationToken cancellationToken)
    {
        var files = archives ?? new List<IFormFile>();
        var uploads = new List<PhaseOneArchiveUpload>(files.Count);
        try
        {
            foreach (var file in files)
            {
                uploads.Add(new PhaseOneArchiveUpload
                {
                    FileName = file.FileName,
                    Length = file.Length,
                    Stream = file.OpenReadStream(),
                });
            }

            var result = await _phaseOneProcessor.ProcessAsync(uploads, cancellationToken);
            return Ok(result);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }
}
