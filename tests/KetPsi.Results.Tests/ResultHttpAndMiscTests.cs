using Xunit;

namespace KetPsi.Results.Tests;

public class ResultHttpAndMiscTests
{
    // ───────────────────────────── HTTP status codes ─────────────────────────────

    [Theory]
    [InlineData(ErrorType.Failure, 500)]
    [InlineData(ErrorType.DependencyFailure, 503)]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.RuleViolation, 422)]
    [InlineData(ErrorType.QuotaExceeded, 429)]
    [InlineData(ErrorType.Canceled, 499)]
    public void ToStatusCode_MapsCorrectly(ErrorType type, int expected)
        => Assert.Equal(expected, type.ToStatusCode());

    [Fact]
    public void FailedResult_ToStatusCode_UsesFirstError()
        => Assert.Equal(404, Result.Resource.NotFound().ToStatusCode());

    [Fact]
    public void FailedResult_ToStatusCode_MultiError_UsesFirst()
    {
        var result = Result.Failed(
            ErrorResult.Create(ErrorType.Validation, "v", "a"),
            ErrorResult.Create(ErrorType.NotFound, "n", "b"));
        Assert.Equal(400, result.ToStatusCode());
    }

    [Fact]
    public void FailedResultGeneric_ToStatusCode()
    {
        var result = Result.Failed<string>(ErrorType.Forbidden, "f", "no");
        Assert.Equal(403, result.ToStatusCode());
    }

    // ───────────────────────────── ProblemDetails ─────────────────────────────

    [Fact]
    public void ToProblemDetails_ContainsExpectedShape()
    {
        var result = Result.Validation.InvalidInput("bad field");
        var pd = result.ToProblemDetails("/api/items");

        Assert.Equal("validation.invalid_input", pd["type"]);
        Assert.Equal("Validation", pd["title"]);
        Assert.Equal(400, pd["status"]);
        Assert.Equal("bad field", pd["detail"]);
        Assert.Equal("/api/items", pd["instance"]);
        Assert.NotNull(pd["errors"]);
    }

    [Fact]
    public void ToProblemDetails_NullInstance()
    {
        var result = Result.Failed("x");
        var pd = result.ToProblemDetails();
        Assert.Null(pd["instance"]);
    }

    [Fact]
    public void ToProblemDetails_MultiError_ErrorsArrayHasAll()
    {
        var result = Result.Failed(
            ErrorResult.Create(ErrorType.Validation, "v.1", "a"),
            ErrorResult.Create(ErrorType.NotFound, "n.1", "b"));
        var pd = result.ToProblemDetails();
        var errors = Assert.IsAssignableFrom<Array>(pd["errors"]);
        Assert.Equal(2, errors.Length);
    }

    // ───────────────────────────── PagedData ─────────────────────────────

    [Fact]
    public void PagedData_MiddlePage_HasBoth()
    {
        var page = PagedData<int>.Create([1, 2, 3], pageNumber: 2, pageSize: 3, totalCount: 10);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(3, page.PageSize);
        Assert.Equal(10, page.TotalCount);
    }

    [Fact]
    public void PagedData_FirstPage_NoPrevious()
    {
        var page = PagedData<string>.Create(["a"], 1, 10, 5);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public void PagedData_LastPage_ExactBoundary_NoNext()
    {
        // 2 * 5 = 10 == total → no next
        var page = PagedData<int>.Create([1, 2, 3, 4, 5], 2, 5, 10);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public void PagedData_LastPage_Partial_NoNext()
    {
        var page = PagedData<int>.Create([1, 2], 3, 5, 12);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public void PagedData_Empty_ZeroTotal()
    {
        var page = PagedData<int>.Create([], 1, 10, 0);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void PagedData_SinglePage_Full()
    {
        var page = PagedData<int>.Create([1, 2, 3], 1, 10, 3);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }
}
