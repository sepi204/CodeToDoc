using CodeToDoc.Repository.Entities;

namespace CodeToDoc.Repository.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);

    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectSource> AddSourceAsync(ProjectSource source, CancellationToken cancellationToken = default);

    Task DeleteSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
}
