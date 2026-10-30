using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectSourceDto> AddSourceAsync(Guid projectId, AddSourceDto dto, CancellationToken cancellationToken = default);

    Task DeleteSourceAsync(Guid projectId, Guid sourceId, CancellationToken cancellationToken = default);
}
