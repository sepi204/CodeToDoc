using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services.Storage;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Tests;

public class ArtifactStoreTests : IDisposable
{
    private readonly string _root = TestHelpers.CreateTempRoot();
    private readonly LocalFileArtifactStore _store;

    public ArtifactStoreTests()
    {
        _store = new LocalFileArtifactStore(
            Options.Create(new ArtifactStorageOptions { RootPath = _root }),
            Options.Create(new PhaseOneProcessingOptions { TempRootPath = _root }));
    }

    public void Dispose() => TestHelpers.DeleteDirectory(_root);

    [Fact]
    public async Task StoreAndRead_RoundTrips_WithChecksumAndSize()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("hello");
        var stored = await _store.StoreAsync("job1", "technical_document.pdf", content, "application/pdf");

        Assert.Equal("technical_document.pdf", stored.ArtifactId);
        Assert.Equal(ArtifactFormat.Pdf, stored.Format);
        Assert.Equal(5, stored.SizeBytes);
        Assert.StartsWith("sha256:", stored.Checksum);

        var read = await _store.ReadAsync("job1", "technical_document.pdf");
        Assert.Equal(content, read);
    }

    [Fact]
    public async Task NamespaceIsolation_PreventsCrossJobAccess()
    {
        await _store.StoreAsync("job1", "a.pdf", new byte[] { 1 }, "application/pdf");

        Assert.True(_store.Exists("job1", "a.pdf"));
        Assert.False(_store.Exists("job2", "a.pdf"));
    }

    [Fact]
    public async Task MissingArtifact_ReturnsNull()
    {
        Assert.Null(await _store.ReadAsync("job1", "missing.pdf"));
        Assert.Null(_store.GetMetadata("job1", "missing.pdf"));
    }

    [Fact]
    public async Task List_ReturnsMetadataWithoutAbsolutePaths()
    {
        await _store.StoreAsync("job1", "a.pdf", new byte[] { 1 }, "application/pdf");
        await _store.StoreAsync("job1", "b.docx", new byte[] { 1, 2 }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var list = _store.List("job1");
        Assert.Equal(2, list.Count);
        Assert.All(list, a => Assert.False(Path.IsPathRooted(a.ArtifactId)));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/b.pdf")]
    [InlineData("a\\b.pdf")]
    [InlineData("C:\\absolute.pdf")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("file with space.pdf")]
    public void InvalidArtifactIds_AreRejected(string fileName)
    {
        Assert.False(ArtifactIdValidator.IsValidFileName(fileName));
    }

    [Theory]
    [InlineData("technical_document.pdf")]
    [InlineData("user_guide.docx")]
    [InlineData("technical_document.md")]
    [InlineData("synthesis_manifest.json")]
    public void ValidArtifactIds_AreAccepted(string fileName)
    {
        Assert.True(ArtifactIdValidator.IsValidFileName(fileName));
    }
}
