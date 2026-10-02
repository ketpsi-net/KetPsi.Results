namespace KetPsi.Results;

/// <summary>
/// Represents the base, non-generic outcome of an operation.
/// Provides factory methods for success, failure, in-progress, exception conversion,
/// and error combination.
/// </summary>
public abstract partial record Result
{
    private const string InternalErrorCode = "server.internal_error";

    /// <summary>Gets the operational status of the result.</summary>
    public Status Status { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    /// <param name="status">The operational status.</param>
    private protected Result(Status status) => Status = status;

    // ───────────────────────────── Success / InProgress ─────────────────────────────

    /// <summary>Creates a successful non-generic result.</summary>
    /// <returns>A non-generic <see cref="SuccessResult"/>.</returns>
    public static SuccessResult Success() => new();

    /// <summary>Creates a successful generic result containing the specified value.</summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    /// <param name="value">The data payload to return.</param>
    /// <returns>A generic <see cref="SuccessResult{T}"/>.</returns>
    public static SuccessResult<T> Success<T>(T value) => new(value);

    /// <summary>Creates an in-progress non-generic result.</summary>
    /// <returns>A non-generic <see cref="InProgressResult"/>.</returns>
    public static InProgressResult InProgress() => new();

    /// <summary>Creates an in-progress generic result containing a tracking value.</summary>
    /// <typeparam name="T">The type of the tracking value.</typeparam>
    /// <param name="value">The tracking identifier.</param>
    /// <returns>A generic <see cref="InProgressResult{T}"/>.</returns>
    public static InProgressResult<T> InProgress<T>(T value) => new(value);

    // ───────────────────────────── Fail factories ─────────────────────────────

    /// <summary>
    /// Creates a failed non-generic result using a generic internal server error code
    /// and a specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(string message)
        => new([ErrorResult.Create(ErrorType.Failure, InternalErrorCode, message)]);

    /// <summary>
    /// Creates a failed generic result using a generic internal server error code
    /// and a specified message.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="message">The error message.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(string message)
        => new([ErrorResult.Create(ErrorType.Failure, InternalErrorCode, message)]);

    /// <summary>
    /// Creates a failed non-generic result with an explicit error type and code.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(ErrorType type, string code, string message)
        => new([ErrorResult.Create(type, code, message)]);

    /// <summary>
    /// Creates a failed non-generic result with an explicit error type and code.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="metadata">The error metadata.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(ErrorType type, string code, string message, Dictionary<string, object?> metadata)
        => new([ErrorResult.Create(type, code, message, metadata)]);

    /// <summary>
    /// Creates a non-generic failed result by configuring a single <see cref="ErrorResult"/>
    /// through an <see cref="ErrorResult.ErrorResultBuilder"/>.
    /// </summary>
    /// <param name="errorBuilder">
    /// Callback that receives a new builder and returns it after configuration
    /// (e.g. <c>err => err.WithCode("…").WithMessage("…")</c>).
    /// </param>
    /// <returns>A <see cref="FailedResult"/> containing one error.</returns>
    /// <remarks>
    /// <para>
    /// The builder starts with defaults:
    /// <see cref="ErrorType.Failure"/>, empty code, empty message, and no metadata.
    /// Only properties set in <paramref name="errorBuilder"/> are changed.
    /// </para>
    /// <para>
    /// Prefer domain factories (<c>Result.Authentication</c>, <c>Result.Resource</c>, etc.)
    /// when a standard code and type already exist; use this overload for integration-specific
    /// or one-off errors that need custom codes and metadata.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// return Result.Failed(err => err
    ///     .WithErrorType(ErrorType.Failure)
    ///     .WithCode("integration.keycloak.invalid_grant")
    ///     .WithMessage(description)
    ///     .AddMetadata("status_code", (int)response.StatusCode));
    /// </code>
    /// </example>
    public static FailedResult Failed(Func<ErrorResult.ErrorResultBuilder, ErrorResult.ErrorResultBuilder> errorBuilder)
        => new([errorBuilder(new ErrorResult.ErrorResultBuilder()).Build()]);

    /// <inheritdoc cref="Failed(Func{ErrorResult.ErrorResultBuilder, ErrorResult.ErrorResultBuilder})"/>
    public static FailedResult<T> Failed<T>(
        Func<ErrorResult.ErrorResultBuilder, ErrorResult.ErrorResultBuilder> errorBuilder)
        => new([errorBuilder(new ErrorResult.ErrorResultBuilder()).Build()]);

    /// <summary>
    /// Creates a failed generic result with an explicit error type and code.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(ErrorType type, string code, string message)
        => new([ErrorResult.Create(type, code, message)]);

    /// <summary>
    /// Creates a failed generic result with an explicit error type and code.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="metadata">The error metadata.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(ErrorType type, string code, string message, Dictionary<string, object?> metadata)
        => new([ErrorResult.Create(type, code, message, metadata)]);

    /// <summary>
    /// Creates a failed non-generic result using an interpolated string handler
    /// to dynamically build the message and capture metadata.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The interpolated string handler containing the message and metadata.</param>
    /// <returns>A non-generic <see cref="FailedResult"/> with structured error metadata.</returns>
    public static FailedResult Failed(ErrorType type, string code, ref ResultErrorMessageHandler message)
        => new([new ErrorResult(type, code, message.GetFormattedText(), message.Metadata)]);

    /// <summary>
    /// Creates a failed generic result using an interpolated string handler
    /// to dynamically build the message and capture metadata.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The interpolated string handler containing the message and metadata.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/> with structured error metadata.</returns>
    public static FailedResult<T> Failed<T>(ErrorType type, string code, ref ResultErrorMessageHandler message)
        => new([new ErrorResult(type, code, message.GetFormattedText(), message.Metadata)]);

    /// <summary>Creates a failed non-generic result from a single error instance.</summary>
    /// <param name="error">The error result to wrap.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(ErrorResult error) => new([error]);

    /// <summary>Creates a failed non-generic result from a collection of error instances.</summary>
    /// <param name="errors">The collection of errors.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(IReadOnlyList<ErrorResult> errors) => new(errors);

    /// <summary>Creates a failed non-generic result from a variable number of error instances.</summary>
    /// <param name="errors">The errors.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult Failed(params ErrorResult[] errors) => new(errors);

    /// <summary>Creates a failed generic result from a single error instance.</summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="error">The error result to wrap.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(ErrorResult error) => new([error]);

    /// <summary>Creates a failed generic result from a collection of error instances.</summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="errors">The collection of errors.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(IReadOnlyList<ErrorResult> errors) => new(errors);

    /// <summary>Creates a failed generic result from a variable number of error instances.</summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="errors">The errors.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> Failed<T>(params ErrorResult[] errors) => new(errors);

    // ───────────────────────────── FromException ─────────────────────────────

    /// <summary>
    /// Creates a failed result from an exception.
    /// <see cref="OperationCanceledException"/> is automatically mapped to
    /// <see cref="ErrorType.Canceled"/>.
    /// </summary>
    /// <param name="exception">The exception to convert.</param>
    /// <param name="type">The error type to use when the exception is not a cancellation.</param>
    /// <param name="code">Optional explicit error code. Defaults to a system code.</param>
    /// <returns>A non-generic <see cref="FailedResult"/>.</returns>
    public static FailedResult FromException(Exception exception, ErrorType type = ErrorType.Failure, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errorCode = code ?? (exception is OperationCanceledException
            ? "system.execution_canceled"
            : InternalErrorCode);

        var errorType = exception is OperationCanceledException
            ? ErrorType.Canceled
            : type;

        return new FailedResult([
            ErrorResult.Create(errorType, errorCode, exception.Message)
        ]);
    }

    /// <summary>
    /// Creates a failed generic result from an exception.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="exception">The exception to convert.</param>
    /// <param name="type">The error type to use when the exception is not a cancellation.</param>
    /// <param name="code">Optional explicit error code.</param>
    /// <returns>A generic <see cref="FailedResult{T}"/>.</returns>
    public static FailedResult<T> FromException<T>(Exception exception, ErrorType type = ErrorType.Failure, string? code = null)
        => new(FromException(exception, type, code).Errors);


    // ───────────────────────────── Combine ─────────────────────────────

    /// <summary>
    /// Combines multiple results. If any are failed, returns a single failed result
    /// containing the union of all errors; otherwise returns success.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A combined <see cref="Result"/>.</returns>
    public static Result Combine(params Result[] results)
    {
        if (results is null || results.Length == 0)
            return Success();

        List<ErrorResult>? errors = null;

        foreach (var result in results)
        {
            if (result is FailedResult failed)
            {
                errors ??= new List<ErrorResult>();
                errors.AddRange(failed.Errors);
            }
        }

        return errors is { Count: > 0 } ? Failed(errors) : Success();
    }
}

/// <summary>
/// Represents the generic outcome of an operation that includes a data payload.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public abstract record Result<T> : Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class.
    /// </summary>
    /// <param name="status">The operational status.</param>
    private protected Result(Status status) : base(status) { }

