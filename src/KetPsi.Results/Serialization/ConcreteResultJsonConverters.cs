using System.Text.Json;
using System.Text.Json.Serialization;

namespace KetPsi.Results.Serialization;

// Converters for the concrete result types. Each one serializes with the converter of its base result type
// and, on read, rejects JSON whose "status" does not match the requested type. They are public with a
// parameterless constructor so that [JsonConverter] attributes and the System.Text.Json source generator
// (which emits `new TConverter()` into the consuming assembly) can create them without reflection.

/// <summary>JSON converter for <see cref="SuccessResult"/>.</summary>
public sealed class SuccessResultJsonConverter : JsonConverter<SuccessResult>
{
    /// <inheritdoc />
    public override SuccessResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<SuccessResult>(ResultJsonConverter.Instance.Read(ref reader, typeof(Result), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SuccessResult value, JsonSerializerOptions options)
        => ResultJsonConverter.Instance.Write(writer, value, options);
}

/// <summary>JSON converter for <see cref="InProgressResult"/>.</summary>
public sealed class InProgressResultJsonConverter : JsonConverter<InProgressResult>
{
    /// <inheritdoc />
    public override InProgressResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<InProgressResult>(ResultJsonConverter.Instance.Read(ref reader, typeof(Result), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, InProgressResult value, JsonSerializerOptions options)
        => ResultJsonConverter.Instance.Write(writer, value, options);
}

/// <summary>JSON converter for <see cref="FailedResult"/>.</summary>
public sealed class FailedResultJsonConverter : JsonConverter<FailedResult>
{
    /// <inheritdoc />
    public override FailedResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<FailedResult>(ResultJsonConverter.Instance.Read(ref reader, typeof(Result), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FailedResult value, JsonSerializerOptions options)
        => ResultJsonConverter.Instance.Write(writer, value, options);
}

/// <summary>JSON converter for <see cref="SuccessResult{T}"/>.</summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public sealed class SuccessResultJsonConverter<T> : JsonConverter<SuccessResult<T>>
{
    private readonly ResultOfTJsonConverter<T> _inner = new();

    /// <inheritdoc />
    public override SuccessResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<SuccessResult<T>>(_inner.Read(ref reader, typeof(Result<T>), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SuccessResult<T> value, JsonSerializerOptions options)
        => _inner.Write(writer, value, options);
}

/// <summary>JSON converter for <see cref="InProgressResult{T}"/>.</summary>
/// <typeparam name="T">The type of the tracking value.</typeparam>
public sealed class InProgressResultJsonConverter<T> : JsonConverter<InProgressResult<T>>
{
    private readonly ResultOfTJsonConverter<T> _inner = new();

    /// <inheritdoc />
    public override InProgressResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<InProgressResult<T>>(_inner.Read(ref reader, typeof(Result<T>), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, InProgressResult<T> value, JsonSerializerOptions options)
        => _inner.Write(writer, value, options);
}

/// <summary>JSON converter for <see cref="FailedResult{T}"/>.</summary>
/// <typeparam name="T">The value type of the result.</typeparam>
public sealed class FailedResultJsonConverter<T> : JsonConverter<FailedResult<T>>
{
    private readonly ResultOfTJsonConverter<T> _inner = new();

    /// <inheritdoc />
    public override FailedResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ConcreteResult.As<FailedResult<T>>(_inner.Read(ref reader, typeof(Result<T>), options));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FailedResult<T> value, JsonSerializerOptions options)
        => _inner.Write(writer, value, options);
}

internal static class ConcreteResult
{
    public static TConcrete As<TConcrete>(Result result) where TConcrete : Result
        => result as TConcrete
           ?? throw new JsonException(
               $"Cannot deserialize {result.GetType().Name} as {typeof(TConcrete).Name}; the JSON status does not match.");
}
