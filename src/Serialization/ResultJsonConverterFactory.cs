using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KetPsi.Results.Serialization;

/// <summary>
/// <see cref="JsonConverterFactory"/> for <see cref="Result"/>, <see cref="Result{T}"/>,
/// their concrete variants, and <see cref="ErrorResult"/>.
/// </summary>
/// <remarks>
/// Wire shape (camelCase when that naming policy is configured):
/// <code>
/// { "status": "Success"|"Failed"|"InProgress", "value": ..., "errors": [ ... ] }
/// </code>
/// Nested <c>value</c> and <see cref="ErrorResult"/> payloads use
/// <see cref="JsonSerializer"/> with the supplied options so source-generated
/// <see cref="JsonTypeInfo"/> is respected when present.
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

        if (!typeToConvert.IsGenericType)
            return false;

        var def = typeToConvert.GetGenericTypeDefinition();
        return def == typeof(Result<>)
               || def == typeof(SuccessResult<>)
               || def == typeof(InProgressResult<>)
               || def == typeof(FailedResult<>);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(ErrorResult))
            return new ErrorResultJsonConverter();

        if (typeToConvert == typeof(Result)
            || typeToConvert == typeof(SuccessResult)
            || typeToConvert == typeof(InProgressResult)
            || typeToConvert == typeof(FailedResult))
            return new ResultJsonConverter();

        var t = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultOfTJsonConverter<>).MakeGenericType(t);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Converter for <see cref="ErrorResult"/> (internal construction + metadata as JSON objects).
/// </summary>
public sealed class ErrorResultJsonConverter : JsonConverter<ErrorResult>
{
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
                if (val is null)
                    writer.WriteNullValue();
                else
                    JsonSerializer.Serialize(writer, val, val.GetType(), options);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
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
            Status.Failed => Result.Failed(errors ?? []),
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

            var error = JsonSerializer.Deserialize<ErrorResult>(ref reader, options)
                        ?? throw new JsonException("Null ErrorResult in errors array.");
            list.Add(error);
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
            JsonSerializer.Serialize(writer, error, options);
        writer.WriteEndArray();
    }

    internal static JsonTypeInfo<T>? GetTypeInfo<T>(JsonSerializerOptions options)
    {
        if (options.TypeInfoResolver is null)
            return null;

        var info = options.TypeInfoResolver.GetTypeInfo(typeof(T), options);
        return info as JsonTypeInfo<T>;
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
            Status.Failed => Result.Failed<T>(errors ?? []),
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

        var typeInfo = ResultJsonConverter.GetTypeInfo<T>(options);
        if (typeInfo is not null)
            return JsonSerializer.Deserialize(ref reader, typeInfo);

        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    private static void WriteValue(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var name = options.PropertyNamingPolicy?.ConvertName("value") ?? "value";
        writer.WritePropertyName(name);

        var typeInfo = ResultJsonConverter.GetTypeInfo<T>(options);
        if (typeInfo is not null)
            JsonSerializer.Serialize(writer, value, typeInfo);
        else
            JsonSerializer.Serialize(writer, value, options);
    }
}