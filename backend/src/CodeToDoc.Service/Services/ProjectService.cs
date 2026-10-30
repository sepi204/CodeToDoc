using CodeToDoc.Repository.Enums;
using CodeToDoc.Repository.Interfaces;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Exceptions;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Mapping;

namespace CodeToDoc.Service.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _repository.GetAllAsync(cancellationToken);
        return projects.Select(ProjectMapper.ToDto).ToList();
    }

    public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Project '{id}' was not found.");

        return ProjectMapper.ToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default)
    {
        var entity = ProjectMapper.ToEntity(dto);
        var created = await _repository.CreateAsync(entity, cancellationToken);
        return ProjectMapper.ToDto(created);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Project '{id}' was not found.");

        project.Title = dto.Title.Trim();
        project.Description = dto.Description.Trim();
        project.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(project, cancellationToken);
        return ProjectMapper.ToDto(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Project '{id}' was not found.");

        await _repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<ProjectSourceDto> AddSourceAsync(Guid projectId, AddSourceDto dto, CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{projectId}' was not found.");

        ValidateSource(dto);

        var source = ProjectMapper.ToEntity(projectId, dto);
        var created = await _repository.AddSourceAsync(source, cancellationToken);
        return ProjectMapper.ToSourceDto(created);
    }

    public async Task DeleteSourceAsync(Guid projectId, Guid sourceId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{projectId}' was not found.");

        var source = project.Sources.FirstOrDefault(s => s.Id == sourceId)
            ?? throw new NotFoundException($"Source '{sourceId}' was not found for project '{projectId}'.");

        await _repository.DeleteSourceAsync(source.Id, cancellationToken);
    }

    private static void ValidateSource(AddSourceDto dto)
    {
        if (dto.Kind == SourceKind.File && string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new BadRequestException("A file source requires a name.");
        }

        if (dto.Kind == SourceKind.Repo && string.IsNullOrWhiteSpace(dto.Url))
        {
            throw new BadRequestException("A repository source requires a URL.");
        }
    }
}
