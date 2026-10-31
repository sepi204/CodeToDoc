namespace CodeToDoc.Service.Enums;

public enum SynthesisStatus
{
    Complete = 0,
    Partial = 1,
    Blocked = 2,
    NotApplicable = 3,
    Failed = 4
}

public enum CertaintyLevel
{
    Explicit = 0,
    Derived = 1,
    Heuristic = 2,
    Unknown = 3
}

public enum LlmErrorCategory
{
    Timeout = 0,
    Cancellation = 1,
    RateLimited = 2,
    TransientProviderFailure = 3,
    AuthenticationFailure = 4,
    InvalidRequest = 5,
    InvalidStructuredResponse = 6,
    ContentRejected = 7,
    ContextLimitExceeded = 8,
    UnknownProviderFailure = 9
}

public enum TokenCountKind
{
    ProviderReported = 0,
    Estimated = 1
}
