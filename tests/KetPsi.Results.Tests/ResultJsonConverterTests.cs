using System.Text.Json;

using KetPsi.Results.Serialization;

using Xunit;

namespace KetPsi.Results.Tests;

public class ResultJsonConverterTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        }.AddKetPsiResultConverters();
    }

    // ───────────────────────────── Non-generic Result ─────────────────────────────

    [Fact]
    public void Serialize_NonGeneric_Success()
    {
        var json = JsonSerializer.Serialize<Result>(Result.Success(), CreateOptions());
        Assert.Contains("\"status\":\"Success\"", json);
        Assert.DoesNotContain("\"errors\"", json);
        Assert.DoesNotContain("\"value\"", json);
    }

    [Fact]
    public void Serialize_NonGeneric_InProgress()
    {
        var json = JsonSerializer.Serialize<Result>(Result.InProgress(), CreateOptions());
        Assert.Contains("\"status\":\"InProgress\"", json);
    }

    [Fact]
    public void Serialize_NonGeneric_Failed_WithErrors()
    {
        var result = Result.Resource.NotFound("missing");
        var json = JsonSerializer.Serialize<Result>(result, CreateOptions());

        Assert.Contains("\"status\":\"Failed\"", json);
        Assert.Contains("\"code\":\"resource.not_found\"", json);
        Assert.Contains("\"message\":\"missing\"", json);
        Assert.Contains("\"type\":\"NotFound\"", json);
    }

    [Fact]
    public void Deserialize_NonGeneric_Success()
    {
        const string json = """{"status":"Success"}""";
        var result = JsonSerializer.Deserialize<Result>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsSuccessful());
        Assert.IsType<SuccessResult>(result);
    }

    [Fact]
    public void Deserialize_NonGeneric_InProgress()
    {
        const string json = """{"status":"InProgress"}""";
        var result = JsonSerializer.Deserialize<Result>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsInProgress());
    }

    [Fact]
    public void Deserialize_NonGeneric_Failed()
    {
        const string json =
            """
            {
              "status": "Failed",
              "errors": [
                {
                  "type": "Validation",
                  "code": "validation.invalid_input",
                  "message": "bad"
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<Result>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsFailed());
        var error = result.FirstError();
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Validation, error!.Type);
        Assert.Equal("validation.invalid_input", error.Code);
        Assert.Equal("bad", error.Message);
    }

    [Fact]
    public void RoundTrip_NonGeneric_Failed()
    {
        var original = Result.Failed(
            ErrorResult.Create(ErrorType.Conflict, "c.1", "dup"),
            ErrorResult.Create(ErrorType.NotFound, "n.1", "gone"));

        var options = CreateOptions();
        var json = JsonSerializer.Serialize<Result>(original, options);
        var restored = JsonSerializer.Deserialize<Result>(json, options);

        Assert.NotNull(restored);
        Assert.True(restored!.IsFailed());
        Assert.Equal(2, restored.GetErrors().Count);
        Assert.Equal("c.1", restored.GetErrors()[0].Code);
        Assert.Equal("n.1", restored.GetErrors()[1].Code);
    }

    // ───────────────────────────── Result<T> ─────────────────────────────

    [Fact]
    public void Serialize_Generic_Success()
    {
        Result<int> result = 42;
        var json = JsonSerializer.Serialize(result, CreateOptions());

        Assert.Contains("\"status\":\"Success\"", json);
        Assert.Contains("\"value\":42", json);
        Assert.DoesNotContain("\"errors\"", json);
    }

    [Fact]
    public void Serialize_Generic_Success_ComplexValue()
    {
        var result = Result.Success(new SampleDto { Id = 7, Name = "alice" });
        var json = JsonSerializer.Serialize(result, CreateOptions());

        Assert.Contains("\"status\":\"Success\"", json);
        Assert.Contains("\"id\":7", json);
        Assert.Contains("\"name\":\"alice\"", json);
    }

    [Fact]
    public void Serialize_Generic_InProgress()
    {
        var result = Result.InProgress("track-1");
        var json = JsonSerializer.Serialize(result, CreateOptions());

        Assert.Contains("\"status\":\"InProgress\"", json);
        Assert.Contains("\"value\":\"track-1\"", json);
    }

    [Fact]
    public void Serialize_Generic_Failed()
    {
        Result<string> result = Result.Authentication.Unauthorized("no token");
        var json = JsonSerializer.Serialize(result, CreateOptions());

        Assert.Contains("\"status\":\"Failed\"", json);
        Assert.Contains("\"code\":\"auth.unauthorized\"", json);
        Assert.DoesNotContain("\"value\"", json);
    }

    [Fact]
    public void Deserialize_Generic_Success()
    {
        const string json = """{"status":"Success","value":99}""";
        var result = JsonSerializer.Deserialize<Result<int>>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsSuccessful());
        Assert.True(result.TryGetResult(out var value));
        Assert.Equal(99, value);
    }

    [Fact]
    public void Deserialize_Generic_Success_ComplexValue()
    {
        const string json = """{"status":"Success","value":{"id":3,"name":"bob"}}""";
        var result = JsonSerializer.Deserialize<Result<SampleDto>>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.TryGetResult(out var dto));
        Assert.Equal(3, dto!.Id);
        Assert.Equal("bob", dto.Name);
    }

    [Fact]
    public void Deserialize_Generic_InProgress()
    {
        const string json = """{"status":"InProgress","value":"job-9"}""";
        var result = JsonSerializer.Deserialize<Result<string>>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsInProgress());
        Assert.True(result.TryGetResult(out var id));
        Assert.Equal("job-9", id);
    }

    [Fact]
    public void Deserialize_Generic_Failed()
    {
        const string json =
            """
            {
              "status": "Failed",
              "errors": [
                {
                  "type": "NotFound",
                  "code": "resource.not_found",
                  "message": "Order missing"
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<Result<SampleDto>>(json, CreateOptions());

        Assert.NotNull(result);
        Assert.True(result!.IsFailed());
        Assert.False(result.TryGetResult(out _));
        Assert.Equal("resource.not_found", result.FirstError()!.Code);
    }

    [Fact]
    public void Deserialize_Generic_Success_MissingValue_Throws()
    {
        const string json = """{"status":"Success"}""";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Result<int>>(json, CreateOptions()));
    }

    [Fact]
    public void Deserialize_MissingStatus_Throws()
    {
        const string json = """{"value":1}""";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Result<int>>(json, CreateOptions()));
    }

    [Fact]
    public void RoundTrip_Generic_Success()
    {
        var original = Result.Success(new SampleDto { Id = 1, Name = "x" });
        var options = CreateOptions();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<Result<SampleDto>>(json, options);

        Assert.NotNull(restored);
        Assert.True(restored!.TryGetResult(out var dto));
        Assert.Equal(1, dto!.Id);
        Assert.Equal("x", dto.Name);
    }

    [Fact]
    public void RoundTrip_Generic_Failed_ViaImplicitConversion()
    {
        Result<SampleDto> original = Result.Validation.InvalidInput("email required");
        var options = CreateOptions();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<Result<SampleDto>>(json, options);

        Assert.NotNull(restored);
        Assert.True(restored!.IsFailed());
        Assert.Equal("validation.invalid_input", restored.FirstError()!.Code);
        Assert.Equal("email required", restored.FirstError()!.Message);
    }

    // ───────────────────────────── ErrorResult ─────────────────────────────

    [Fact]
    public void Serialize_ErrorResult_WithoutMetadata()
    {
        var error = ErrorResult.Create(ErrorType.Forbidden, "permissions.forbidden", "no access");
        var json = JsonSerializer.Serialize(error, CreateOptions());

        Assert.Contains("\"type\":\"Forbidden\"", json);
        Assert.Contains("\"code\":\"permissions.forbidden\"", json);
        Assert.Contains("\"message\":\"no access\"", json);
        Assert.DoesNotContain("\"metadata\"", json);
    }

    [Fact]
    public void Serialize_ErrorResult_WithMetadata()
    {
        var error = ErrorResult.Create(
            ErrorType.Failure,
            "integration.keycloak",
            "failed",
            new Dictionary<string, object?> { ["status_code"] = 502, ["retry"] = true });

        var json = JsonSerializer.Serialize(error, CreateOptions());

        Assert.Contains("\"metadata\"", json);
        Assert.Contains("\"status_code\":502", json);
        Assert.Contains("\"retry\":true", json);
    }

    [Fact]
    public void Deserialize_ErrorResult_WithMetadata()
    {
        const string json =
            """
            {
              "type": "DependencyFailure",
              "code": "integration.keycloak.timeout",
              "message": "timed out",
              "metadata": { "attempt": 3, "region": "eu" }
            }
            """;

        var error = JsonSerializer.Deserialize<ErrorResult>(json, CreateOptions());

        Assert.NotNull(error);
        Assert.Equal(ErrorType.DependencyFailure, error!.Type);
        Assert.Equal("integration.keycloak.timeout", error.Code);
        Assert.Equal("timed out", error.Message);
        Assert.NotNull(error.Metadata);
        Assert.True(error.Metadata!.ContainsKey("attempt"));
        Assert.True(error.Metadata.ContainsKey("region"));
    }

    [Fact]
    public void RoundTrip_ErrorResult_WithMetadata()
    {
        var original = ErrorResult.Create(
            ErrorType.Validation,
            "v.1",
            "bad field",
            new Dictionary<string, object?> { ["field"] = "email" });

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<ErrorResult>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(original.Type, restored!.Type);
        Assert.Equal(original.Code, restored.Code);
        Assert.Equal(original.Message, restored.Message);
        Assert.NotNull(restored.Metadata);
        Assert.True(restored.Metadata!.ContainsKey("field"));
    }

    [Fact]
    public void RoundTrip_FailedResult_ErrorsIncludeMetadata()
    {
        var error = ErrorResult.Create(
            ErrorType.Failure,
            "integration.keycloak.invalid_grant",
            "Invalid grant",
            new Dictionary<string, object?> { ["status_code"] = 400 });

        Result<string> original = Result.Failed(error);
        var options = CreateOptions();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<Result<string>>(json, options);

        Assert.NotNull(restored);
        Assert.True(restored!.IsFailed());
        var restoredError = restored.FirstError();
        Assert.NotNull(restoredError);
        Assert.Equal("integration.keycloak.invalid_grant", restoredError!.Code);
        Assert.NotNull(restoredError.Metadata);
        Assert.True(restoredError.Metadata!.ContainsKey("status_code"));
    }

    // ───────────────────────────── Options / edge cases ─────────────────────────────

    [Fact]
    public void Deserialize_Status_CaseInsensitive()
    {
        const string json = """{"status":"success","value":1}""";
        var result = JsonSerializer.Deserialize<Result<int>>(json, CreateOptions());
        Assert.True(result!.IsSuccessful());
        Assert.Equal(1, ((SuccessResult<int>)result).Value);
    }

    [Fact]
    public void Deserialize_Status_Numeric()
    {
        const string json = """{"status":1,"value":5}""";
        var result = JsonSerializer.Deserialize<Result<int>>(json, CreateOptions());
        Assert.True(result!.IsSuccessful());
        Assert.Equal(5, ((SuccessResult<int>)result).Value);
    }

    [Fact]
    public void Deserialize_PascalCase_Properties()
    {
        const string json = """{"Status":"Failed","Errors":[{"Type":"NotFound","Code":"n","Message":"x"}]}""";
        var result = JsonSerializer.Deserialize<Result>(json, CreateOptions());
        Assert.True(result!.IsFailed());
        Assert.Equal("n", result.FirstError()!.Code);
    }

    [Fact]
    public void AddKetPsiResultConverters_IsIdempotent()
    {
        var options = new JsonSerializerOptions();
        options.AddKetPsiResultConverters();
        options.AddKetPsiResultConverters();

        var count = options.Converters.Count(c => c is ResultJsonConverterFactory);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Serialize_NullValue_OnSuccess_ReferenceType()
    {
        var result = Result.Success<string?>(null);
        var json = JsonSerializer.Serialize(result, CreateOptions());
        Assert.Contains("\"status\":\"Success\"", json);
        Assert.Contains("\"value\":null", json);

        var restored = JsonSerializer.Deserialize<Result<string?>>(json, CreateOptions());
        Assert.True(restored!.IsSuccessful());
        Assert.True(restored.TryGetResult(out var v));
        Assert.Null(v);
    }

    private sealed class SampleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}