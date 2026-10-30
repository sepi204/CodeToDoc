using CodeToDoc.Infrastructure.Data;
using CodeToDoc.Repository.Entities;
using CodeToDoc.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeToDoc.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly CodeToDocDbContext _context;

    public ProjectRepository(CodeToDocDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .Include(p => p.Sources)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Sources)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Entry(project).Property(p => p.Title).IsModified = true;
        _context.Entry(project).Property(p => p.Description).IsModified = true;
        _context.Entry(project).Property(p => p.UpdatedAt).IsModified = true;

        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync(new object[] { id }, cancellationToken);
        if (project is not null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ProjectSource> AddSourceAsync(ProjectSource source, CancellationToken cancellationToken = default)
    {
        _context.ProjectSources.Add(source);
        await _context.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task DeleteSourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await _context.ProjectSources.FindAsync(new object[] { sourceId }, cancellationToken);
        if (source is not null)
        {
            _context.ProjectSources.Remove(source);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
