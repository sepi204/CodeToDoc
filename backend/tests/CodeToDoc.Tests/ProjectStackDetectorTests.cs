using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class ProjectStackDetectorTests
{
    private static ProjectStackDetector CreateDetector() => new();

    [Theory]
    [InlineData("src/App.csproj", ".NET / C#")]
    [InlineData("App.fsproj", ".NET / F#")]
    [InlineData("App.vbproj", ".NET / Visual Basic")]
    [InlineData("App.sln", ".NET solution")]
    public void DetectsDotNet(string path, string expectedName)
    {
        var detector = CreateDetector();
        detector.ScanEntry(path);
        Assert.Contains(detector.Detections, d => d.Name == expectedName);
    }

    [Theory]
    [InlineData("requirements.txt")]
    [InlineData("pyproject.toml")]
    [InlineData("Pipfile")]
    public void DetectsPython(string path)
    {
        var detector = CreateDetector();
        detector.ScanEntry(path);
        Assert.Contains(detector.Detections, d => d.Name == "Python");
    }

    [Fact]
    public void DetectsNodeJsFromPackageJson()
    {
        var detector = CreateDetector();
        detector.ScanEntry("package.json");
        Assert.Contains(detector.Detections, d => d.Name == "Node.js");
    }

    [Fact]
    public void DetectsNextJsFromNextPackage()
    {
        var detector = CreateDetector();
        detector.ScanEntry("package.json");
        detector.ScanPackageJson("package.json", """{"dependencies":{"next":"14.0.0"}}""");
        Assert.Contains(detector.Detections, d => d.Name == "Next.js");
    }

    [Fact]
    public void DetectsReactFromReactPackage()
    {
        var detector = CreateDetector();
        detector.ScanPackageJson("package.json", """{"dependencies":{"react":"18.2.0"}}""");
        Assert.Contains(detector.Detections, d => d.Name == "React");
    }

    [Fact]
    public void DetectsVueFromVuePackage()
    {
        var detector = CreateDetector();
        detector.ScanPackageJson("package.json", """{"devDependencies":{"vue":"3.4.0"}}""");
        Assert.Contains(detector.Detections, d => d.Name == "Vue");
    }

    [Fact]
    public void DetectsAngularFromAngularCorePackage()
    {
        var detector = CreateDetector();
        detector.ScanPackageJson("package.json", """{"dependencies":{"@angular/core":"17.0.0"}}""");
        Assert.Contains(detector.Detections, d => d.Name == "Angular");
    }

    [Fact]
    public void DetectsAngularFromAngularJson()
    {
        var detector = CreateDetector();
        detector.ScanEntry("angular.json");
        Assert.Contains(detector.Detections, d => d.Name == "Angular");
    }

    [Theory]
    [InlineData("pom.xml", "Java")]
    [InlineData("build.gradle", "Java")]
    [InlineData("build.gradle.kts", "Java")]
    [InlineData("go.mod", "Go")]
    [InlineData("Cargo.toml", "Rust")]
    [InlineData("composer.json", "PHP")]
    [InlineData("Dockerfile", "Docker")]
    [InlineData("docker-compose.yml", "Docker")]
    [InlineData("compose.yaml", "Docker")]
    public void DetectsOtherStacks(string path, string expectedName)
    {
        var detector = CreateDetector();
        detector.ScanEntry(path);
        Assert.Contains(detector.Detections, d => d.Name == expectedName);
    }

    [Fact]
    public void HandlesMalformedPackageJsonWithoutCrashing()
    {
        var detector = CreateDetector();
        detector.ScanEntry("package.json");
        detector.ScanPackageJson("package.json", "{not valid json");

        Assert.Contains(detector.Detections, d => d.Name == "Node.js");
        Assert.NotEmpty(detector.Warnings);
    }

    [Fact]
    public void DeduplicatesDetections()
    {
        var detector = CreateDetector();
        detector.ScanEntry("package.json");
        detector.ScanEntry("sub/package.json");
        detector.ScanPackageJson("package.json", """{"dependencies":{"next":"14.0.0"}}""");

        Assert.Single(detector.Detections, d => d.Name == "Node.js");
        Assert.Single(detector.Detections, d => d.Name == "Next.js");
    }

    [Fact]
    public void RecordsEvidence()
    {
        var detector = CreateDetector();
        detector.ScanPackageJson("frontend/package.json", """{"dependencies":{"next":"14.0.0"}}""");

        var next = detector.Detections.Single(d => d.Name == "Next.js");
        Assert.Contains("frontend/package.json: dependencies.next", next.Evidence);
    }
}
