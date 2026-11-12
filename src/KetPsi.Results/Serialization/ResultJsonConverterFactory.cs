using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KetPsi.Results.Serialization;

/// <summary>
/// <see cref="JsonConverterFactory"/> for <see cref="Result"/>, <see cref="Result{T}"/>,
/// their concrete variants, and <see cref="ErrorResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// Wire shape (camelCase when that naming policy is configured):
/// <code>
/// { "status": "Success"|"Failed"|"InProgress", "value": ..., "errors": [ ... ] }
/// </code>
/// Nested <c>value</c> payloads are resolved through the options' <see cref="JsonTypeInfo"/> resolver,
/// so source-generated contexts are honored.
/// </para>
/// <para>
/// Every result type also carries a <see cref="JsonConverterAttribute"/>, so registering this factory is
/// optional. On .NET 11 the generic result types use open generic converters
/// (e.g. <see cref="ResultOfTJsonConverter{T}"/>) that System.Text.Json and its source generator instantiate
/// statically, which is Native AOT safe for every <c>T</c>. On .NET 9 and 10 this factory builds the generic
/// converters with <see cref="Type.MakeGenericType"/>; under Native AOT, where that is not available,
/// register each payload type with
/// <see cref="ResultJsonSerializerOptionsExtensions.AddKetPsiResultConverters{T}(JsonSerializerOptions)"/>.
/// </para>
/// </remarks>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert == typeof(Result)
            || typeToConvert == typeof(SuccessResult)
            || typeToConvert == typeof(InProgressResult)
            || typeToConvert == typeof(FailedResult)
            || typeToConvert == typeof(ErrorResult))
            return true;

#if NET11_0_OR_GREATER
        // Generic results are handled by the open generic [JsonConverter] attributes on the types themselves.
        return false;
#else
        return IsGenericResult(typeToConvert);
#endif
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(ErrorResult))
            return new ErrorResultJsonConverter();
        if (typeToConvert == typeof(Result))
            return new ResultJsonConverter();
        if (typeToConvert == typeof(SuccessResult))
            return new SuccessResultJsonConverter();
        if (typeToConvert == typeof(InProgressResult))
            return new InProgressResultJsonConverter();
        if (typeToConvert == typeof(FailedResult))
            return new FailedResultJsonConverter();

#if NET11_0_OR_GREATER
        throw new ArgumentException($"{typeToConvert} is not a KetPsi result type.", nameof(typeToConvert));
#else
        if (!IsGenericResult(typeToConvert))
            throw new ArgumentException($"{typeToConvert} is not a KetPsi result type.", nameof(typeToConvert));

        var definition = typeToConvert.GetGenericTypeDefinition();
        var valueType = typeToConvert.GetGenericArguments()[0];

        if (!RuntimeFeature.IsDynamicCodeSupported)
            throw new NotSupportedException(
                $"Cannot create a JSON converter for {typeToConvert} under Native AOT on this target framework. " +
                $"Call options.AddKetPsiResultConverters<{valueType.Name}>() for each result payload type, or target net11.0.");

        // Each typeof(...) stays inline so the trim analyzer can see which converter's constructor is needed.
        var converterType =
            definition == typeof(Result<>) ? typeof(ResultOfTJsonConverter<>).MakeGenericType(valueType)
            : definition == typeof(SuccessResult<>) ? typeof(SuccessResultJsonConverter<>).MakeGenericType(valueType)
            : definition == typeof(InProgressResult<>) ? typeof(InProgressResultJsonConverter<>).MakeGenericType(valueType)
            : typeof(FailedResultJsonConverter<>).MakeGenericType(valueType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
#endif
    }

#if !NET11_0_OR_GREATER
    private static bool IsGenericResult(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        var definition = typeToConvert.GetGenericTypeDefinition();
        return definition == typeof(Result<>)
               || definition == typeof(SuccessResult<>)
               || definition == typeof(InProgressResult<>)
               || definition == typeof(FailedResult<>);
    }
#endif
}

/// <summary>
/// Converter for <see cref="ErrorResult"/> (internal construction + metadata as JSON objects).
/// </summary>
public sealed class ErrorResultJsonConverter : JsonConverter<ErrorResult>
{
    // Errors inside a result are always written with this converter, so a source-generated context
    // does not need to list ErrorResult.
    internal static readonly ErrorResultJsonConverter Instance = new();

    /// <inheritdoc />
    public override ErrorResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for ErrorResult.");

