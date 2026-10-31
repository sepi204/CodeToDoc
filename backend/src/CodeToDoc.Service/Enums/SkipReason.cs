namespace CodeToDoc.Service.Enums;

public enum SkipReason
{
    IgnoredDirectory = 0,
    IgnoredExtension = 1,
    IgnoredFileName = 2,
    UnsafePath = 3,
    LimitExceeded = 4,
    UnsupportedEntry = 5
}
