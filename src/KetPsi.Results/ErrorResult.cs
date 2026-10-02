using System.Diagnostics;

namespace KetPsi.Results;

/// <summary>
/// Represents a specific error that occurred during an operation.
/// Contains a structural category, a stable machine-readable code,
/// a human-readable message, and optional diagnostic metadata.
/// </summary>
public record ErrorResult
{
    /// <summary>Gets the structural category of the error.</summary>
    public ErrorType Type { get; init; }

    /// <summary>Gets the unique, domain-specific string identifier for the error.</summary>
    public string Code { get; init; }

    /// <summary>Gets the human-readable explanation of the error.</summary>
    public string Message { get; init; }

    /// <summary>Gets the optional raw data points associated with the error.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorResult"/> record.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">A unique, domain-specific string identifier.</param>
    /// <param name="message">A human-readable explanation of the error.</param>
    /// <param name="metadata">Optional raw data points associated with the error.</param>
    internal ErrorResult(ErrorType type, string code, string message, IReadOnlyDictionary<string, object?>? metadata)
    {
        Type = type;
        Code = code;
        Message = message;
        Metadata = metadata;
    }

    private ErrorResult() : this(ErrorType.Failure, string.Empty, string.Empty, new Dictionary<string, object?>())
    {

    }

    /// <summary>
    /// Creates a new instance of an error result without metadata.
    /// Useful when building a collection of multiple errors.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">A unique, domain-specific string identifier for the error.</param>
    /// <param name="message">A human-readable explanation of the error.</param>
    /// <returns>A newly created <see cref="ErrorResult"/>.</returns>
    public static ErrorResult Create(ErrorType type, string code, string message)
        => new(type, code, message, null);

    /// <summary>
    /// Creates a new instance of an error result with optional metadata.
    /// Useful when building a collection of multiple errors.
    /// </summary>
    /// <param name="type">The structural category of the error.</param>
    /// <param name="code">A unique, domain-specific string identifier for the error.</param>
    /// <param name="message">A human-readable explanation of the error.</param>
    /// <param name="metadata">Optional raw data points associated with the error.</param>
    /// <returns>A newly created <see cref="ErrorResult"/>.</returns>
    public static ErrorResult Create(ErrorType type, string code, string message, IReadOnlyDictionary<string, object?>? metadata)
        => new(type, code, message, metadata);

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";

    /// <summary>
    /// Fluent builder for constructing an <see cref="ErrorResult"/> with optional metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults when no mutators are applied:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="ErrorType"/> = <see cref="ErrorType.Failure"/></item>
    ///   <item><c>Code</c> = empty string</item>
    ///   <item><c>Message</c> = empty string</item>
    ///   <item><c>Metadata</c> = empty dictionary</item>
    /// </list>
    /// <para>
    /// Callers should set at least a stable <c>Code</c> and a human-readable <c>Message</c>
    /// before <see cref="Build"/> for any error that leaves the integration boundary.
    /// </para>
    /// </remarks>
    public class ErrorResultBuilder
    {
        ErrorType _errorType = ErrorType.Failure;
        string _code = string.Empty;
        string _message = string.Empty;
        readonly Dictionary<string, object?> _metadata = new();

        /// <summary>Sets the structural <see cref="ErrorType"/> (default: <see cref="ErrorType.Failure"/>).</summary>
        public ErrorResultBuilder WithErrorType(ErrorType errorType)
        {
            _errorType = errorType;
            return this;
        }
        /// <summary>Sets the stable machine-readable error code (default: empty).</summary>
        public ErrorResultBuilder WithCode(string code)
        {
            _code = code;
            return this;
        }

        /// <summary>Sets the human-readable message (default: empty).</summary>
        public ErrorResultBuilder WithMessage(string? message)
        {
            _message = message ?? string.Empty;
            return this;
        }

        /// <summary>Adds a metadata entry. Throws if <paramref name="key"/> already exists.</summary>
        public ErrorResultBuilder AddMetadata(string key, object? value)
        {
            _metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Builds an <see cref="ErrorResult"/> from the current state.
        /// Unset fields keep their defaults (<see cref="ErrorType.Failure"/>, empty code/message).
        /// </summary>
        internal ErrorResult Build()
        {
            Debug.Assert(!string.IsNullOrEmpty(_code));
            return new ErrorResult(_errorType, _code, _message, _metadata);
        }
    }

}
