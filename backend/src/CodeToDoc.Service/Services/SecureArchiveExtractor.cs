using System.IO.Compression;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Exceptions;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class SecureArchiveExtractor : IArchiveExtractor
{
    private readonly PhaseOneProcessingOptions _options;
    private readonly IArchiveEntryFilter _filter;
    private readonly IProjectStackDetector _stackDetector;
    private readonly ILogger<SecureArchiveExtractor> _logger;

    private HashSet<string> _seenDestinations = null!;

    public SecureArchiveExtractor(
        IOptions<PhaseOneProcessingOptions> options,
        IArchiveEntryFilter filter,
        IProjectStackDetector stackDetector,
        ILogger<SecureArchiveExtractor> logger)
    {
        _options = options.Value;
        _filter = filter;
        _stackDetector = stackDetector;
        _logger = logger;
    }

    public async Task<ArchiveExtractionResult> ExtractAsync(
        PhaseOneArchiveUpload archive,
        string sourceDirectory,
        string safeDirectoryName,
        ExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ArchiveExtractionResult { OriginalFileName = archive.FileName };
        var destinationDirectory = Path.Combine(sourceDirectory, safeDirectoryName);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        _seenDestinations = new HashSet<string>(pathComparison);
        _stackDetector.Reset();

        Directory.CreateDirectory(destinationDirectory);

        using var zip = OpenArchive(archive);

        try
        {
            var entries = zip.Entries;
            result.TotalEntries = entries.Count;
            context.TotalArchiveEntries += entries.Count;

            if (entries.Count == 0)
            {
                throw new InvalidArchiveException($"Archive '{archive.FileName}' contains no entries.");
            }

            if (entries.Count > _options.MaxEntryCount)
            {
                throw new LimitExceededException(
                    $"Archive '{archive.FileName}' contains {entries.Count} entries, exceeding the limit of {_options.MaxEntryCount}.");
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessEntryAsync(entry, safeDirectoryName, destinationDirectory, context, result, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            throw new InvalidArchiveException($"File '{archive.FileName}' is not a valid ZIP archive.", ex);
        }

        result.DetectedStacks = _stackDetector.Detections.ToList();
        result.Warnings.AddRange(_stackDetector.Warnings);
        result.StackDetectionMs = _stackDetector.ElapsedMilliseconds;

        return result;
    }

    private static ZipArchive OpenArchive(PhaseOneArchiveUpload archive)
    {
        try
        {
            return new ZipArchive(archive.Stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex)
        {
            // Any failure to open the stream as a ZIP archive (truncated, corrupt, short,
            // unseekable, etc.) is reported as an invalid archive.
            throw new InvalidArchiveException($"File '{archive.FileName}' is not a valid ZIP archive.", ex);
        }
    }

    private async Task ProcessEntryAsync(
        ZipArchiveEntry entry,
        string safeDirectoryName,
        string destinationDirectory,
        ExtractionContext context,
        ArchiveExtractionResult result,
        CancellationToken cancellationToken)
    {
        var rawName = entry.FullName;
        var normalized = rawName.Replace('\\', '/');

        RejectUnsafeEntry(entry, normalized);

        var isDirectory = normalized.EndsWith('/');
        var trimmed = normalized.TrimEnd('/');

        if (trimmed.Length == 0)
        {
            // Root directory marker; nothing to write.
            return;
        }

        if (trimmed.Length > _options.MaxPathLength)
        {
            throw new LimitExceededException(
                $"Archive entry path exceeds the maximum length of {_options.MaxPathLength}.");
        }

        var decision = _filter.Evaluate(trimmed, isDirectory);
        if (decision.ShouldSkip)
        {
            result.SkippedEntryCount++;
            context.RecordSkip(decision.Reason!.Value);
            return;
        }

        var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, normalized));
        EnsureContained(destinationDirectory, destinationPath);

        var relativePath = $"{safeDirectoryName}/{trimmed}";

        if (isDirectory)
        {
            EnsureUniqueDestination(destinationPath);
            Directory.CreateDirectory(destinationPath);
            return;
        }

        // File entry.
        _stackDetector.ScanEntry(relativePath);

        var written = await CopyEntryAsync(entry, destinationPath, relativePath, context, cancellationToken);

        result.ExtractedFileCount++;
        result.ExtractedBytes += written;
        context.TotalExtractedFileCount++;
        context.TotalExtractedBytes += written;

        if (Path.GetFileName(relativePath).Equals("package.json", StringComparison.OrdinalIgnoreCase))
        {
            var content = ReadTextBounded(destinationPath, 1_000_000);
            _stackDetector.ScanPackageJson(relativePath, content);
        }
    }

    private async Task<long> CopyEntryAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        string relativePath,
        ExtractionContext context,
        CancellationToken cancellationToken)
    {
        EnsureUniqueDestination(destinationPath);

        var uncompressedLength = entry.Length;
        var compressedLength = entry.CompressedLength;

        if (uncompressedLength >= 0)
        {
            var ratio = uncompressedLength / Math.Max(compressedLength, 1L);
            if (ratio > _options.MaxCompressionRatio)
            {
                _logger.LogWarning("Rejected entry {RelativePath} for exceeding the compression ratio.", relativePath);
                throw new LimitExceededException(
                    $"Entry '{relativePath}' exceeds the maximum compression ratio of {_options.MaxCompressionRatio}.");
            }

            if (uncompressedLength > _options.MaxSingleExtractedFileBytes)
            {
                throw new LimitExceededException(
                    $"Entry '{relativePath}' exceeds the single-file size limit of {_options.MaxSingleExtractedFileBytes} bytes.");
            }
        }

        if (context.TotalExtractedFileCount + 1 > _options.MaxExtractedFileCount)
        {
            throw new LimitExceededException(
                $"The maximum extracted file count of {_options.MaxExtractedFileCount} has been exceeded.");
        }

        var parentDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(parentDirectory);

        long written = 0;
        try
        {
            await using var input = entry.Open();
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                written += read;

                if (written > _options.MaxSingleExtractedFileBytes)
                {
                    throw new LimitExceededException(
                        $"Entry '{relativePath}' exceeds the single-file size limit of {_options.MaxSingleExtractedFileBytes} bytes.");
                }

                if (context.TotalExtractedBytes + written > _options.MaxTotalExtractedBytes)
                {
                    throw new LimitExceededException(
                        $"The maximum total extracted size of {_options.MaxTotalExtractedBytes} bytes has been exceeded.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }

        return written;
    }

    private void EnsureUniqueDestination(string destinationPath)
    {
        if (!_seenDestinations.Add(destinationPath))
        {
            throw new InvalidArchiveException("Archive contains duplicate entry paths.");
        }
    }

    private void RejectUnsafeEntry(ZipArchiveEntry entry, string normalized)
    {
        var unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
        if ((unixMode & 0xF000) == 0xA000) // S_IFLNK (symbolic link)
        {
            _logger.LogWarning("Rejected symbolic-link entry {EntryName}.", entry.FullName);
            throw new UnsupportedArchiveException("Archive contains a symbolic link, which is not supported.");
        }

        if (normalized.IndexOf('\0') >= 0)
        {
            throw new UnsafeArchiveException("Archive entry path contains a null byte.");
        }

        if (normalized.StartsWith('/'))
        {
            throw new UnsafeArchiveException("Archive entry uses an absolute path.");
        }

        if (normalized.StartsWith("//"))
        {
            throw new UnsafeArchiveException("Archive entry uses a UNC path.");
        }

        var trimmed = normalized.TrimEnd('/');
        if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
        {
            throw new UnsafeArchiveException("Archive entry uses a drive-qualified path.");
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new UnsafeArchiveException("Archive entry contains a path-traversal segment.");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (segments.Any(segment => segment.IndexOfAny(invalidChars) >= 0))
        {
            throw new UnsafeArchiveException("Archive entry contains invalid path characters.");
        }
    }

    private static void EnsureContained(string rootDirectory, string destinationPath)
    {
        var root = Path.GetFullPath(rootDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!destinationPath.StartsWith(root, comparison))
        {
            throw new UnsafeArchiveException("Archive entry resolves outside the extraction directory.");
        }
    }

    private static string ReadTextBounded(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        var buffer = new char[Math.Min(maxBytes, 8192)];
        var builder = new System.Text.StringBuilder();
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            builder.Append(buffer, 0, read);
            if (builder.Length >= maxBytes)
            {
                break;
            }
        }

        return builder.ToString();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort; partial files are cleaned with the job directory.
        }
    }
}
