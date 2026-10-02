namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for system-level and infrastructure failures.
    /// </summary>
    public static class System
    {
        private const string ExecutionCanceledCode = "system.execution_canceled";

        private static readonly FailedResult _internalError =
            new(ErrorType.Failure, InternalErrorCode, "An unexpected internal error occurred.");

        private static readonly FailedResult _executionCanceled =
            new(ErrorType.Canceled, ExecutionCanceledCode, "The operation was canceled before completion.");

        /// <summary>Creates a standard internal-error failure.</summary>
        public static FailedResult InternalError() => _internalError;

        /// <summary>Creates an internal-error failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult InternalError(string message)
            => new(ErrorType.Failure, InternalErrorCode, message);

        /// <summary>Creates an internal-error failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult InternalError(ref ResultErrorMessageHandler message)
            => new(ErrorType.Failure, InternalErrorCode, message.GetFormattedText(), message.Metadata);

        /// <summary>Creates a standard execution-canceled failure.</summary>
        public static FailedResult ExecutionCanceled() => _executionCanceled;

        /// <summary>Creates an execution-canceled failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult ExecutionCanceled(string message)
            => new(ErrorType.Canceled, ExecutionCanceledCode, message);

        /// <summary>Creates an execution-canceled failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult ExecutionCanceled(ref ResultErrorMessageHandler message)
            => new(ErrorType.Canceled, ExecutionCanceledCode, message.GetFormattedText(), message.Metadata);
    }
}