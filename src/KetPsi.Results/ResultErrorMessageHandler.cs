using System.Globalization;
using System.Runtime.CompilerServices;

namespace KetPsi.Results;

/// <summary>
/// High-performance interpolated string handler that captures raw values into metadata
/// while building a human-readable error message.
/// </summary>
/// <remarks>
/// <para>
/// Format syntax (colon-separated, left-to-right):
/// </para>
/// <code>
/// [@Key:][standardFormat][:mask[:range]][:trunc:N]
/// </code>
/// <para>
/// <b>Examples</b>
/// <list type="bullet">
///   <item><c>{id}</c> – expression name becomes the metadata key</item>
///   <item><c>{id:@userId}</c> – metadata key forced to "userId"</item>
///   <item><c>{amount:N2}</c> – standard .NET format</item>
///   <item><c>{token:mask}</c> – full mask (***)</item>
///   <item><c>{token:mask:2..}</c> – mask from index 2 to end</item>
///   <item><c>{token:mask:..4}</c> – mask from start to index 4</item>
///   <item><c>{token:mask:2..-2}</c> – mask from index 2 to second-to-last</item>
///   <item><c>{name:trunc:8}</c> – keep first 8 characters (remove from the end)</item>
///   <item><c>{secret:@pwd:mask:..4}</c> – key override + mask first 4 chars</item>
/// </list>
/// </para>
/// <para>
/// Range syntax is Python-style: <c>start..end</c>, <c>..end</c>, <c>start..</c>.
/// Negative indices count from the end. Parsing is span-based and allocation-free
/// on the common path.
/// </para>
/// </remarks>
[InterpolatedStringHandler]
public ref struct ResultErrorMessageHandler
{
    private DefaultInterpolatedStringHandler _builder;
    private Dictionary<string, object?>? _metadata;

    /// <summary>
    /// Raw values captured during interpolation.
    /// Keys are the expression name or an explicit <c>@Key</c> override.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata => _metadata;

    /// <summary>
    /// Initializes a new instance of the handler.
    /// </summary>
    /// <param name="literalLength">Total length of literal segments.</param>
    /// <param name="formattedCount">Number of interpolation holes.</param>
    public ResultErrorMessageHandler(int literalLength, int formattedCount)
    {
        _builder = new DefaultInterpolatedStringHandler(
            literalLength,
            formattedCount,
            CultureInfo.InvariantCulture);
        _metadata = null;
    }

    /// <summary>Appends a literal segment.</summary>
    public void AppendLiteral(string s) => _builder.AppendLiteral(s);

    /// <summary>
    /// Appends a value, optionally applying format / mask / trunc directives,
    /// and always stores the raw value in metadata.
    /// </summary>
    public void AppendFormatted<T>(
        T value,
        string? format = null,
        [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        // Fast path – no format
        if (string.IsNullOrEmpty(format))
        {
            Store(expression, value);
            _builder.AppendFormatted(value);
            return;
        }

        Parse(format.AsSpan(),
            out var keyOverride,
            out var standardFormat,
            out var maskRange,
            out var truncLen);

        string key = keyOverride.Length > 0 ? keyOverride.ToString() : expression;
        Store(key, value);

        string display = FormatValue(value, standardFormat);

        if (truncLen is not null)
            display = ApplyTrunc(display, truncLen.Value);

        if (maskRange.HasValue)
            display = ApplyMask(display, maskRange.Value);

        _builder.AppendLiteral(display);
    }

    /// <summary>Returns the finished message and resets the builder.</summary>
    public string GetFormattedText() => _builder.ToStringAndClear();

    // ───────────────────────────── private helpers ─────────────────────────────

    private void Store(string key, object? value)
    {
        _metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        _metadata[key] = value;
    }

    private static void Parse(
        ReadOnlySpan<char> format,
        out ReadOnlySpan<char> keyOverride,
        out ReadOnlySpan<char> standardFormat,
        out RangeSpec? maskRange,
        out int? truncLen)
    {
        keyOverride = default;
        standardFormat = default;
        maskRange = null;
        truncLen = null;

        // Contiguous region of the original span that forms the standard format.
        // This preserves embedded colons (e.g. "yyyy-MM-dd HH:mm:ss") without
        // allocating on the common single-token path.
        int fmtStart = -1;
        int fmtEnd = -1;

        int pos = 0;
        bool first = true;

        while (pos < format.Length)
        {
            int colon = format[pos..].IndexOf(':');
            ReadOnlySpan<char> part = colon < 0
                ? format[pos..]
                : format.Slice(pos, colon);

            int advance = colon < 0 ? format.Length - pos : colon + 1;
            int partEnd = pos + (colon < 0 ? part.Length : colon);

            if (part.Length == 0)
            {
                pos += advance;
                first = false;
                continue;
            }

            // @Key (only meaningful as first token)
            if (first && part[0] == '@')
            {
                keyOverride = part[1..];
                pos += advance;
                first = false;
                continue;
            }

            // mask / mask:range
            if (part.Equals("mask", StringComparison.OrdinalIgnoreCase))
            {
                if (fmtStart >= 0 && standardFormat.IsEmpty)
                {
                    standardFormat = format[fmtStart..fmtEnd];
                    fmtStart = -1;
                    fmtEnd = -1;
                }

                if (TryConsumeRange(format, ref pos, colon, out var r))
                {
                    maskRange = r;
                    first = false;
                    continue;
                }
                maskRange = new RangeSpec(full: true);
                pos += advance;
                first = false;
                continue;
            }

            // trunc:N  (keeps the first N characters, removes from the end)
            if (part.Equals("trunc", StringComparison.OrdinalIgnoreCase))
            {
                if (fmtStart >= 0 && standardFormat.IsEmpty)
                {
                    standardFormat = format[fmtStart..fmtEnd];
                    fmtStart = -1;
                    fmtEnd = -1;
                }

                if (colon >= 0)
                {
                    int next = pos + colon + 1;
                    int nextColon = format[next..].IndexOf(':');
                    ReadOnlySpan<char> numSpan = nextColon < 0
                        ? format[next..]
                        : format.Slice(next, nextColon);

                    if (int.TryParse(numSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0)
                    {
                        truncLen = n;
                        pos = nextColon < 0 ? format.Length : next + nextColon + 1;
                        first = false;
                        continue;
                    }
                }
                // bare "trunc" is a no-op
                pos += advance;
                first = false;
                continue;
            }

            // Non-keyword → extend the standard-format region (preserves colons).
            if (fmtStart < 0)
                fmtStart = pos;
            fmtEnd = partEnd;

            pos += advance;
            first = false;
        }

        if (fmtStart >= 0 && standardFormat.IsEmpty)
            standardFormat = format[fmtStart..fmtEnd];
    }

    private static bool TryConsumeRange(
        ReadOnlySpan<char> format,
        ref int pos,
        int colonOffset,
        out RangeSpec range)
    {
        range = default;
        if (colonOffset < 0) return false;

        int start = pos + colonOffset + 1;
        if (start >= format.Length) return false;

        int nextColon = format[start..].IndexOf(':');
        ReadOnlySpan<char> rangePart = nextColon < 0
            ? format[start..]
            : format.Slice(start, nextColon);

        if (!TryParseRange(rangePart, out range))
            return false;

        pos = nextColon < 0 ? format.Length : start + nextColon + 1;
        return true;
    }

    private static bool TryParseRange(ReadOnlySpan<char> span, out RangeSpec range)
    {
        range = default;
        if (span.IsEmpty) return false;

        int dots = span.IndexOf("..");
        if (dots < 0) return false;

        ReadOnlySpan<char> left = span[..dots];
        ReadOnlySpan<char> right = span[(dots + 2)..];

        int? s = null;
        int? e = null;

        if (!left.IsEmpty)
        {
            if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return false;
            s = v;
        }

        if (!right.IsEmpty)
        {
            if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return false;
            e = v;
        }

        range = new RangeSpec(s, e, full: false);
        return true;
    }

    private static string FormatValue<T>(T value, ReadOnlySpan<char> standardFormat)
    {
        if (value is null) return string.Empty;
        if (standardFormat.IsEmpty) return value.ToString() ?? string.Empty;

        if (value is IFormattable f)
            return f.ToString(standardFormat.ToString(), CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    /// <summary>Keeps the first <paramref name="keep"/> characters (removes from the end).</summary>
    private static string ApplyTrunc(string input, int keep)
    {
        if (string.IsNullOrEmpty(input) || keep >= input.Length) return input;
        if (keep <= 0) return string.Empty;
        return input[..keep];
    }

    private static string ApplyMask(string input, RangeSpec range)
    {
        if (string.IsNullOrEmpty(input)) return input;

        int len = input.Length;

        if (range.Full)
            return new string('*', Math.Min(len, 3));

        int start = range.Start ?? 0;
        int end = range.End ?? len;

        if (start < 0) start = Math.Max(0, len + start);
        if (end < 0) end = Math.Max(0, len + end);

        start = Math.Clamp(start, 0, len);
        end = Math.Clamp(end, 0, len);
        if (start > end) (start, end) = (end, start);

        if (end - start <= 0) return input;

        return string.Create(len, (input, start, end), static (span, state) =>
        {
            state.input.AsSpan().CopyTo(span);
            span[state.start..state.end].Fill('*');
        });
    }

    private readonly struct RangeSpec
    {
        public readonly int? Start;
        public readonly int? End;
        public readonly bool Full;

        public RangeSpec(int? start, int? end, bool full)
        {
            Start = start;
            End = end;
            Full = full;
        }

        public RangeSpec(bool full) : this(null, null, full) { }
    }
}