        ErrorType type = ErrorType.Failure;
        string code = string.Empty;
        string message = string.Empty;
        Dictionary<string, object?>? metadata = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name on ErrorResult.");

            var propertyName = reader.GetString();
            reader.Read();

            if (ResultJsonConverter.IsProperty(propertyName, "type", options))
                type = ReadErrorType(ref reader);
            else if (ResultJsonConverter.IsProperty(propertyName, "code", options))
                code = reader.GetString() ?? string.Empty;
            else if (ResultJsonConverter.IsProperty(propertyName, "message", options))
                message = reader.GetString() ?? string.Empty;
            else if (ResultJsonConverter.IsProperty(propertyName, "metadata", options))
                metadata = ReadMetadata(ref reader);
            else
                reader.Skip();
        }

        return ErrorResult.Create(type, code, message, metadata);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ErrorResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WritePropName(writer, "type", options);
        writer.WriteStringValue(value.Type.ToString());

        WritePropName(writer, "code", options);
        writer.WriteStringValue(value.Code);

        WritePropName(writer, "message", options);
        writer.WriteStringValue(value.Message);

        if (value.Metadata is { Count: > 0 })
        {
            WritePropName(writer, "metadata", options);
            writer.WriteStartObject();
            foreach (var (key, val) in value.Metadata)
            {
                writer.WritePropertyName(key);
                WriteMetadataValue(writer, val, options);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes a metadata value through the options' type metadata when the resolver knows its runtime type,
    /// which is always the case with reflection-based options. Otherwise (e.g. a source-generated context that
    /// does not list the type) primitives are written directly and anything else as its invariant string form.
    /// </summary>
    private static void WriteMetadataValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (options.TryGetTypeInfo(value.GetType(), out var typeInfo))
        {
            JsonSerializer.Serialize(writer, value, typeInfo);
            return;
        }

        switch (value)
        {
            case string s: writer.WriteStringValue(s); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case short sh: writer.WriteNumberValue(sh); break;
            case byte by: writer.WriteNumberValue(by); break;
            case sbyte sb: writer.WriteNumberValue(sb); break;
            case uint ui: writer.WriteNumberValue(ui); break;
            case ulong ul: writer.WriteNumberValue(ul); break;
            case ushort us: writer.WriteNumberValue(us); break;
            case decimal m: writer.WriteNumberValue(m); break;
            case double d when double.IsFinite(d): writer.WriteNumberValue(d); break;
            case float f when float.IsFinite(f): writer.WriteNumberValue(f); break;
            case Guid g: writer.WriteStringValue(g); break;
            case DateTime dt: writer.WriteStringValue(dt); break;
            case DateTimeOffset dto: writer.WriteStringValue(dto); break;
            case JsonElement element: element.WriteTo(writer); break;
            case IFormattable formattable: writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture)); break;
            default: writer.WriteStringValue(value.ToString()); break;
        }
    }

    private static ErrorType ReadErrorType(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (Enum.TryParse<ErrorType>(s, ignoreCase: true, out var type))
                return type;
            throw new JsonException($"Unknown ErrorType '{s}'.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out var b))
            return (ErrorType)b;

        throw new JsonException("ErrorType must be a string or number.");
    }

    private static Dictionary<string, object?>? ReadMetadata(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("ErrorResult metadata must be an object.");

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name in metadata.");

            var key = reader.GetString()!;
            reader.Read();
            using var doc = JsonDocument.ParseValue(ref reader);
            metadata[key] = doc.RootElement.Clone();
        }

        return metadata.Count > 0 ? metadata : null;
    }

    private static void WritePropName(Utf8JsonWriter writer, string name, JsonSerializerOptions options)
    {
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(name) ?? name);
    }
}

/// <summary>
/// Converter for non-generic <see cref="Result"/> and its concrete variants.
/// </summary>
public sealed class ResultJsonConverter : JsonConverter<Result>
{
    internal static readonly ResultJsonConverter Instance = new();

    /// <inheritdoc />
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for Result.");

        Status? status = null;
        List<ErrorResult>? errors = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var propertyName = reader.GetString();
            reader.Read();

