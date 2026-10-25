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
    /// <remarks>Takes ownership of <paramref name="metadata"/> without copying; use <see cref="Create(ErrorType, string, string, IReadOnlyDictionary{string, object?}?)"/> for caller-owned dictionaries.</remarks>
    internal ErrorResult(ErrorType type, string code, string message, IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(code);

        Type = type;
        Code = code;
        Message = message ?? string.Empty;
        Metadata = metadata is { Count: > 0 } ? metadata : null;
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
    /// <param name="metadata">
    /// Optional raw data points associated with the error. The entries are copied, so later changes
    /// to the caller's dictionary do not affect the error. An empty dictionary is stored as <see langword="null"/>.
    /// </param>
    /// <returns>A newly created <see cref="ErrorResult"/>.</returns>
    public static ErrorResult Create(ErrorType type, string code, string message, IReadOnlyDictionary<string, object?>? metadata)
        => new(type, code, message, metadata is { Count: > 0 } ? new Dictionary<string, object?>(metadata) : null);

    /// <summary>
    /// Value equality over type, code, message and metadata entries
    /// (metadata values are compared with <see cref="object.Equals(object, object)"/>).
    /// </summary>
    public virtual bool Equals(ErrorResult? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || EqualityContract != other.EqualityContract) return false;

        return Type == other.Type
               && string.Equals(Code, other.Code, StringComparison.Ordinal)
               && string.Equals(Message, other.Message, StringComparison.Ordinal)
               && MetadataEquals(Metadata, other.Metadata);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Metadata is excluded: equal errors must hash equally, and order-independent
        // hashing of arbitrary values is not worth the cost here.
        return HashCode.Combine(Type, Code, Message, Metadata?.Count ?? 0);
    }

    private static bool MetadataEquals(IReadOnlyDictionary<string, object?>? left, IReadOnlyDictionary<string, object?>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if ((left?.Count ?? 0) != (right?.Count ?? 0)) return false;
        if (left is null || right is null) return true; // both empty

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
                return false;
        }

        return true;
    }

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
    ///   <item><c>Metadata</c> = <see langword="null"/> (no entries)</item>
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
        Dictionary<string, object?>? _metadata;

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

        /// <summary>Adds or replaces a metadata entry.</summary>
        public ErrorResultBuilder AddMetadata(string key, object? value)
        {
            _metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            _metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Builds an <see cref="ErrorResult"/> from the current state.
        /// Unset fields keep their defaults (<see cref="ErrorType.Failure"/>, empty code/message).
        /// </summary>
        internal ErrorResult Build() => new(_errorType, _code, _message, _metadata);
    }

}
