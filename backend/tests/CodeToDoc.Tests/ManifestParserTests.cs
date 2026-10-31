using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class ManifestParserTests : IDisposable
{
    private readonly string _root = TestHelpers.CreateTempRoot();
    private readonly ManifestParser _parser = new();
    private readonly ArchitectureInferenceEngine _engine = new();

    public void Dispose() => TestHelpers.DeleteDirectory(_root);

    private async Task<ProjectManifest> ParseFileAsync(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_root, fileName), content);
        return await _parser.ParseAsync(_root);
    }

    [Fact]
    public async Task ParsesCsprojPackageReferences()
    {
        var manifest = await ParseFileAsync("MyApi.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
                <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);

        var npgsql = Assert.Single(manifest.Dependencies, d => d.Name == "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.Equal("9.0.0", npgsql.Version);
        Assert.Contains("aspnetcore", manifest.FrameworkHints);
    }

    [Fact]
    public async Task ParsesPackageJsonDependenciesAndDevDependencies()
    {
        var manifest = await ParseFileAsync("package.json",
            """{"dependencies":{"next":"14.0.0"},"devDependencies":{"typescript":"5.4.0"}}""");

        Assert.Contains(manifest.Dependencies, d => d.Name == "next" && d.Version == "14.0.0");
        Assert.Contains(manifest.Dependencies, d => d.Name == "typescript" && d.Version == "5.4.0");
    }

    [Fact]
    public void InfersPostgreSqlAndEfCoreFromNpgsql()
    {
        var manifest = new ProjectManifest();
        manifest.FrameworkHints.Add("aspnetcore");
        manifest.PrimaryLanguage = "C#";
        manifest.Dependencies.Add(new ParsedDependency { Name = "Npgsql.EntityFrameworkCore.PostgreSQL", Version = "9.0.0" });

        var result = _engine.Infer(manifest);

        Assert.Equal("PostgreSQL", result.Summary.Database);
        Assert.Equal("Entity Framework Core", result.Summary.Orm);
        Assert.Equal("ASP.NET Core", result.Summary.Framework);
    }

    [Fact]
    public void InfersJwtBearerAuth()
    {
        var manifest = new ProjectManifest();
        manifest.FrameworkHints.Add("aspnetcore");
        manifest.PrimaryLanguage = "C#";
        manifest.Dependencies.Add(new ParsedDependency { Name = "Microsoft.AspNetCore.Authentication.JwtBearer", Version = "10.0.0" });

        var result = _engine.Infer(manifest);

        Assert.Equal("JWT Bearer", result.Summary.Auth);
    }

    [Fact]
    public async Task MalformedPackageJsonDoesNotCrash()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), "{not valid json");
        var parsed = await _parser.ParseAsync(_root);

        Assert.NotEmpty(parsed.Warnings);
    }
}
