namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for authentication-related failures.
    /// </summary>
    public static class Authentication
    {
        private const string UnauthorizedCode = "auth.unauthorized";
        private const string TokenExpiredCode = "auth.token_expired";

        private static readonly FailedResult _unauthorized =
            new(ErrorType.Unauthorized, UnauthorizedCode, "Authentication is required to access this resource.");

        private static readonly FailedResult _tokenExpired =
            new(ErrorType.Unauthorized, TokenExpiredCode, "The provided authentication token has expired.");

        /// <summary>Creates a standard unauthorized failure.</summary>
        public static FailedResult Unauthorized() => _unauthorized;

        /// <summary>Creates an unauthorized failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult Unauthorized(string message)
            => new(ErrorType.Unauthorized, UnauthorizedCode, message);

        /// <summary>Creates an unauthorized failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult Unauthorized(ref ResultErrorMessageHandler message)
            => new(ErrorType.Unauthorized, UnauthorizedCode, message.GetFormattedText(), message.Metadata);

        /// <summary>Creates a standard token-expired failure.</summary>
        public static FailedResult TokenExpired() => _tokenExpired;

        /// <summary>Creates a token-expired failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult TokenExpired(string message)
            => new(ErrorType.Unauthorized, TokenExpiredCode, message);

        /// <summary>Creates a token-expired failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult TokenExpired(ref ResultErrorMessageHandler message)
            => new(ErrorType.Unauthorized, TokenExpiredCode, message.GetFormattedText(), message.Metadata);
    }
}
