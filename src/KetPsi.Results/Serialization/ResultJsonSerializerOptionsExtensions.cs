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
    /// <remarks>
    /// The result types also carry <c>[JsonConverter]</c> attributes, so this call is optional. It is kept so
    /// existing setups keep working and so the converters apply even where a resolver ignores type attributes.
    /// </remarks>
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

    /// <summary>
    /// Adds the converters from <see cref="AddKetPsiResultConverters(JsonSerializerOptions)"/> plus statically
    /// created converters for <see cref="Result{T}"/>, <see cref="SuccessResult{T}"/>,
    /// <see cref="InProgressResult{T}"/> and <see cref="FailedResult{T}"/> with the given <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Only needed for Native AOT on .NET 9 and 10, where the factory cannot build generic converters at run
    /// time: call it once for every payload type you serialize, e.g. <c>Result&lt;OrderDto&gt;</c> and
    /// <c>Result&lt;int&gt;</c>. On .NET 11 the generic converters are created statically from the types'
    /// attributes and this call is unnecessary (but harmless). Safe to call more than once for the same type.
    /// </remarks>
    /// <typeparam name="T">The result payload type.</typeparam>
    /// <param name="options">The serializer options to mutate.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    public static JsonSerializerOptions AddKetPsiResultConverters<T>(this JsonSerializerOptions options)
    {
        options.AddKetPsiResultConverters();

        foreach (var converter in options.Converters)
        {
            if (converter is ResultOfTJsonConverter<T>)
                return options;
        }

        // Inserted ahead of the factory: System.Text.Json uses the first converter in the list that can
        // convert the type.
        options.Converters.Insert(0, new ResultOfTJsonConverter<T>());
        options.Converters.Insert(1, new SuccessResultJsonConverter<T>());
        options.Converters.Insert(2, new InProgressResultJsonConverter<T>());
        options.Converters.Insert(3, new FailedResultJsonConverter<T>());
        return options;
    }
}
