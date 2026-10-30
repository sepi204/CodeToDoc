using CodeToDoc.Repository.Entities;
using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Mapping;

public static class ProjectMapper
{
    public static ProjectDto ToDto(Project entity)
    {
        return new ProjectDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Sources = entity.Sources.Select(ToSourceDto).ToList(),
        };
    }

    public static ProjectSourceDto ToSourceDto(ProjectSource entity)
    {
        return new ProjectSourceDto
        {
            Id = entity.Id,
            Kind = entity.Kind,
            Name = entity.FileName,
            Size = entity.FileSize,
            ContentType = entity.ContentType,
            Url = entity.RepoUrl,
        };
    }

    public static Project ToEntity(CreateProjectDto dto)
    {
        var now = DateTime.UtcNow;
        return new Project
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static ProjectSource ToEntity(Guid projectId, AddSourceDto dto)
    {
        return new ProjectSource
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = dto.Kind,
            FileName = dto.Name,
            FileSize = dto.Size,
            ContentType = dto.ContentType,
            RepoUrl = dto.Url,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
