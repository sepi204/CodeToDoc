using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class ArchiveEntryFilterTests
{
    private readonly ArchiveEntryFilter _filter = new();

    [Fact]
    public void KeepsNormalSourceFiles()
    {
        Assert.False(_filter.Evaluate("Program.cs", false).ShouldSkip);
        Assert.False(_filter.Evaluate("src/app/page.tsx", false).ShouldSkip);
        Assert.False(_filter.Evaluate("Backend.csproj", false).ShouldSkip);
        Assert.False(_filter.Evaluate("package.json", false).ShouldSkip);
    }

    [Fact]
    public void IgnoresBinDirectory()
    {
        Assert.Equal(SkipReason.IgnoredDirectory, _filter.Evaluate("bin/Debug/App.dll", false).Reason);
        Assert.Equal(SkipReason.IgnoredDirectory, _filter.Evaluate("src/bin", true).Reason);
    }

    [Fact]
    public void IgnoresObjDirectory()
    {
        Assert.Equal(SkipReason.IgnoredDirectory, _filter.Evaluate("obj/x.cs", false).Reason);
    }

    [Fact]
    public void IgnoresNodeModulesDirectory()
    {
        Assert.Equal(SkipReason.IgnoredDirectory, _filter.Evaluate("node_modules/pkg/index.js", false).Reason);
    }

    [Fact]
    public void IgnoresGitDirectory()
    {
        Assert.Equal(SkipReason.IgnoredDirectory, _filter.Evaluate(".git/config", false).Reason);
    }

    [Theory]
    [InlineData("logo.png")]
    [InlineData("photo.JPG")]
    [InlineData("video.mp4")]
    [InlineData("font.WOFF2")]
    [InlineData("App.dll")]
    [InlineData("archive.zip")]
    public void IgnoresMediaExtensionsCaseInsensitive(string name)
    {
        Assert.Equal(SkipReason.IgnoredExtension, _filter.Evaluate(name, false).Reason);
    }

    [Theory]
    [InlineData("package-lock.json")]
    [InlineData("yarn.lock")]
    [InlineData("pnpm-lock.yaml")]
    [InlineData(".DS_Store")]
    [InlineData("Thumbs.db")]
    public void IgnoresLockFilesAndSystemFiles(string name)
    {
        Assert.Equal(SkipReason.IgnoredFileName, _filter.Evaluate(name, false).Reason);
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.development.local")]
    [InlineData(".env.production.local")]
    public void IgnoresEnvironmentSecretFiles(string name)
    {
        Assert.Equal(SkipReason.IgnoredFileName, _filter.Evaluate(name, false).Reason);
    }

    [Theory]
    [InlineData("environment.ts")]
    [InlineData("env.ts")]
    [InlineData("process-env.js")]
    [InlineData("environment/config.ts")]
    public void DoesNotIgnoreNormalFileContainingEnv(string name)
    {
        Assert.False(_filter.Evaluate(name, false).ShouldSkip);
    }
}
