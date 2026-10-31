using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Exceptions;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class PhaseOneProcessorTests : IDisposable
{
    private readonly string _tempRoot = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_tempRoot);

    private PhaseOneProcessor CreateProcessor(Action<PhaseOneProcessingOptions>? configure = null)
    {
        var options = TestHelpers.DefaultOptions(_tempRoot);
        configure?.Invoke(options);
        return TestHelpers.CreateProcessor(options);
    }

    private static byte[] TextBytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    // ---- Valid extraction ----

    [Fact]
    public async Task ExtractsAValidZip()
    {
        var zip = TestHelpers.CreateZip(
            ("src/Program.cs", "Console.WriteLine();"),
            ("src/App.csproj", "<Project />"));

        var upload = TestHelpers.CreateUpload("backend.zip", zip);
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(new[] { upload });

        Assert.Equal(ProcessingStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ArchiveCount);
        Assert.Equal(2, result.ExtractedFileCount);
        Assert.Equal(0, result.SkippedEntryCount);
        Assert.NotEmpty(result.JobId);

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task SkipsIgnoredEntriesAndReportsStacks()
    {
        var zip = TestHelpers.CreateZip(
            ("src/App.csproj", "<Project />"),
            ("src/Program.cs", "x"),
            ("src/bin/Debug/App.dll", "binary"));

        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("dotnet.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload });

        Assert.Equal(2, result.ExtractedFileCount);
        Assert.Equal(1, result.SkippedEntryCount);
        Assert.Contains(result.AggregateDetectedStacks, d => d.Name == ".NET / C#");
        // "<Project />"(11 bytes) + "x"(1 byte); the "binary" file under bin/ is skipped.
        Assert.Equal(12, result.TotalExtractedBytes);
    }

    // ---- Zip Slip / unsafe entries ----

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../../outside.txt")]
    [InlineData("folder/../../../outside.txt")]
    [InlineData("/absolute/file.txt")]
    [InlineData("C:\\outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    public async Task RejectsUnsafePaths(string entryName)
    {
        var zip = TestHelpers.CreateZip((entryName, "evil"));
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("evil.zip", zip);

        await Assert.ThrowsAsync<UnsafeArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        Assert.False(File.Exists(Path.Combine(_tempRoot, "outside.txt")));
        Assert.Empty(Directory.GetDirectories(_tempRoot));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsSymlinkEntries()
    {
        var zip = TestHelpers.CreateSymlinkZip();
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("symlink.zip", zip);

        await Assert.ThrowsAsync<UnsupportedArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsDuplicatePaths()
    {
        var zip = TestHelpers.CreateZip(("file.txt", "a"), ("file.txt", "b"));
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("dup.zip", zip);

        await Assert.ThrowsAsync<InvalidArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsCorruptZip()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("corrupt.zip", bytes);

        await Assert.ThrowsAsync<InvalidArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsEmptyArchive()
    {
        var zip = TestHelpers.CreateZip();
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("empty.zip", zip);

        await Assert.ThrowsAsync<InvalidArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task CleansJobDirectoryAfterFailure()
    {
        var zip = TestHelpers.CreateZip(("../outside.txt", "evil"));
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("evil.zip", zip);

        await Assert.ThrowsAsync<UnsafeArchiveException>(
            () => processor.ProcessAsync(new[] { upload }));

        Assert.Empty(Directory.GetDirectories(_tempRoot));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task HonorsCancellationToken()
    {
        var zip = TestHelpers.CreateZip(("file.txt", "x"));
        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("file.zip", zip);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => processor.ProcessAsync(new[] { upload }, cts.Token));

        Assert.Empty(Directory.GetDirectories(_tempRoot));

        upload.Stream.Dispose();
    }

    // ---- Limits ----

    [Fact]
    public async Task RejectsTooManyArchives()
    {
        var zip = TestHelpers.CreateZip(("a.txt", "x"));
        var processor = CreateProcessor(o => o.MaxArchiveCount = 1);
        var upload1 = TestHelpers.CreateUpload("a.zip", zip);
        var upload2 = TestHelpers.CreateUpload("b.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload1, upload2 }));

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    [Fact]
    public async Task RejectsTooManyEntries()
    {
        var zip = TestHelpers.CreateZip(("a.txt", "x"), ("b.txt", "y"));
        var processor = CreateProcessor(o => o.MaxEntryCount = 1);
        var upload = TestHelpers.CreateUpload("many.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsSingleFileOverLimit()
    {
        var zip = TestHelpers.CreateZip(("big.txt", new string('a', 100)));
        var processor = CreateProcessor(o => o.MaxSingleExtractedFileBytes = 10);
        var upload = TestHelpers.CreateUpload("big.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsAggregateBytesOverLimit()
    {
        var zip = TestHelpers.CreateZip(("a.txt", new string('a', 40)), ("b.txt", new string('b', 40)));
        var processor = CreateProcessor(o => o.MaxTotalExtractedBytes = 50);
        var upload = TestHelpers.CreateUpload("total.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task RejectsSuspiciousCompressionRatio()
    {
        var zip = TestHelpers.CreateZip(("blob.txt", new string('a', 100_000)));
        var processor = CreateProcessor(o => o.MaxCompressionRatio = 2);
        var upload = TestHelpers.CreateUpload("ratio.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload }));

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task CountsActualBytesAndExcludesSkippedFiles()
    {
        var zip = TestHelpers.CreateZip(
            ("Program.cs", new string('x', 100)),
            ("logo.png", new string('p', 500)));

        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("count.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload });

        Assert.Equal(100, result.TotalExtractedBytes);
        Assert.Equal(1, result.ExtractedFileCount);
        Assert.Equal(1, result.SkippedEntryCount);

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task DeletesPartiallyExtractedFileAfterLimitFailure()
    {
        var zip = TestHelpers.CreateZip(
            ("a.txt", new string('a', 40)),
            ("b.txt", new string('b', 40)));

        var processor = CreateProcessor(o =>
        {
            o.MaxTotalExtractedBytes = 50;
            o.DeleteJobDirectoryOnFailure = false;
        });
        var upload = TestHelpers.CreateUpload("partial.zip", zip);

        await Assert.ThrowsAsync<LimitExceededException>(
            () => processor.ProcessAsync(new[] { upload }));

        var jobDirs = Directory.GetDirectories(_tempRoot);
        Assert.Single(jobDirs);
        var archiveDir = Directory.GetDirectories(Path.Combine(jobDirs[0], "source")).Single();
        Assert.True(File.Exists(Path.Combine(archiveDir, "a.txt")));
        Assert.False(File.Exists(Path.Combine(archiveDir, "b.txt")));

        upload.Stream.Dispose();
    }

    // ---- Multiple archives ----

    [Fact]
    public async Task ExtractsMultipleArchivesIntoSeparateFolders()
    {
        var frontendZip = TestHelpers.CreateZip(("package.json", "{}"));
        var backendZip = TestHelpers.CreateZip(("Program.cs", "x"));

        var processor = CreateProcessor();
        var upload1 = TestHelpers.CreateUpload("frontend.zip", frontendZip);
        var upload2 = TestHelpers.CreateUpload("backend.zip", backendZip);

        var result = await processor.ProcessAsync(new[] { upload1, upload2 });

        Assert.Equal(2, result.ArchiveCount);
        Assert.Equal("frontend", result.ArchiveResults[0].SafeDirectoryName);
        Assert.Equal("backend", result.ArchiveResults[1].SafeDirectoryName);
        Assert.Equal(2, result.ExtractedFileCount);

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    [Fact]
    public async Task HandlesDuplicateArchiveNamesWithSuffixes()
    {
        var zip = TestHelpers.CreateZip(("Program.cs", "x"));
        var processor = CreateProcessor();
        var upload1 = TestHelpers.CreateUpload("frontend.zip", zip);
        var upload2 = TestHelpers.CreateUpload("frontend.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload1, upload2 });

        Assert.Equal("frontend", result.ArchiveResults[0].SafeDirectoryName);
        Assert.Equal("frontend-2", result.ArchiveResults[1].SafeDirectoryName);

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    [Fact]
    public async Task CleansEntireJobWhenSecondArchiveFails()
    {
        var validZip = TestHelpers.CreateZip(("a.txt", "x"));
        var corruptZip = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        var processor = CreateProcessor();
        var upload1 = TestHelpers.CreateUpload("good.zip", validZip);
        var upload2 = TestHelpers.CreateUpload("bad.zip", corruptZip);

        await Assert.ThrowsAsync<InvalidArchiveException>(
            () => processor.ProcessAsync(new[] { upload1, upload2 }));

        Assert.Empty(Directory.GetDirectories(_tempRoot));

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    [Fact]
    public async Task PreventsCollisionsBetweenArchives()
    {
        var zip1 = TestHelpers.CreateZip(("Program.cs", "one"));
        var zip2 = TestHelpers.CreateZip(("Program.cs", "two"));

        var processor = CreateProcessor();
        var upload1 = TestHelpers.CreateUpload("one.zip", zip1);
        var upload2 = TestHelpers.CreateUpload("two.zip", zip2);

        var result = await processor.ProcessAsync(new[] { upload1, upload2 });

        Assert.Equal(2, result.ExtractedFileCount);

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    // ---- Stack detection integration ----

    [Fact]
    public async Task DetectsDotNetFromManifest()
    {
        var zip = TestHelpers.CreateZip(
            ("src/App.csproj", "<Project />"),
            ("src/Program.cs", "x"),
            ("src/bin/Debug/App.dll", "bin"));

        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("valid-dotnet.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload });

        Assert.Contains(result.AggregateDetectedStacks, d => d.Name == ".NET / C#");
        Assert.Equal(2, result.ExtractedFileCount); // bin content skipped
        Assert.Equal(1, result.SkippedEntryCount);

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task DetectsNodeNextAndReact()
    {
        var zip = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
            ("src/app/page.tsx", "export default function Page() {}"),
            ("node_modules/pkg/index.js", "ignored"),
            ("public/logo.png", "ignored"));

        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("valid-next.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload });

        var names = result.AggregateDetectedStacks.Select(d => d.Name).ToList();
        Assert.Contains("Node.js", names);
        Assert.Contains("Next.js", names);
        Assert.Contains("React", names);
        Assert.Equal(2, result.ExtractedFileCount); // page.tsx + package.json

        upload.Stream.Dispose();
    }

    [Fact]
    public async Task AggregatesStacksAcrossArchives()
    {
        var frontendZip = TestHelpers.CreateZip(
            ("package.json", """{"dependencies":{"next":"14.0.0","react":"18.2.0"}}"""),
            ("src/app/page.tsx", "x"));
        var backendZip = TestHelpers.CreateZip(("App.csproj", "<Project />"), ("Program.cs", "x"));

        var processor = CreateProcessor();
        var upload1 = TestHelpers.CreateUpload("frontend.zip", frontendZip);
        var upload2 = TestHelpers.CreateUpload("backend.zip", backendZip);

        var result = await processor.ProcessAsync(new[] { upload1, upload2 });

        var names = result.AggregateDetectedStacks.Select(d => d.Name).ToList();
        Assert.Contains(".NET / C#", names);
        Assert.Contains("Node.js", names);
        Assert.Contains("Next.js", names);
        Assert.Contains("React", names);

        TestHelpers.DisposeUploads(new[] { upload1, upload2 });
    }

    // ---- File tree integration ----

    [Fact]
    public async Task ProducesFileTreeWithRelativePaths()
    {
        var zip = TestHelpers.CreateZip(
            ("backend/Backend.csproj", "<Project />"),
            ("backend/Program.cs", "x"),
            ("frontend/package.json", "{}"));

        var processor = CreateProcessor();
        var upload = TestHelpers.CreateUpload("multi.zip", zip);

        var result = await processor.ProcessAsync(new[] { upload });

        Assert.NotNull(result.FileTree);
        Assert.StartsWith("source/", result.TextFileTree);
        Assert.DoesNotContain(_tempRoot, result.TextFileTree);
        Assert.DoesNotContain(_tempRoot, System.Text.Json.JsonSerializer.Serialize(result.FileTree));

        upload.Stream.Dispose();
    }
}
