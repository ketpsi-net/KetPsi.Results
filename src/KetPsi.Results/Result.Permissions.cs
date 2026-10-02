namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for permission-related failures.
    /// </summary>
    public static class Permissions
    {
        private const string ForbiddenCode = "permissions.forbidden";

        private static readonly FailedResult _forbidden =
            new(ErrorType.Forbidden, ForbiddenCode, "You do not have the required permissions to perform this action.");

        /// <summary>Creates a standard forbidden failure.</summary>
        public static FailedResult Forbidden() => _forbidden;

        /// <summary>Creates a forbidden failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult Forbidden(string message)
            => new(ErrorType.Forbidden, ForbiddenCode, message);

        /// <summary>Creates a forbidden failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult Forbidden(ref ResultErrorMessageHandler message)
            => new(ErrorType.Forbidden, ForbiddenCode, message.GetFormattedText(), message.Metadata);
    }
}
