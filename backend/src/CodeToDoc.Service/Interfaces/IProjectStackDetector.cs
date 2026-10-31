using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface IProjectStackDetector
{
    void Reset();

    void ScanEntry(string relativePath);

    void ScanPackageJson(string relativePath, string content);

    IReadOnlyList<DetectedStack> Detections { get; }

    IReadOnlyList<string> Warnings { get; }

    long ElapsedMilliseconds { get; }
}
