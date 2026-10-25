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

    // ───────────────────────────── Failure invariant ─────────────────────────────

    [Fact]
    public void Failed_WithNoErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Failed(Array.Empty<ErrorResult>()));
        Assert.Throws<ArgumentException>(() => Result.Failed<int>(new List<ErrorResult>()));
    }

    [Fact]
    public void Failed_CopiesErrorList_SoCallerMutationDoesNotLeak()
    {
        var errors = new List<ErrorResult> { ErrorResult.Create(ErrorType.Validation, "a", "A") };
        var failed = Result.Failed(errors);

        errors.Add(ErrorResult.Create(ErrorType.Validation, "b", "B"));

        Assert.Single(failed.Errors);
    }

    // ───────────────────────────── Equality ─────────────────────────────

    [Fact]
    public void FailedResults_WithSameErrors_AreEqual()
    {
        var a = Result.Failed(ErrorType.NotFound, "x", "missing");
        var b = Result.Failed(ErrorType.NotFound, "x", "missing");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Result.Failed(ErrorType.NotFound, "y", "missing"));
    }

    [Fact]
    public void GenericFailedResults_WithSameErrors_AreEqual()
    {
        var a = Result.Failed<int>(ErrorType.NotFound, "x", "missing");
        var b = Result.Failed<int>(ErrorType.NotFound, "x", "missing");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ErrorResults_FromInterpolatedMessages_AreEqual()
    {
        int id = 5;
        var a = Result.Resource.NotFound($"Order {id:@orderId} missing");
        var b = Result.Resource.NotFound($"Order {id:@orderId} missing");

        Assert.Equal(a, b);
        Assert.Equal(a.FirstError, b.FirstError);
    }

    // ───────────────────────────── with-expressions keep invariants ─────────────────────────────

    [Fact]
    public void With_EmptyErrors_Throws()
    {
        var failed = Result.Failed(ErrorType.Failure, "x", "y");

        Assert.Throws<ArgumentException>(() => failed with { Errors = [] });
        Assert.Throws<ArgumentException>(() => Result.Failed<int>("boom") with { Errors = [] });
    }

    [Fact]
    public void With_Errors_CopiesList()
    {
        var replacement = new List<ErrorResult> { ErrorResult.Create(ErrorType.Validation, "a", "A") };
        var failed = Result.Failed("x") with { Errors = replacement };

        replacement.Clear();

        Assert.Equal("a", Assert.Single(failed.Errors).Code);
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

    // ───────────────────────────── TryGetResult nullability ─────────────────────────────

    [Fact]
    public void TryGetResult_SuccessWithNull_ReturnsTrueAndNull()
    {
        Result<string?> result = Result.Success<string?>(null);

        Assert.True(result.TryGetResult(out var value));
        Assert.Null(value);
    }

    // ───────────────────────────── Builders ─────────────────────────────

    [Fact]
    public void ErrorBuilder_WithoutMetadata_HasNullMetadata()
    {
        var failed = Result.Failed(e => e.WithCode("c").WithMessage("m"));
        Assert.Null(failed.FirstError!.Metadata);
    }

    [Fact]
    public void ServiceBuilder_WithoutMetadata_HasNullMetadata()
    {
        var failed = Result.Service.Failure(b => b.WithServiceName("svc").WithMessage("m"));
        Assert.Null(failed.FirstError!.Metadata);
    }

    // ───────────────────────────── Message handler ─────────────────────────────

    [Fact]
    public void FullMask_DoesNotLeakLengthOfShortValues()
    {
        string pin = "12";
        var failed = Result.Validation.InvalidInput($"pin {pin:mask}");

        Assert.Equal("pin ***", failed.FirstError!.Message);
        Assert.Equal("12", failed.FirstError.Metadata!["pin"]);
    }

    // ───────────────────────────── PagedData ─────────────────────────────

    [Fact]
    public void PagedData_HasNextPage_DoesNotOverflow()
    {
        var page = PagedData<int>.Create([], pageNumber: 50_000, pageSize: 50_000, totalCount: long.MaxValue);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void PagedData_TotalPages()
    {
        Assert.Equal(5, PagedData<int>.Create([], 1, 20, 95).TotalPages);
        Assert.Equal(0, PagedData<int>.Create([], 1, 20, 0).TotalPages);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 10, -1)]
    public void PagedData_InvalidArguments_Throw(int pageNumber, int pageSize, long totalCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PagedData<int>.Create([], pageNumber, pageSize, totalCount));
    }

}