            if (IsProperty(propertyName, "status", options))
                status = ReadStatus(ref reader);
            else if (IsProperty(propertyName, "errors", options))
                errors = ReadErrors(ref reader, options);
            else
                reader.Skip();
        }

        return status switch
        {
            Status.Success => Result.Success(),
            Status.InProgress => Result.InProgress(),
            Status.Failed => Result.Failed(RequireErrors(errors)),
            _ => throw new JsonException("Result JSON requires a 'status' property.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        WriteStatus(writer, value.Status, options);

        if (value is FailedResult failed)
            WriteErrors(writer, failed.Errors, options);

        writer.WriteEndObject();
    }

    internal static bool IsProperty(string? name, string expected, JsonSerializerOptions options)
    {
        if (name is null) return false;
        if (options.PropertyNameCaseInsensitive)
            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

        var expectedName = options.PropertyNamingPolicy?.ConvertName(expected) ?? expected;
        return string.Equals(name, expectedName, StringComparison.Ordinal)
               || string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
    }

    internal static List<ErrorResult> RequireErrors(List<ErrorResult>? errors)
        => errors is { Count: > 0 }
            ? errors
            : throw new JsonException("Failed Result JSON requires a non-empty 'errors' array.");

    internal static Status ReadStatus(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (Enum.TryParse<Status>(s, ignoreCase: true, out var status))
                return status;
            throw new JsonException($"Unknown Result status '{s}'.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out var b))
            return (Status)b;

        throw new JsonException("Result status must be a string or number.");
    }

    internal static void WriteStatus(Utf8JsonWriter writer, Status status, JsonSerializerOptions options)
    {
        var name = options.PropertyNamingPolicy?.ConvertName("status") ?? "status";
        writer.WriteString(name, status.ToString());
    }

    internal static List<ErrorResult> ReadErrors(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return [];

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected errors array.");

        var list = new List<ErrorResult>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.Null)
                throw new JsonException("Null ErrorResult in errors array.");
            list.Add(ErrorResultJsonConverter.Instance.Read(ref reader, typeof(ErrorResult), options));
        }

        return list;
    }

    internal static void WriteErrors(
        Utf8JsonWriter writer,
        IReadOnlyList<ErrorResult> errors,
        JsonSerializerOptions options)
    {
        var name = options.PropertyNamingPolicy?.ConvertName("errors") ?? "errors";
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var error in errors)
            ErrorResultJsonConverter.Instance.Write(writer, error, options);
        writer.WriteEndArray();
    }
}

/// <summary>
/// Converter for <see cref="Result{T}"/> and its concrete variants.
/// </summary>
public sealed class ResultOfTJsonConverter<T> : JsonConverter<Result<T>>
{
    /// <inheritdoc />
    public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for Result<T>.");

        Status? status = null;
        List<ErrorResult>? errors = null;
        var hasValue = false;
        T? value = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var propertyName = reader.GetString();
            reader.Read();

            if (ResultJsonConverter.IsProperty(propertyName, "status", options))
                status = ResultJsonConverter.ReadStatus(ref reader);
            else if (ResultJsonConverter.IsProperty(propertyName, "errors", options))
                errors = ResultJsonConverter.ReadErrors(ref reader, options);
            else if (ResultJsonConverter.IsProperty(propertyName, "value", options))
            {
                hasValue = true;
                value = ReadValue(ref reader, options);
            }
            else
                reader.Skip();
        }

        return status switch
        {
            Status.Success => hasValue
                ? Result.Success(value!)
                : throw new JsonException("Successful Result<T> requires a 'value' property."),
            Status.InProgress => hasValue
                ? Result.InProgress(value!)
                : throw new JsonException("In-progress Result<T> requires a 'value' property."),
            Status.Failed => Result.Failed<T>(ResultJsonConverter.RequireErrors(errors)),
            _ => throw new JsonException("Result<T> JSON requires a 'status' property.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        ResultJsonConverter.WriteStatus(writer, value.Status, options);

        switch (value)
        {
            case SuccessResult<T> success:
                WriteValue(writer, success.Value, options);
                break;
            case InProgressResult<T> inProgress:
                WriteValue(writer, inProgress.Value, options);
                break;
            case FailedResult<T> failed:
                ResultJsonConverter.WriteErrors(writer, failed.Errors, options);
                break;
        }

        writer.WriteEndObject();
    }

    private static T? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        return JsonSerializer.Deserialize(ref reader, GetValueTypeInfo(options));
    }

    private static void WriteValue(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var name = options.PropertyNamingPolicy?.ConvertName("value") ?? "value";
        writer.WritePropertyName(name);

        JsonSerializer.Serialize(writer, value, GetValueTypeInfo(options));
    }

    // GetTypeInfo resolves through options.TypeInfoResolver (reflection or a source-generated context) and
    // caches the result on the options, so this is AOT safe and cheap after the first call.
    private static JsonTypeInfo<T> GetValueTypeInfo(JsonSerializerOptions options)
        => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}