    /// <summary>
    /// Implicitly converts a non-generic <see cref="FailedResult"/> into a generic <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="result">The non-generic failed result to convert.</param>
    public static implicit operator Result<T>(FailedResult result) => new FailedResult<T>(result.Errors);





    /// <summary>
    /// Implicitly converts a raw value into a generic <see cref="SuccessResult{T}"/>.
    /// </summary>
    /// <param name="value">The value to wrap in a success result.</param>
    public static implicit operator Result<T>(T value) => new SuccessResult<T>(value);
}

/// <summary>
/// Represents a successful generic outcome containing a value.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public record SuccessResult<T> : Result<T>
{
    /// <summary>Gets the underlying result value.</summary>
    public T Value { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SuccessResult{T}"/> record.
    /// </summary>
    /// <param name="value">The success value.</param>
    internal SuccessResult(T value) : base(Status.Success) => Value = value;
}

/// <summary>
/// Represents a generic outcome that is currently in progress.
/// </summary>
/// <typeparam name="T">The type of the tracking value.</typeparam>
public record InProgressResult<T> : Result<T>
{
    /// <summary>Gets the tracking token or in-progress state value.</summary>
    public T Value { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InProgressResult{T}"/> record.
    /// </summary>
    /// <param name="value">The tracking value.</param>
    internal InProgressResult(T value) : base(Status.InProgress) => Value = value;
}

/// <summary>
/// Represents a failed generic outcome containing one or more errors.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public record FailedResult<T> : Result<T>
{
    /// <summary>Gets the collection of errors that caused the failure.</summary>
    public IReadOnlyList<ErrorResult> Errors { get; init; }

    /// <summary>Gets the first error, or <see langword="null"/> if the list is empty.</summary>
    public ErrorResult? FirstError => Errors.Count > 0 ? Errors[0] : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedResult{T}"/> record.
    /// </summary>
    /// <param name="errors">The collection of errors.</param>
    internal FailedResult(IReadOnlyList<ErrorResult> errors) : base(Status.Failed) => Errors = errors;
}

/// <summary>
/// Represents a successful non-generic outcome.
/// </summary>
public record SuccessResult : Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuccessResult"/> record.
    /// </summary>
    internal SuccessResult() : base(Status.Success) { }
}

/// <summary>
/// Represents a non-generic outcome that is currently in progress.
/// </summary>
public record InProgressResult : Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InProgressResult"/> record.
    /// </summary>
    internal InProgressResult() : base(Status.InProgress) { }
}

