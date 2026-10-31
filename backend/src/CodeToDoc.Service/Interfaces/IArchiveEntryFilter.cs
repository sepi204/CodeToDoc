using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IArchiveEntryFilter
{
    ArchiveFilterDecision Evaluate(string entryName, bool isDirectory);
}
