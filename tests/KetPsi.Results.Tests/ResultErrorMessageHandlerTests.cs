using Xunit;

namespace KetPsi.Results.Tests;

/// <summary>
/// Comprehensive tests for the interpolated string handler:
/// @Key, standard formats, mask ranges, trunc, combinations, and edge cases.
/// </summary>
public class ResultErrorMessageHandlerTests
{
    private static FailedResult Fail(ref ResultErrorMessageHandler msg)
        => Result.Failed(ErrorType.Validation, "test.code", ref msg);

    // ───────────────────────────── Basic ─────────────────────────────

    [Fact]
    public void NoFormat_UsesExpressionNameAndDefaultToString()
    {
        int answer = 42;
        var result = Fail($"The answer is {answer}");
        Assert.Equal("The answer is 42", result.FirstError()!.Message);
        Assert.Equal(42, result.FirstError()!.Metadata!["answer"]);
    }

    [Fact]
    public void NullValue_ProducesEmptyDisplay_StoresNull()
    {
        string? name = null;
        var result = Fail($"Hello {name}");
        Assert.Equal("Hello ", result.FirstError()!.Message);
        Assert.Null(result.FirstError()!.Metadata!["name"]);
    }

    [Fact]
    public void LiteralOnly_MetadataIsNull()
    {
        // No holes → handler still runs but Metadata stays null
        var result = Result.Failed(ErrorType.Validation, "test", "pure literal");
        Assert.Null(result.FirstError()!.Metadata);
    }

    // ───────────────────────────── @Key ─────────────────────────────

    [Fact]
    public void KeyOverride_ReplacesExpressionName()
    {
        int userId = 12345;
        var result = Fail($"User {userId:@id} not found");
        Assert.Equal("User 12345 not found", result.FirstError()!.Message);
        Assert.True(result.FirstError()!.Metadata!.ContainsKey("id"));
        Assert.False(result.FirstError()!.Metadata!.ContainsKey("userId"));
        Assert.Equal(12345, result.FirstError()!.Metadata!["id"]);
    }

    [Fact]
    public void KeyOverride_IsCaseSensitive()
    {
        int a = 1;
        var result = Fail($"{a:@Key}");
        Assert.True(result.FirstError()!.Metadata!.ContainsKey("Key"));
        Assert.False(result.FirstError()!.Metadata!.ContainsKey("key"));
    }

    [Fact]
    public void KeyOverride_WithStandardFormat()
    {
        decimal amount = 1234.567m;
        var result = Fail($"Total {amount:@total:N2}");
        Assert.Equal("Total 1,234.57", result.FirstError()!.Message);
        Assert.Equal(1234.567m, result.FirstError()!.Metadata!["total"]);
    }

    // ───────────────────────────── Standard format ─────────────────────────────

    [Fact]
    public void StandardNumericFormat_N0()
    {
        int n = 1234567;
        var result = Fail($"Count {n:N0}");
        Assert.Equal("Count 1,234,567", result.FirstError()!.Message);
    }

    [Fact]
    public void StandardDateFormat_PreservesColons()
    {
        var dt = new DateTime(2026, 9, 29, 14, 30, 0, DateTimeKind.Utc);
        var result = Fail($"At {dt:yyyy-MM-dd HH:mm}");
        Assert.Equal("At 2026-09-29 14:30", result.FirstError()!.Message);
        Assert.Equal(dt, result.FirstError()!.Metadata!["dt"]);
    }

    [Fact]
    public void StandardDateFormat_WithSeconds()
    {
        var dt = new DateTime(2026, 9, 29, 14, 30, 45, DateTimeKind.Utc);
        var result = Fail($"At {dt:yyyy-MM-dd HH:mm:ss}");
        Assert.Equal("At 2026-09-29 14:30:45", result.FirstError()!.Message);
    }

    [Fact]
    public void StandardDateFormat_ThenMask()
    {
        var dt = new DateTime(2026, 9, 29, 14, 30, 0, DateTimeKind.Utc);
        var result = Fail($"At {dt:yyyy-MM-dd HH:mm:mask}");
        Assert.Equal("At ***", result.FirstError()!.Message);
        Assert.Equal(dt, result.FirstError()!.Metadata!["dt"]);
    }

    [Fact]
    public void StandardFormat_OnNonFormattable_FallsBackToToString()
    {
        var obj = new object();
        var result = Fail($"X {obj:N0}"); // object is not IFormattable
        Assert.Contains(obj.ToString()!, result.FirstError()!.Message);
    }

    // ───────────────────────────── Full mask ─────────────────────────────

    [Fact]
    public void Mask_Full_ProducesThreeStars()
    {
        string secret = "super-secret-token";
        var result = Fail($"Token {secret:mask}");
        Assert.Equal("Token ***", result.FirstError()!.Message);
        Assert.Equal("super-secret-token", result.FirstError()!.Metadata!["secret"]);
    }

