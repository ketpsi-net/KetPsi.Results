#if NET11_0_OR_GREATER
using Xunit;

namespace KetPsi.Results.Tests;

/// <summary>
/// On .NET 11, <see cref="Result{T}"/> is a C# 15 <c>closed</c> record. The switches below have no
/// discard arm; the test project treats CS8509 as an error, so they only compile while the
/// hierarchy stays closed over exactly these three cases.
/// </summary>
public sealed class ResultClosedHierarchyTests
{
    private static string Describe(Result<int> result) => result switch
    {
        SuccessResult<int> success => $"success:{success.Value}",
        InProgressResult<int> inProgress => $"in-progress:{inProgress.Value}",
        FailedResult<int> failed => $"failed:{failed.Errors[0].Code}",
    };

    [Fact]
    public void SwitchExpression_WithoutDiscard_CoversEveryCase()
    {
        Assert.Equal("success:1", Describe(1));
        Assert.Equal("in-progress:2", Describe(Result.InProgress(2)));
        Assert.Equal("failed:resource.not_found", Describe(Result.Resource.NotFound("Order")));
    }

    [Fact]
    public void SwitchStatement_WithoutDefault_CoversEveryCase()
    {
        Result<string> result = Result.Validation.InvalidInput("Name is required.");

        var reached = string.Empty;
        switch (result)
        {
            case SuccessResult<string>:
                reached = "success";
                break;
            case InProgressResult<string>:
                reached = "in-progress";
                break;
            case FailedResult<string>:
                reached = "failed";
                break;
        }

        Assert.Equal("failed", reached);
    }

    [Fact]
    public void ExistingDiscardArm_StillCompilesAndIsNeverReached()
    {
        Result<int> result = 5;

        var text = result switch
        {
            SuccessResult<int> s => s.Value.ToString(),
            InProgressResult<int> => "in-progress",
            FailedResult<int> => "failed",
            _ => "unreachable",
        };

        Assert.Equal("5", text);
    }

    [Fact]
    public void GenericResult_IsMarkedClosedOverItsThreeCases()
    {
        var attribute = typeof(Result<>).GetCustomAttributesData()
            .Single(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsClosedTypeAttribute");

        var derived = attribute.NamedArguments.Single(a => a.MemberName == "DerivedTypes").TypedValue.Value
            as IReadOnlyCollection<System.Reflection.CustomAttributeTypedArgument>;

        Assert.NotNull(derived);
        Assert.Equivalent(
            new[] { typeof(SuccessResult<>), typeof(InProgressResult<>), typeof(FailedResult<>) },
            derived.Select(d => (Type)d.Value!).ToArray());
    }
}
#endif
