using System.Text.Json;
using System.Text.Json.Serialization;

using KetPsi.Results.Serialization;

using Xunit;

namespace KetPsi.Results.Tests;

/// <summary>
/// Regression tests for bugs where a generic <see cref="FailedResult{T}"/> was treated
/// as non-failed (or as having no errors) when seen through the non-generic <see cref="Result"/> type,
/// plus serialization of concrete result types and other edge cases.
/// </summary>
public class ResultRegressionTests
{
    private static JsonSerializerOptions CreateOptions() => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    }.AddKetPsiResultConverters();

    // ───────────────────────────── Generic failures via base type ─────────────────────────────

    [Fact]
    public void Combine_IncludesGenericFailures()
    {
        var combined = Result.Combine(
            Result.Success(1),
            Result.Failed<int>(ErrorType.Validation, "v.a", "A"),
            Result.Resource.NotFound());

        Assert.True(combined.IsFailed());
        Assert.Equal(["v.a", "resource.not_found"], combined.GetErrors().Select(e => e.Code));
    }

    [Fact]
    public void GetErrors_OnGenericFailure_TypedAsBase_ReturnsErrors()
    {
        Result result = Result.Failed<int>(ErrorType.NotFound, "x.missing", "missing");

        Assert.Single(result.GetErrors());
        Assert.Equal("x.missing", result.FirstError()?.Code);
        Assert.Contains("x.missing:missing", result.GetErrorAsString());
    }

    [Fact]
    public void AsFailedOfT_FromOtherGenericFailure_KeepsErrors()
    {
        Result<string> token = Result.Authentication.Unauthorized("bad token");

        var forwarded = token.AsFailed<int>();

        var error = Assert.Single(forwarded.Errors);
        Assert.Equal("auth.unauthorized", error.Code);
    }

    [Fact]
    public void AsFailed_NonGeneric_FromGenericFailure_KeepsErrors()
    {
        Result result = Result.Failed<int>(ErrorType.Conflict, "c", "conflict");

        var failed = result.AsFailed();

        Assert.Equal("c", Assert.Single(failed.Errors).Code);
    }

    [Fact]
    public void Match_NonGeneric_OnGenericFailure_PassesErrors()
    {
        Result result = Result.Failed<int>(ErrorType.Failure, "f", "boom");

        var count = result.Match(() => -1, errors => errors.Count);

        Assert.Equal(1, count);
    }

    // ───────────────────────────── Misc API ─────────────────────────────

    [Fact]
    public void AsFailedOfT_OnSameType_ReturnsSameInstance()
    {
        Result<int> failed = Result.Failed<int>("boom");
        Assert.Same(failed, failed.AsFailed<int>());
    }

    [Fact]
    public void Combine_WithNullElement_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Combine(Result.Success(), null!));
    }

}

