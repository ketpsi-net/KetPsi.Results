using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace KetPsi.Results;

/// <summary>
/// Extension methods for inspecting and consuming <see cref="Result"/> / <see cref="Result{T}"/>.
/// Status checks, value extraction, error helpers, Match, and HTTP mapping live here.
/// Intentional design: the Result types themselves stay pure carriers of status + data/errors.
/// </summary>
public static class ResultExtensions
{
    // ───────────────────────────── Status checks ─────────────────────────────

    /// <summary>Returns <c>true</c> when the result status is <see cref="Status.Success"/>.</summary>
    public static bool IsSuccessful(this Result result) => result.Status == Status.Success;

    /// <summary>Returns <c>true</c> when the result status is <see cref="Status.InProgress"/>.</summary>
    public static bool IsInProgress(this Result result) => result.Status == Status.InProgress;

    /// <summary>Returns <c>true</c> when the result status is <see cref="Status.Failed"/>.</summary>
    public static bool IsFailed(this Result result) => result.Status == Status.Failed;

    // ───────────────────────────── Value / Error extraction ─────────────────────────────

    /// <summary>
    /// Tries to extract the value when the result is successful or in-progress.
    /// </summary>
    public static bool TryGetResult<T>(this Result<T> result, [NotNullWhen(true)] out T? value)
    {
        if (result is SuccessResult<T> success)
        {
            value = success.Value;
            return true;
        }

        if (result is InProgressResult<T> inProgress)
        {
            value = inProgress.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Returns the error list, or an empty list when the result is not failed.</summary>
    public static IReadOnlyList<ErrorResult> GetErrors(this Result result)
        => result is FailedResult failed ? failed.Errors : [];

    /// <summary>Returns the error list, or an empty list when the result is not failed.</summary>
    public static IReadOnlyList<ErrorResult> GetErrors<T>(this Result<T> result)
        => result is FailedResult<T> failed ? failed.Errors : [];

    /// <summary>Returns the first error, or <see langword="null"/> when the result is not failed.</summary>
    public static ErrorResult? FirstError(this Result result)
        => result is FailedResult { Errors.Count: > 0 } f ? f.Errors[0] : null;

    /// <summary>Returns the first error, or <see langword="null"/> when the result is not failed.</summary>
    public static ErrorResult? FirstError<T>(this Result<T> result)
        => result is FailedResult<T> { Errors.Count: > 0 } f ? f.Errors[0] : null;

    /// <summary>Formats all errors as a multi-line string ("code:message").</summary>
    public static string GetErrorAsString(this Result result) => FormatErrors(result.GetErrors());

    /// <summary>Formats all errors as a multi-line string ("code:message").</summary>
    public static string GetErrorAsString<T>(this Result<T> result) => FormatErrors(result.GetErrors());

    private static string FormatErrors(IReadOnlyList<ErrorResult> errors)
    {
        if (errors.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (var error in errors)
            sb.Append(error.Code).Append(':').Append(error.Message).AppendLine();
        return sb.ToString();
    }

    // ───────────────────────────── Match (safe consumption) ─────────────────────────────

    /// <summary>
    /// Exhaustively handles the three possible statuses of a non-generic result.
    /// </summary>
    public static TResult Match<TResult>(
        this Result result,
        Func<TResult> onSuccess,
        Func<IReadOnlyList<ErrorResult>, TResult> onFailure,
        Func<TResult>? onInProgress = null)
    {
        return result.Status switch
        {
            Status.Success => onSuccess(),
            Status.Failed => onFailure(result.GetErrors()),
            Status.InProgress => onInProgress is not null ? onInProgress() : onSuccess(),
            _ => throw new InvalidOperationException($"Unknown status: {result.Status}")
        };
    }

    /// <summary>
    /// Exhaustively handles the three possible statuses of a generic result.
    /// </summary>
    public static TResult Match<T, TResult>(
        this Result<T> result,
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<ErrorResult>, TResult> onFailure,
        Func<T, TResult>? onInProgress = null)
    {
        return result switch
        {
            SuccessResult<T> s => onSuccess(s.Value),
            FailedResult<T> f => onFailure(f.Errors),
            InProgressResult<T> i => onInProgress is not null ? onInProgress(i.Value) : onSuccess(i.Value),
            _ => throw new InvalidOperationException($"Unknown result type: {result.GetType().Name}")
        };
    }

    // ───────────────────────────── HTTP helpers ─────────────────────────────

    /// <summary>Maps an <see cref="ErrorType"/> to the corresponding HTTP status code.</summary>
    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Failure => 500,
        ErrorType.DependencyFailure => 503,
        ErrorType.Validation => 400,
        ErrorType.Unauthorized => 401,
        ErrorType.Forbidden => 403,
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        ErrorType.RuleViolation => 422,
        ErrorType.QuotaExceeded => 429,
        ErrorType.Canceled => 499,
        _ => 500
    };

    /// <summary>Maps a failed result to an HTTP status code using its first error.</summary>
    public static int ToStatusCode(this FailedResult result)
        => result.FirstError?.Type.ToStatusCode() ?? 500;

    /// <summary>Maps a failed generic result to an HTTP status code using its first error.</summary>
    public static int ToStatusCode<T>(this FailedResult<T> result)
        => result.FirstError?.Type.ToStatusCode() ?? 500;

    /// <summary>
    /// Builds a dictionary suitable for ProblemDetails / RFC 7807 responses.
    /// </summary>
    public static Dictionary<string, object?> ToProblemDetails(this FailedResult result, string? instance = null)
    {
        var first = result.FirstError;

        return new Dictionary<string, object?>
        {
            ["type"] = first?.Code ?? "about:blank",
            ["title"] = first?.Type.ToString() ?? "Error",
            ["status"] = result.ToStatusCode(),
            ["detail"] = first?.Message,
            ["instance"] = instance,
            ["errors"] = result.Errors.Select(e => new
            {
                e.Code,
                e.Message,
                e.Type,
                e.Metadata
            }).ToArray()
        };
    }

    /// <summary>
    /// Forwards errors into a non-generic <see cref="FailedResult"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The result is not failed (success or in-progress).
    /// </exception>
    public static FailedResult AsFailed(this Result result)
    {
        if (result is FailedResult failed)
            return failed;

        if (!result.IsFailed())
            throw new InvalidOperationException(
                $"AsFailed() requires a failed result; status was {result.Status}.");

        return Result.Failed(result.GetErrors());
    }

    /// <summary>
    /// Forwards errors into a <see cref="FailedResult{T}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The result is not failed (success or in-progress).
    /// </exception>
    public static FailedResult<T> AsFailed<T>(this Result result)
    {
        if (!result.IsFailed())
            throw new InvalidOperationException(
                $"AsFailed() requires a failed result; status was {result.Status}.");

        return Result.Failed<T>(result.GetErrors());
    }
}
