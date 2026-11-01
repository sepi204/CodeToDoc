using System.IO.Compression;
using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Tests;

internal static class TestHelpers
{
    public static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "codetodoc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    public static PhaseOneProcessingOptions DefaultOptions(string tempRoot) => new()
    {
        TempRootPath = tempRoot,
    };

    public static PhaseOneProcessor CreateProcessor(PhaseOneProcessingOptions options)
    {
        var workspaceManager = new JobWorkspaceManager(
            Options.Create(options),
            NullLogger<JobWorkspaceManager>.Instance);

        var filter = new ArchiveEntryFilter();
        var stackDetector = new ProjectStackDetector();
        var extractor = new SecureArchiveExtractor(
            Options.Create(options),
            filter,
            stackDetector,
            NullLogger<SecureArchiveExtractor>.Instance);

        var fileTreeBuilder = new FileTreeBuilder();
        var rootDiscoverer = new ProjectRootDiscoverer();

        return new PhaseOneProcessor(
            Options.Create(options),
            workspaceManager,
            extractor,
            fileTreeBuilder,
            rootDiscoverer,
            NullLogger<PhaseOneProcessor>.Instance);
    }

    public static PhaseOneArchiveUpload CreateUpload(string fileName, byte[] content) =>
        new()
        {
            FileName = fileName,
            Length = content.Length,
            Stream = new MemoryStream(content),
        };

    public static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Creates a ZIP archive that contains a single symbolic-link entry using raw bytes,
    /// because <see cref="ZipArchive"/> does not expose a way to create symlink entries.
    /// </summary>
    public static byte[] CreateSymlinkZip()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        const string name = "link";
        var nameBytes = Encoding.UTF8.GetBytes(name);

        // Local file header
        writer.Write(0x04034b50u);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0x21);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write(nameBytes);

        // Central directory file header
        writer.Write(0x02014b50u);
        writer.Write((ushort)0x031E);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0x21);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0xA1FF0000); // Unix mode: S_IFLNK (0xA000) | 0o777
        writer.Write(0u);
        writer.Write(nameBytes);

        // End of central directory
        writer.Write(0x06054b50u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(50u);
        writer.Write(34u);
        writer.Write((ushort)0);

        writer.Flush();
        return stream.ToArray();
    }

    public static void DisposeUploads(IEnumerable<PhaseOneArchiveUpload> uploads)
    {
        foreach (var upload in uploads)
        {
            upload.Stream.Dispose();
        }
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
