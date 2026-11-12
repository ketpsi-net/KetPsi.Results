using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using KetPsi.Results;
using KetPsi.Results.Serialization;

var options = new JsonSerializerOptions(SmokeContext.Default.Options);
#if !NET11_0_OR_GREATER
// .NET 9/10 under Native AOT cannot build generic converters at run time: register each payload type.
options.AddKetPsiResultConverters<int>().AddKetPsiResultConverters<OrderDto>();
#endif

// Native AOT apps must serialize through JsonTypeInfo<T>; options.GetTypeInfo resolves it from the context.
string Serialize<T>(T value) => JsonSerializer.Serialize(value, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
T? Deserialize<T>(string json) => JsonSerializer.Deserialize(json, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));

var failures = 0;

void Check(string name, bool passed)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
    if (!passed) failures++;
}

void Run(string name, Func<bool> check)
{
    try { Check(name, check()); }
    catch (Exception e) { Console.WriteLine($"FAIL {name}: {e.GetType().Name}: {e.Message}"); failures++; }
}

Run("value type Result<int> round-trips", () =>
{
    Result<int> result = 42;
    var json = Serialize(result);
    return json == "{\"status\":\"Success\",\"value\":42}"
           && Deserialize<Result<int>>(json) == result;
});

Run("reference type Result<OrderDto> round-trips", () =>
{
    Result<OrderDto> result = new OrderDto("A-1", 3);
    var json = Serialize(result);
    return Deserialize<Result<OrderDto>>(json) == result;
});

Run("concrete SuccessResult<int> and InProgressResult<OrderDto>", () =>
{
    var success = Result.Success(5);
    var inProgress = Result.InProgress(new OrderDto("B-2", 1));
    return Deserialize<SuccessResult<int>>(Serialize(success)) == success
           && Deserialize<InProgressResult<OrderDto>>(Serialize(inProgress)) == inProgress;
});

Run("concrete type with wrong status throws JsonException", () =>
{
    try { Deserialize<SuccessResult<int>>("{\"status\":\"InProgress\",\"value\":1}"); return false; }
    catch (JsonException) { return true; }
});

Run("failure with interpolated metadata round-trips", () =>
{
    var orderId = 1234;
    var token = "secret-token";
    Result<OrderDto> failed = Result.Resource.NotFound($"Order {orderId} not found for {token:mask}");
    var json = Serialize(failed);
    var restored = Deserialize<Result<OrderDto>>(json);
    return json.Contains("\"orderId\":1234")
           && restored!.FirstError() is { Code: "resource.not_found" } error
           && error.Message == "Order 1234 not found for ***";
});

Run("non-generic Result and ErrorResult", () =>
{
    var combined = Result.Combine(Result.Success(), Result.Validation.InvalidInput("bad"));
    var json = Serialize(combined);
    var error = combined.FirstError()!;
    return Deserialize<Result>(json) == combined
           && Deserialize<ErrorResult>(Serialize(error)) == error;
});

Run("exception conversion and HTTP mapping", () =>
{
    var failed = Result.FromException(new InvalidOperationException("boom"));
    return failed.ToStatusCode() == 500;
});

Console.WriteLine(failures == 0 ? "All AOT checks passed." : $"{failures} AOT check(s) failed.");
return failures == 0 ? 0 : 1;

public sealed record OrderDto(string Id, int Quantity);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Result))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(Result<int>))]
[JsonSerializable(typeof(SuccessResult<int>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Result<OrderDto>))]
[JsonSerializable(typeof(InProgressResult<OrderDto>))]
[JsonSerializable(typeof(OrderDto))]
internal sealed partial class SmokeContext : JsonSerializerContext;
