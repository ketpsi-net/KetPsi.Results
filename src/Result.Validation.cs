namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for validation and input failures.
    /// </summary>
    public static class Validation
    {
        private const string InvalidInputCode = "validation.invalid_input";

        private static readonly FailedResult _invalidInput =
            new(ErrorType.Validation, InvalidInputCode, "The provided input is invalid or malformed.");

        /// <summary>Creates a standard invalid-input failure.</summary>
        public static FailedResult InvalidInput() => _invalidInput;

        /// <summary>Creates an invalid-input failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult InvalidInput(string message)
            => new(ErrorType.Validation, InvalidInputCode, message);

        /// <summary>Creates an invalid-input failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult InvalidInput(ref ResultErrorMessageHandler message)
            => new(ErrorType.Validation, InvalidInputCode, message.GetFormattedText(), message.Metadata);
    }
}
