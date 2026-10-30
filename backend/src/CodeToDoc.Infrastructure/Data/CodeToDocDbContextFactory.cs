using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeToDoc.Infrastructure.Data;

public class CodeToDocDbContextFactory : IDesignTimeDbContextFactory<CodeToDocDbContext>
{
    public CodeToDocDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CodeToDocDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=CodeToDoc;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;");

        return new CodeToDocDbContext(optionsBuilder.Options);
    }
}
