using CodeToDoc.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeToDoc.Infrastructure.Data.Configurations;

public class ProjectSourceConfiguration : IEntityTypeConfiguration<ProjectSource>
{
    public void Configure(EntityTypeBuilder<ProjectSource> builder)
    {
        builder.ToTable("ProjectSources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind)
            .HasConversion<int>();

        builder.Property(x => x.FileName)
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .HasMaxLength(200);

        builder.Property(x => x.RepoUrl)
            .HasMaxLength(2000);
    }
}