    [Fact]
    public void Mask_Full_OnShortString_CapsToLength()
    {
        string pin = "12";
        var result = Fail($"PIN {pin:mask}");
        Assert.Equal("PIN **", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_Full_OnEmptyString()
    {
        string empty = "";
        var result = Fail($"X {empty:mask}");
        Assert.Equal("X ", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_Full_OnNull()
    {
        string? secret = null;
        var result = Fail($"X {secret:mask}");
        Assert.Equal("X ", result.FirstError()!.Message);
        Assert.Null(result.FirstError()!.Metadata!["secret"]);
    }

    // ───────────────────────────── Mask ranges ─────────────────────────────

    [Fact]
    public void Mask_FromIndexToEnd()
    {
        string token = "abcdefghij";
        var result = Fail($"T {token:mask:2..}");
        Assert.Equal("T ab********", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_FromStartToIndex()
    {
        string token = "abcdefghij";
        var result = Fail($"T {token:mask:..4}");
        // masks [0..4) → first 4 chars become *
        Assert.Equal("T ****efghij", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_MiddleRange_WithNegativeEnd()
    {
        string token = "abcdefghij";
        var result = Fail($"T {token:mask:2..-2}");
        Assert.Equal("T ab******ij", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_ExactRange()
    {
        string token = "abcdefghij";
        var result = Fail($"T {token:mask:3..7}");
        Assert.Equal("T abc****hij", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_OutOfBounds_Clamped()
    {
        string token = "abc";
        var result = Fail($"T {token:mask:0..100}");
        Assert.Equal("T ***", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_NegativeBeyondLength_Clamped()
    {
        string s = "abc";
        var result = Fail($"S {s:mask:-10..}");
        Assert.Equal("S ***", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_EmptyRange_NoChange()
    {
        string token = "abc";
        var result = Fail($"T {token:mask:2..2}");
        Assert.Equal("T abc", result.FirstError()!.Message);
    }

    [Fact]
    public void Mask_ReversedRange_Swapped()
    {
        string token = "abcdefghij";
        // 7..3 should be treated as 3..7
        var result = Fail($"T {token:mask:7..3}");
        Assert.Equal("T abc****hij", result.FirstError()!.Message);
    }

    // ───────────────────────────── trunc (from end) ─────────────────────────────

    [Fact]
    public void Trunc_KeepsFirstN_RemovesFromEnd()
    {
        string name = "xxyy";
        var result = Fail($"N {name:trunc:2}");
        Assert.Equal("N xx", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_LongerString_KeepsPrefix()
    {
        string name = "HelloWorldExtra";
        var result = Fail($"N {name:trunc:5}");
        Assert.Equal("N Hello", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_WhenNLargerThanLength_ReturnsOriginal()
    {
        string name = "Hi";
        var result = Fail($"N {name:trunc:10}");
        Assert.Equal("N Hi", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_Zero_ReturnsEmpty()
    {
        string name = "Hello";
        var result = Fail($"N {name:trunc:0}");
        Assert.Equal("N ", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_Bare_IsNoOp()
    {
        string name = "Hello";
        var result = Fail($"N {name:trunc}");
        Assert.Equal("N Hello", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_OnEmpty()
    {
        string empty = "";
        var result = Fail($"N {empty:trunc:3}");
        Assert.Equal("N ", result.FirstError()!.Message);
    }

    [Fact]
    public void Trunc_OnNull()
    {
        string? name = null;
        var result = Fail($"N {name:trunc:3}");
        Assert.Equal("N ", result.FirstError()!.Message);
    }

    // ───────────────────────────── Combined directives ─────────────────────────────

    [Fact]
    public void Combined_Key_Format_Mask()
    {
        decimal amount = 1234.5m;
        var result = Fail($"Pay {amount:@amt:N0:mask}");
        Assert.Equal("Pay ***", result.FirstError()!.Message);
        Assert.Equal(1234.5m, result.FirstError()!.Metadata!["amt"]);
    }

    [Fact]
    public void Combined_Key_Mask_First4()
    {
        string secret = "my-super-secret-value";
        var result = Fail($"S {secret:@pwd:mask:..4}");
        // masks [0..4) of "my-super-secret-value"
        Assert.Equal("S ****uper-secret-value", result.FirstError()!.Message);
        Assert.Equal("my-super-secret-value", result.FirstError()!.Metadata!["pwd"]);
    }

    [Fact]
    public void Combined_Trunc_Then_Mask()
    {
        string data = "abcdefghijklmnop";
        // trunc:8 → "abcdefgh", then mask:2..6 → "ab****gh"
        var result = Fail($"D {data:trunc:8:mask:2..6}");
        Assert.Equal("D ab****gh", result.FirstError()!.Message);
    }

    [Fact]
    public void Combined_Format_Then_Trunc()
    {
        var dt = new DateTime(2026, 9, 29, 15, 45, 30, DateTimeKind.Utc);
        var result = Fail($"D {dt:yyyy-MM-dd HH:mm:ss:trunc:10}");
        // formatted = "2026-09-29 15:45:30" (19 chars), trunc:10 keeps first 10
        Assert.Equal("D 2026-09-29", result.FirstError()!.Message);
    }

    // ───────────────────────────── Multiple holes ─────────────────────────────

    [Fact]
    public void MultipleHoles_AllCaptured()
    {
        int id = 42;
        string name = "Alice";
        var result = Fail($"User {id:@userId} named {name:mask:1..} failed");
        Assert.Equal("User 42 named A**** failed", result.FirstError()!.Message);
        Assert.Equal(42, result.FirstError()!.Metadata!["userId"]);
        Assert.Equal("Alice", result.FirstError()!.Metadata!["name"]);
    }

    [Fact]
    public void MultipleHoles_MixedDirectives()
    {
        string token = "abcdefghij";
        int count = 3;
        var result = Fail($"t={token:mask:..4} n={count:N0}");
        Assert.Equal("t=****efghij n=3", result.FirstError()!.Message);
        Assert.Equal("abcdefghij", result.FirstError()!.Metadata!["token"]);
        Assert.Equal(3, result.FirstError()!.Metadata!["count"]);
    }
}
