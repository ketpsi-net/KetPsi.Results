using Xunit;

namespace KetPsi.Results.Tests;

public class ResultBasicTests
{
    // ───────────────────────────── Success / InProgress ─────────────────────────────

    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccessful());
        Assert.False(result.IsFailed());
        Assert.False(result.IsInProgress());
        Assert.Equal(Status.Success, result.Status);
    }

    [Fact]
    public void Success_WithValue_CreatesSuccessfulGenericResult()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccessful());
        Assert.IsType<SuccessResult<int>>(result);
        Assert.Equal(42, ((SuccessResult<int>)result).Value);
    }

    [Fact]
    public void Success_WithNullReferenceType_IsAllowed()
    {
        var result = Result.Success<string?>(null);
        Assert.True(result.IsSuccessful());
        Assert.Null(((SuccessResult<string?>)result).Value);
    }

    [Fact]
    public void InProgress_CreatesInProgressResult()
    {
        var result = Result.InProgress();
        Assert.True(result.IsInProgress());
        Assert.Equal(Status.InProgress, result.Status);
    }

    [Fact]
    public void InProgress_WithValue_CreatesInProgressGenericResult()
    {
        var result = Result.InProgress("tracking-123");
        Assert.True(result.IsInProgress());
        Assert.Equal("tracking-123", ((InProgressResult<string>)result).Value);
    }

    // ───────────────────────────── Failed factories ─────────────────────────────

    [Fact]
    public void Failed_WithMessage_CreatesFailedResult()
    {
        var result = Result.Failed("something went wrong");
        Assert.True(result.IsFailed());
        Assert.Single(result.Errors);
        Assert.Equal("server.internal_error", result.Errors[0].Code);
        Assert.Equal("something went wrong", result.Errors[0].Message);
        Assert.Equal(ErrorType.Failure, result.Errors[0].Type);
    }

    [Fact]
    public void Failed_Generic_WithMessage()
    {
        var result = Result.Failed<int>("boom");
        Assert.True(result.IsFailed());
        Assert.Equal("boom", result.Errors[0].Message);
        Assert.Equal(ErrorType.Failure, result.Errors[0].Type);
    }

    [Fact]
    public void Failed_WithTypeCodeMessage()
    {
        var result = Result.Failed(ErrorType.NotFound, "res.missing", "Item not found");
        Assert.True(result.IsFailed());
        Assert.Equal(ErrorType.NotFound, result.FirstError()!.Type);
        Assert.Equal("res.missing", result.FirstError()!.Code);
        Assert.Equal("Item not found", result.FirstError()!.Message);
    }

    [Fact]
    public void Failed_Generic_WithTypeCodeMessage()
    {
        var result = Result.Failed<string>(ErrorType.Conflict, "c.1", "dup");
        Assert.Equal(ErrorType.Conflict, result.FirstError()!.Type);
        Assert.Equal("c.1", result.FirstError()!.Code);
    }

    [Fact]
    public void Failed_FromSingleErrorResult()
    {
        var error = ErrorResult.Create(ErrorType.Validation, "v.1", "bad");
        var result = Result.Failed(error);
        Assert.Same(error, result.Errors[0]);
    }

    [Fact]
    public void Failed_FromParamsErrors()
    {
        var e1 = ErrorResult.Create(ErrorType.Validation, "v.1", "a");
        var e2 = ErrorResult.Create(ErrorType.NotFound, "n.1", "b");
        var result = Result.Failed(e1, e2);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("v.1", result.Errors[0].Code);
        Assert.Equal("n.1", result.Errors[1].Code);
    }

    [Fact]
    public void Failed_FromList()
    {
        IReadOnlyList<ErrorResult> list =
        [
            ErrorResult.Create(ErrorType.Validation, "v.1", "a"),
            ErrorResult.Create(ErrorType.Forbidden, "f.1", "b")
        ];
        var result = Result.Failed(list);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Failed_Generic_FromParamsErrors()
    {
        var result = Result.Failed<int>(
            ErrorResult.Create(ErrorType.Validation, "v.1", "a"),
            ErrorResult.Create(ErrorType.NotFound, "n.1", "b"));
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Failed_WithInterpolatedHandler()
    {
        int id = 7;
        var result = Result.Failed(ErrorType.NotFound, "res.missing", $"Item {id:@itemId} gone");
        Assert.Equal("Item 7 gone", result.FirstError()!.Message);
        Assert.Equal(7, result.FirstError()!.Metadata!["itemId"]);
    }

    [Fact]
    public void Failed_Generic_WithInterpolatedHandler()
    {
        string name = "alice";
        var result = Result.Failed<string>(ErrorType.Validation, "v.1", $"Bad name {name:@n}");
        Assert.Equal("Bad name alice", result.FirstError()!.Message);
        Assert.Equal("alice", result.FirstError()!.Metadata!["n"]);
    }

    // ───────────────────────────── Implicit conversions ─────────────────────────────

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<int> result = 99;
        Assert.True(result.IsSuccessful());
        Assert.Equal(99, ((SuccessResult<int>)result).Value);
    }

    [Fact]
    public void ImplicitConversion_FromFailedResult_CreatesTypedFailure()
    {
        FailedResult failed = Result.Resource.NotFound();
        Result<string> typed = failed;
        Assert.True(typed.IsFailed());
        Assert.Equal("resource.not_found", typed.GetErrors()[0].Code);
    }

    // ───────────────────────────── Domain factories ─────────────────────────────

    [Fact]
    public void DomainFactories_ReturnCorrectTypes()
    {
        Assert.Equal(ErrorType.Unauthorized, Result.Authentication.Unauthorized().FirstError()!.Type);
        Assert.Equal(ErrorType.Unauthorized, Result.Authentication.TokenExpired().FirstError()!.Type);
        Assert.Equal(ErrorType.Forbidden, Result.Permissions.Forbidden().FirstError()!.Type);
        Assert.Equal(ErrorType.NotFound, Result.Resource.NotFound().FirstError()!.Type);
        Assert.Equal(ErrorType.Conflict, Result.Resource.AlreadyExists().FirstError()!.Type);
        Assert.Equal(ErrorType.RuleViolation, Result.Business.RuleViolation().FirstError()!.Type);
        Assert.Equal(ErrorType.QuotaExceeded, Result.Business.QuotaExceeded().FirstError()!.Type);
        Assert.Equal(ErrorType.Conflict, Result.Business.StateConflict().FirstError()!.Type);
        Assert.Equal(ErrorType.Validation, Result.Validation.InvalidInput().FirstError()!.Type);
        Assert.Equal(ErrorType.Failure, Result.System.InternalError().FirstError()!.Type);
        Assert.Equal(ErrorType.DependencyFailure, Result.Service.Unavailable("service").FirstError()!.Type);
        Assert.Equal(ErrorType.Canceled, Result.System.ExecutionCanceled().FirstError()!.Type);
    }

    [Fact]
    public void DomainFactories_CustomMessage_OverridesDefault()
    {
        Assert.Equal("custom", Result.Authentication.Unauthorized("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Authentication.TokenExpired("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Permissions.Forbidden("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Resource.NotFound("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Resource.AlreadyExists("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Business.RuleViolation("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Business.QuotaExceeded("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Business.StateConflict("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Validation.InvalidInput("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.System.InternalError("custom").FirstError()!.Message);
        Assert.Equal("custom", Result.Service.Unavailable("service","code","custom").FirstError()!.Message);
        Assert.Equal("custom", Result.System.ExecutionCanceled("custom").FirstError()!.Message);
    }

    [Fact]
    public void DomainFactories_CachedInstances_AreReused()
    {
        Assert.Same(Result.Authentication.Unauthorized(), Result.Authentication.Unauthorized());
        Assert.Same(Result.Resource.NotFound(), Result.Resource.NotFound());
        Assert.Same(Result.Validation.InvalidInput(), Result.Validation.InvalidInput());
        Assert.Same(Result.System.InternalError(), Result.System.InternalError());
    }

    [Fact]
    public void DomainFactories_CustomMessage_CreatesNewInstance()
    {
        Assert.NotSame(
            Result.Resource.NotFound(),
            Result.Resource.NotFound("other"));
    }

    [Fact]
    public void DomainFactory_WithHandler_CapturesMetadata()
    {
        int orderId = 99;
        var result = Result.Resource.NotFound($"Order {orderId:@id} not found");
        Assert.Equal("Order 99 not found", result.FirstError()!.Message);
        Assert.Equal(99, result.FirstError()!.Metadata!["id"]);
        Assert.Equal("resource.not_found", result.FirstError()!.Code);
    }

    // ───────────────────────────── FromException ─────────────────────────────

    [Fact]
    public void FromException_MapsOperationCanceled()
    {
        var result = Result.FromException(new OperationCanceledException("aborted"));
        Assert.Equal(ErrorType.Canceled, result.FirstError()!.Type);
        Assert.Equal("system.execution_canceled", result.FirstError()!.Code);
        Assert.Equal("aborted", result.FirstError()!.Message);
    }

    [Fact]
    public void FromException_MapsGenericException()
    {
        var result = Result.FromException(new InvalidOperationException("boom"));
        Assert.Equal(ErrorType.Failure, result.FirstError()!.Type);
        Assert.Equal("server.internal_error", result.FirstError()!.Code);
        Assert.Equal("boom", result.FirstError()!.Message);
    }

    [Fact]
    public void FromException_WithExplicitTypeAndCode()
    {
        var result = Result.FromException(
            new Exception("x"),
            type: ErrorType.DependencyFailure,
            code: "custom.code");
        Assert.Equal(ErrorType.DependencyFailure, result.FirstError()!.Type);
        Assert.Equal("custom.code", result.FirstError()!.Code);
    }

    [Fact]
    public void FromException_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.FromException(null!));
    }

    [Fact]
    public void FromException_Generic_PreservesErrors()
    {
        var result = Result.FromException<string>(new InvalidOperationException("x"));
        Assert.True(result.IsFailed());
        Assert.Equal("x", result.FirstError()!.Message);
    }

    // ───────────────────────────── Combine ─────────────────────────────

    [Fact]
    public void Combine_AllSuccess_ReturnsSuccess()
    {
        var combined = Result.Combine(Result.Success(), Result.Success());
        Assert.True(combined.IsSuccessful());
    }

    [Fact]
    public void Combine_Empty_ReturnsSuccess()
    {
        var combined = Result.Combine();
        Assert.True(combined.IsSuccessful());
    }

    [Fact]
    public void Combine_WithFailures_AggregatesErrors()
    {
        var combined = Result.Combine(
            Result.Success(),
            Result.Validation.InvalidInput("bad"),
            Result.Resource.NotFound("missing"));

        Assert.True(combined.IsFailed());
        var errors = combined.GetErrors();
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Code == "validation.invalid_input");
        Assert.Contains(errors, e => e.Code == "resource.not_found");
    }

    [Fact]
    public void Combine_OnlyFailures_AggregatesAll()
    {
        var combined = Result.Combine(
            Result.Validation.InvalidInput("a"),
            Result.Business.RuleViolation("b"));
        Assert.Equal(2, combined.GetErrors().Count);
    }

    // ───────────────────────────── Match ─────────────────────────────

    [Fact]
    public void Match_Generic_Success()
    {
        var result = Result.Success(10);
        var value = result.Match(v => v * 2, _ => -1);
        Assert.Equal(20, value);
    }

    [Fact]
    public void Match_Generic_Failure()
    {
        var result = Result.Failed<int>("boom");
        var value = result.Match(v => v, errs => errs.Count);
        Assert.Equal(1, value);
    }

    [Fact]
    public void Match_Generic_InProgress_FallsBackToSuccessHandler()
    {
        var result = Result.InProgress(5);
        var value = result.Match(v => v, _ => -1);
        Assert.Equal(5, value);
    }

    [Fact]
    public void Match_Generic_InProgress_UsesExplicitHandler()
    {
        var result = Result.InProgress(5);
        var value = result.Match(
            onSuccess: v => v,
            onFailure: _ => -1,
            onInProgress: v => v + 100);
        Assert.Equal(105, value);
    }

    [Fact]
    public void Match_NonGeneric_Success()
    {
        var result = Result.Success();
        var text = result.Match(() => "ok", _ => "fail");
        Assert.Equal("ok", text);
    }

    [Fact]
    public void Match_NonGeneric_Failure()
    {
        var result = Result.Failed("x");
        var text = result.Match(() => "ok", errs => errs[0].Message);
        Assert.Equal("x", text);
    }

    [Fact]
    public void Match_NonGeneric_InProgress_Explicit()
    {
        var result = Result.InProgress();
        var text = result.Match(
            onSuccess: () => "ok",
            onFailure: _ => "fail",
            onInProgress: () => "pending");
        Assert.Equal("pending", text);
    }

    [Fact]
    public void Match_MultiError_Failure_ReceivesAll()
    {
        var result = Result.Failed(
            ErrorResult.Create(ErrorType.Validation, "v.1", "a"),
            ErrorResult.Create(ErrorType.NotFound, "n.1", "b"));
        var count = result.Match(() => 0, errs => errs.Count);
        Assert.Equal(2, count);
    }

    // ───────────────────────────── Extraction helpers ─────────────────────────────

    [Fact]
    public void FirstError_OnSuccess_ReturnsNull()
    {
        Assert.Null(Result.Success().FirstError());
        Assert.Null(Result.Success(1).FirstError());
    }

    [Fact]
    public void FirstError_OnInProgress_ReturnsNull()
    {
        Assert.Null(Result.InProgress().FirstError());
        Assert.Null(Result.InProgress("t").FirstError());
    }

    [Fact]
    public void FirstError_OnFailure_ReturnsFirst()
    {
        var result = Result.Failed(
            ErrorResult.Create(ErrorType.Validation, "v.1", "first"),
            ErrorResult.Create(ErrorType.NotFound, "n.1", "second"));
        Assert.Equal("v.1", result.FirstError()!.Code);
    }

    [Fact]
    public void FirstError_Property_OnFailedResult()
    {
        var result = Result.Failed("x");
        Assert.NotNull(result.FirstError);
        Assert.Equal("x", result.FirstError!.Message);
    }

    [Fact]
    public void GetErrors_OnSuccess_ReturnsEmpty()
    {
        Assert.Empty(Result.Success().GetErrors());
        Assert.Empty(Result.Success(1).GetErrors());
        Assert.Empty(Result.InProgress().GetErrors());
    }

    [Fact]
    public void GetErrorAsString_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Result.Success().GetErrorAsString());
    }

    [Fact]
    public void GetErrorAsString_FormatsAllErrors()
    {
        var result = Result.Failed(
            ErrorResult.Create(ErrorType.Validation, "v1", "first"),
            ErrorResult.Create(ErrorType.NotFound, "n1", "second"));
        var text = result.GetErrorAsString();
        Assert.Contains("v1:first", text);
        Assert.Contains("n1:second", text);
    }

    [Fact]
    public void TryGetResult_Success_ReturnsTrue()
    {
        Result<int> r = 42;
        Assert.True(r.TryGetResult(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void TryGetResult_InProgress_ReturnsTrue()
    {
        var r = Result.InProgress("track-1");
        Assert.True(r.TryGetResult(out var v));
        Assert.Equal("track-1", v);
    }

    [Fact]
    public void TryGetResult_Failure_ReturnsFalse()
    {
        Result<int> r = Result.Failed<int>("x");
        Assert.False(r.TryGetResult(out var v));
        Assert.Equal(0, v);
    }

    // ───────────────────────────── ErrorResult ─────────────────────────────

    [Fact]
    public void ErrorResult_Create_WithoutMetadata()
    {
        var e = ErrorResult.Create(ErrorType.Validation, "c", "m");
        Assert.Equal(ErrorType.Validation, e.Type);
        Assert.Equal("c", e.Code);
        Assert.Equal("m", e.Message);
        Assert.Null(e.Metadata);
        Assert.Equal("c: m", e.ToString());
    }

    [Fact]
    public void ErrorResult_Create_WithMetadata()
    {
        var meta = new Dictionary<string, object?> { ["k"] = 1 };
        var e = ErrorResult.Create(ErrorType.Validation, "c", "m", meta);
        Assert.Same(meta, e.Metadata);
    }
}
