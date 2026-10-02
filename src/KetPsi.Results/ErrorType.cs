namespace KetPsi.Results;

/// <summary>
/// Categorizes the structural nature of an error to dictate system control flow,
/// HTTP status mapping, and retry policies.
/// </summary>
public enum ErrorType : byte
{
    /// <summary>Unhandled internal bug or infrastructure failure (HTTP 500). Do not retry.</summary>
    Failure = 1,

    /// <summary>Downstream service or database is unavailable (HTTP 503). Safe to retry.</summary>
    DependencyFailure = 2,

    /// <summary>Syntactic input error or malformed data (HTTP 400).</summary>
    Validation = 3,

    /// <summary>Identity is unknown or token has expired (HTTP 401).</summary>
    Unauthorized = 4,

    /// <summary>Identity is known, but lacks specific permissions (HTTP 403).</summary>
    Forbidden = 5,

    /// <summary>The target resource does not exist (HTTP 404).</summary>
    NotFound = 6,

    /// <summary>Domain state collision, e.g., duplicates or concurrency exceptions (HTTP 409).</summary>
    Conflict = 7,

    /// <summary>A semantic business rule prevented the action (HTTP 422).</summary>
    RuleViolation = 8,

    /// <summary>Domain limits or API rate limits have been exceeded (HTTP 429).</summary>
    QuotaExceeded = 9,

    /// <summary>The operation was aborted via a cancellation token (HTTP 499).</summary>
    Canceled = 10
}
