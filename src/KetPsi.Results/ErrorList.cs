using System.Collections.ObjectModel;

namespace KetPsi.Results;

/// <summary>
/// Helpers that keep the error list of a failed result valid and immutable.
/// </summary>
internal static class ErrorList
{
    /// <summary>
    /// Returns an immutable copy of <paramref name="errors"/>, or the same instance when it was
    /// already produced by this method (so forwarding a failure between result types does not re-copy).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The list is empty or contains <see langword="null"/>.</exception>
    public static IReadOnlyList<ErrorResult> Freeze(IReadOnlyList<ErrorResult>? errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors is Frozen)
            return errors;

        if (errors.Count == 0)
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));

        var copy = new ErrorResult[errors.Count];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = errors[i] ?? throw new ArgumentException("Errors cannot contain null.", nameof(errors));

        return new Frozen(copy);
    }

    private sealed class Frozen(IList<ErrorResult> items) : ReadOnlyCollection<ErrorResult>(items);
}
