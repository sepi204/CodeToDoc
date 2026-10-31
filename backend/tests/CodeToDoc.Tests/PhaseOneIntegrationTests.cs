using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeToDoc.Tests;

public class PhaseOneIntegrationTests : IDisposable
{
    private readonly string _tempRoot = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_tempRoot);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private WebApplicationFactory<global::Program> CreateFactory(
        Action<Dictionary<string, string?>>? configure = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["PhaseOneProcessingOptions:TempRootPath"] = _tempRoot,
        };
        configure?.Invoke(config);

        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            });
    }

    private static MultipartFormDataContent CreateMultipart(params (string FileName, byte[] Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (fileName, content) in files)
        {
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "archives", fileName);
        }

        return form;
    }

    [Fact]
    public async Task UploadingOneValidZipReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(("src/App.csproj", "<Project />"), ("src/Program.cs", "x"));
        using var response = await client.PostAsync("/api/projects/process-phase-one", CreateMultipart(("dotnet.zip", zip)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<PhaseOneResult>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.NotNull(result);
        Assert.Equal(ProcessingStatus.Succeeded, result.Status);
        Assert.Equal(2, result.ExtractedFileCount);
    }

    [Fact]
    public async Task UploadingMultipleValidZipsReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var frontendZip = TestHelpers.CreateZip(("package.json", """{"dependencies":{"react":"18.0.0"}}"""));
        var backendZip = TestHelpers.CreateZip(("App.csproj", "<Project />"));

        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("frontend.zip", frontendZip), ("backend.zip", backendZip)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<PhaseOneResult>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.Equal(2, result.ArchiveCount);
        Assert.Equal("frontend", result.ArchiveResults[0].SafeDirectoryName);
        Assert.Equal("backend", result.ArchiveResults[1].SafeDirectoryName);
    }

    [Fact]
    public async Task MissingFilesReturns400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonZipFileReturns400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("notes.txt", Encoding.UTF8.GetBytes("hello"))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CorruptZipReturns400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("corrupt.zip", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveOverLimitReturns413()
    {
        using var factory = CreateFactory(config =>
            config["PhaseOneProcessingOptions:MaxArchiveSizeBytes"] = "50");
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(("large.txt", new string('a', 500)));
        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("large.zip", zip)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ZipSlipDoesNotCreateFileOutsideWorkspace()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(("folder/../../../outside.txt", "evil"));
        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("slip.zip", zip)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.False(File.Exists(Path.Combine(_tempRoot, "outside.txt")));
        Assert.Empty(Directory.GetDirectories(_tempRoot));
    }

    [Fact]
    public async Task ResponseDoesNotContainAbsoluteTempPath()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(("Program.cs", "x"));
        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("ok.zip", zip)));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(_tempRoot, body);
    }

    [Fact]
    public async Task ResultContainsFileTreeAndDetectedStacks()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(
            ("src/App.csproj", "<Project />"),
            ("src/Program.cs", "x"),
            ("src/bin/Debug/App.dll", "binary"));

        using var response = await client.PostAsync(
            "/api/projects/process-phase-one",
            CreateMultipart(("valid-dotnet.zip", zip)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<PhaseOneResult>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.Contains(result.AggregateDetectedStacks, d => d.Name == ".NET / C#");
        Assert.NotNull(result.FileTree);
        Assert.StartsWith("source/", result.TextFileTree);
        Assert.Equal(2, result.ExtractedFileCount);
        Assert.Equal(1, result.SkippedEntryCount);
    }
}
