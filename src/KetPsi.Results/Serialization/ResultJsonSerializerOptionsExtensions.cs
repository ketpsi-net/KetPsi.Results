using System.Text.Json;

namespace KetPsi.Results.Serialization;

/// <summary>
/// Extension methods for registering KetPsi.Results System.Text.Json converters.
/// </summary>
public static class ResultJsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds <see cref="Result"/> / <see cref="Result{T}"/> / <see cref="ErrorResult"/>
    /// JSON converters to <paramref name="options"/>. Safe to call more than once;
    /// duplicate factory instances are avoided when one is already present.
    /// </summary>
    /// <param name="options">The serializer options to mutate.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    public static JsonSerializerOptions AddKetPsiResultConverters(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var converter in options.Converters)
        {
            if (converter is ResultJsonConverterFactory)
                return options;
        }

        options.Converters.Add(new ResultJsonConverterFactory());
        return options;
    }
}