using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodeToDoc.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
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
}
