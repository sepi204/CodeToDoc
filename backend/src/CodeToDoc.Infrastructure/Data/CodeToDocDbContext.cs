using CodeToDoc.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeToDoc.Infrastructure.Data;

public class CodeToDocDbContext : DbContext
{
    public CodeToDocDbContext(DbContextOptions<CodeToDocDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectSource> ProjectSources => Set<ProjectSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodeToDocDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
