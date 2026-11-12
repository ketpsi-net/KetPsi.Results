using System.Text.Json;
using System.Text.Json.Serialization;
using KetPsi.Results.Serialization;
using Xunit;

namespace KetPsi.Results.Tests;

/// <summary>
/// Covers the JSON paths that Native AOT relies on: converters picked up from the types' attributes (no
/// registration), source-generated contexts with value-type payloads, metadata whose types the context
/// does not list, and per-type registration.
/// </summary>
public partial class ResultJsonAotTests
{
    private static readonly JsonSerializerOptions Unregistered = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void Attributes_RoundTripGenericResult_WithoutRegistration()
    {
        Result<int> result = 42;

        var json = JsonSerializer.Serialize(result, Unregistered);
        Assert.Equal("{\"status\":\"Success\",\"value\":42}", json);

        var restored = JsonSerializer.Deserialize<Result<int>>(json, Unregistered);
        Assert.Equal(result, restored);
    }

    [Fact]
    public void Attributes_RoundTripNonGenericAndConcreteTypes_WithoutRegistration()
    {
        var failed = Result.Resource.NotFound("Order");

        var json = JsonSerializer.Serialize(failed, Unregistered);
        Assert.Equal(failed, JsonSerializer.Deserialize<FailedResult>(json, Unregistered));
        Assert.Equal(failed, JsonSerializer.Deserialize<Result>(json, Unregistered));

        var success = Result.Success("x");
        var successJson = JsonSerializer.Serialize(success, Unregistered);
        Assert.Equal(success, JsonSerializer.Deserialize<SuccessResult<string>>(successJson, Unregistered));

        var error = failed.Errors[0];
        Assert.Equal(error, JsonSerializer.Deserialize<ErrorResult>(JsonSerializer.Serialize(error, Unregistered), Unregistered));
    }

    [Fact]
    public void Attributes_ConcreteTypeWithWrongStatus_ThrowsJsonException()
    {
        const string json = "{\"status\":\"InProgress\",\"value\":1}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SuccessResult<int>>(json, Unregistered));
    }

    [Fact]
    public void SourceGen_ValueTypePayload_RoundTrips()
    {
        var options = new JsonSerializerOptions(AotContext.Default.Options);
        Result<int> result = Result.InProgress(7);

        var json = JsonSerializer.Serialize(result, options);
        Assert.Equal("{\"status\":\"InProgress\",\"value\":7}", json);
        Assert.Equal(result, JsonSerializer.Deserialize<Result<int>>(json, options));
    }

    [Fact]
    public void SourceGen_FailureWithMetadataTypesNotInContext_WritesPrimitivesAndStrings()
    {
        var options = new JsonSerializerOptions(AotContext.Default.Options);
        var id = Guid.Parse("6f1c1d5e-0d6b-4f4e-9a55-1b0f3c1e2a10");
        var metadata = new Dictionary<string, object?>
        {
            ["count"] = 3L,
            ["id"] = id,
            ["kind"] = ErrorType.Conflict,
            ["ok"] = true,
            ["none"] = null,
        };
        Result<AotDto> failed = Result.Failed(ErrorResult.Create(ErrorType.Conflict, "order.conflict", "Busy.", metadata));

        var json = JsonSerializer.Serialize(failed, options);

        Assert.Contains("\"count\":3", json);
        Assert.Contains($"\"id\":\"{id}\"", json);
        Assert.Contains("\"kind\":\"Conflict\"", json);
        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"none\":null", json);

        var restored = JsonSerializer.Deserialize<Result<AotDto>>(json, options);
        Assert.Equal("order.conflict", restored!.FirstError()!.Code);
    }

    [Fact]
    public void ReflectionOptions_MetadataStillUsesSerializerDefaults()
    {
        var error = ErrorResult.Create(ErrorType.Failure, "c", "m",
            new Dictionary<string, object?> { ["kind"] = ErrorType.Conflict, ["dto"] = new AotDto { Name = "n" } });

        var json = JsonSerializer.Serialize(error, Unregistered);

        Assert.Contains("\"kind\":7", json);   // enums as numbers, the System.Text.Json default
        Assert.Contains("\"dto\":{\"name\":\"n\"}", json);
    }

    [Fact]
    public void AddKetPsiResultConverters_OfT_AddsTypedConvertersAheadOfFactory_Once()
    {
        var options = new JsonSerializerOptions()
            .AddKetPsiResultConverters<int>()
            .AddKetPsiResultConverters<int>()
            .AddKetPsiResultConverters<string>();

        Assert.Single(options.Converters, c => c is ResultJsonConverterFactory);
        Assert.Single(options.Converters, c => c is ResultOfTJsonConverter<int>);
        Assert.Single(options.Converters, c => c is ResultOfTJsonConverter<string>);
        Assert.IsType<ResultJsonConverterFactory>(options.Converters[^1]);

        Result<int> result = 5;
        Assert.Equal(result, JsonSerializer.Deserialize<Result<int>>(JsonSerializer.Serialize(result, options), options));

        var concrete = Result.Success(9);
        var concreteJson = JsonSerializer.Serialize(concrete, options);
        Assert.Equal(concrete, JsonSerializer.Deserialize<SuccessResult<int>>(concreteJson, options));
    }

    public sealed class AotDto
    {
        public string Name { get; set; } = "";
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Result<int>))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(Result<AotDto>))]
    [JsonSerializable(typeof(AotDto))]
    private partial class AotContext : JsonSerializerContext;
}