/// <summary>
/// Represents a failed non-generic outcome containing one or more errors.
/// </summary>
public record FailedResult : Result
{
    /// <summary>Gets the collection of errors that caused the failure.</summary>
    public IReadOnlyList<ErrorResult> Errors { get; init; }

    /// <summary>Gets the first error, or <see langword="null"/> if the list is empty.</summary>
    public ErrorResult? FirstError => Errors.Count > 0 ? Errors[0] : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedResult"/> record.
    /// </summary>
    /// <param name="errors">The collection of errors.</param>
    internal FailedResult(IReadOnlyList<ErrorResult> errors) : base(Status.Failed) => Errors = errors;

    /// <summary>
    /// Initializes a new instance from a single error.
    /// </summary>
    /// <param name="error">The error.</param>
    internal FailedResult(ErrorResult error) : this([error]) { }

    /// <summary>
    /// Initializes a new instance from type, code and message.
    /// </summary>
    internal FailedResult(ErrorType type, string code, string message)
        : this(ErrorResult.Create(type, code, message)) { }

    /// <summary>
    /// Initializes a new instance from type, code, message and metadata.
    /// </summary>
    internal FailedResult(ErrorType type, string code, string message, IReadOnlyDictionary<string, object?>? metadata)
        : this(ErrorResult.Create(type, code, message, metadata)) { }

}
