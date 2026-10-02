namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for resource-related failures (not-found, already-exists).
    /// </summary>
    public static class Resource
    {
        private const string NotFoundCode = "resource.not_found";
        private const string AlreadyExistsCode = "resource.already_exists";

        private static readonly FailedResult _notFound =
            new(ErrorType.NotFound, NotFoundCode, "The requested resource was not found.");

        private static readonly FailedResult _alreadyExists =
            new(ErrorType.Conflict, AlreadyExistsCode, "The resource already exists and cannot be duplicated.");

        /// <summary>Creates a standard not-found failure.</summary>
        public static FailedResult NotFound() => _notFound;

        /// <summary>Creates a not-found failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult NotFound(string message)
            => new(ErrorType.NotFound, NotFoundCode, message);

        /// <summary>Creates a not-found failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult NotFound(ref ResultErrorMessageHandler message)
            => new(ErrorType.NotFound, NotFoundCode, message.GetFormattedText(), message.Metadata);

        /// <summary>Creates a standard already-exists failure.</summary>
        public static FailedResult AlreadyExists() => _alreadyExists;

        /// <summary>Creates an already-exists failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult AlreadyExists(string message)
            => new(ErrorType.Conflict, AlreadyExistsCode, message);

        /// <summary>Creates an already-exists failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult AlreadyExists(ref ResultErrorMessageHandler message)
            => new(ErrorType.Conflict, AlreadyExistsCode, message.GetFormattedText(), message.Metadata);
    }
}
