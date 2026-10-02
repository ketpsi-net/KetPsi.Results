namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for external service / integration failures.
    /// </summary>
    public static class Service
    {

        private const string DefaultUnavailableMessage =
            "A required downstream service or dependency is currently unavailable.";

        private const string DefaultFailureMessage =
            "An external service reported a failure.";

        /// <summary>
        /// Builds a stable integration error code:
        /// <c>integration.{serviceName}</c>, or <c>integration.{serviceName}.{code}</c> when <paramref name="code"/> is provided.
        /// </summary>
        private static string BuildIntegrationCode(string serviceName, string? code)
        {
            var prefix = $"integration.{serviceName}";
            return string.IsNullOrEmpty(code) ? prefix : $"{prefix}.{code}";
        }

        // ───────────────────────────── Unavailable ─────────────────────────────

        /// <summary>
        /// Creates a service-unavailable failure for the given service .
        /// </summary>
        public static FailedResult Unavailable(Func<Result.Service.ServiceFailureBuilder, ServiceFailureBuilder> builder)
            => builder(new ServiceFailureBuilder(ErrorType.DependencyFailure)).Build();

        /// <summary>
        /// Creates a service-unavailable failure for the given service (default message, no sub-code).
        /// </summary>
        public static FailedResult Unavailable(string serviceName)
            => new(ErrorType.DependencyFailure, BuildIntegrationCode(serviceName, null), DefaultUnavailableMessage);

        /// <summary>
        /// Creates a service-unavailable failure with an optional sub-code (default message).
        /// </summary>
        public static FailedResult Unavailable(string serviceName, string? code)
            => new(ErrorType.DependencyFailure, BuildIntegrationCode(serviceName, code), DefaultUnavailableMessage);

        /// <summary>
        /// Creates a service-unavailable failure with a custom message.
        /// </summary>
        public static FailedResult Unavailable(string serviceName, string? code, string message)
            => new(ErrorType.DependencyFailure, BuildIntegrationCode(serviceName, code), message);

        /// <summary>
        /// Creates a service-unavailable failure using an interpolated message handler.
        /// </summary>
        public static FailedResult Unavailable(string serviceName, string? code, ref ResultErrorMessageHandler message)
            => new(ErrorType.DependencyFailure, BuildIntegrationCode(serviceName, code), message.GetFormattedText(), message.Metadata);

        // ───────────────────────────── Failure ─────────────────────────────

        /// <summary>
        /// Creates a service-failure result for the given service (default message, no sub-code).
        /// </summary>
        public static FailedResult Failure(Func<ServiceFailureBuilder, ServiceFailureBuilder> builder)
            => builder(new ServiceFailureBuilder(ErrorType.Failure)).Build();

        /// <summary>
        /// Creates a service-failure result for the given service (default message, no sub-code).
        /// </summary>
        public static FailedResult Failure(string serviceName)
            => new(ErrorType.Failure, BuildIntegrationCode(serviceName, null), DefaultFailureMessage);

        /// <summary>
        /// Creates a service-failure result with an optional sub-code (default message).
        /// </summary>
        public static FailedResult Failure(string serviceName, string? code)
            => new(ErrorType.Failure, BuildIntegrationCode(serviceName, code), DefaultFailureMessage);

        /// <summary>
        /// Creates a service-failure result with a custom message.
        /// </summary>
        public static FailedResult Failure(string serviceName, string? code, string message)
            => new(ErrorType.Failure, BuildIntegrationCode(serviceName, code), message);

        /// <summary>
        /// Creates a service-failure result using an interpolated message handler.
        /// </summary>
        public static FailedResult Failure(string serviceName, string? code, ref ResultErrorMessageHandler message)
            => new(ErrorType.Failure, BuildIntegrationCode(serviceName, code), message.GetFormattedText(), message.Metadata);

        /// <summary>
        /// Fluent builder for external service / integration <see cref="FailedResult"/> instances.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Used by <see cref="Unavailable(Func{ServiceFailureBuilder, ServiceFailureBuilder})"/>
        /// and <see cref="Failure(Func{ServiceFailureBuilder, ServiceFailureBuilder})"/>.
        /// The initial <see cref="ErrorType"/> is supplied by those factories
        /// (<see cref="ErrorType.DependencyFailure"/> for unavailable,
        /// <see cref="ErrorType.Failure"/> for failure).
        /// </para>
        /// <para>
        /// The error <c>Code</c> is derived at build time as
        /// <c>integration.{serviceName}</c>, or <c>integration.{serviceName}.{code}</c>
        /// when a sub-code was set via <see cref="WithCode"/>.
        /// </para>
        /// <para>
        /// Defaults before configuration: empty service name, empty sub-code, empty message,
        /// and an empty metadata dictionary. Callers should set at least a service name
        /// (and usually a message) before the result leaves the integration boundary.
        /// </para>
        /// </remarks>
        public class ServiceFailureBuilder
        {

            private string _serviceName;
            private string _code;
            private string _message;
            private readonly ErrorType _errorType;
            private readonly Dictionary<string, object?> _metadata;

            /// <summary>
            /// Initializes a new builder with the given structural error type.
            /// </summary>
            /// <param name="errorType">
            /// Typically <see cref="ErrorType.DependencyFailure"/> (unavailable)
            /// or <see cref="ErrorType.Failure"/> (service-reported failure).
            /// </param>
            internal ServiceFailureBuilder(ErrorType errorType)
            {
                _serviceName = string.Empty;
                _code = string.Empty;
                _metadata = [];
                _message = string.Empty;
                _errorType = errorType;
            }

            /// <summary>
            /// Sets the logical external service name used in the integration error code
            /// (e.g. <c>keycloak</c> → <c>integration.keycloak</c>).
            /// </summary>
            /// <param name="serviceName">Service identifier segment of the error code.</param>
            /// <returns>This builder for chaining.</returns>
            public ServiceFailureBuilder WithServiceName(string serviceName)
            {
                _serviceName = serviceName;
                return this;
            }

            /// <summary>
            /// Sets an optional service-specific sub-code appended to the integration code
            /// (e.g. <c>invalid_grant</c> → <c>integration.keycloak.invalid_grant</c>).
            /// </summary>
            /// <param name="code">Service-defined error code; ignored when null or empty.</param>
            /// <returns>This builder for chaining.</returns>
            public ServiceFailureBuilder WithCode(string code)
            {
                _code = code;
                return this;
            }

            /// <summary>
            /// Sets the human-readable error message.
            /// </summary>
            /// <param name="message">Message text shown to callers / logs.</param>
            /// <returns>This builder for chaining.</returns>
            public ServiceFailureBuilder WithMessage(string message)
            {
                _message = message;
                return this;
            }
            /// <summary>
            /// Sets the message from an interpolated handler and merges any captured metadata.
            /// </summary>
            /// <param name="message">
            /// Handler that provides formatted text and optional metadata entries.
            /// Existing metadata keys are overwritten on conflict.
            /// </param>
            /// <returns>This builder for chaining.</returns>
            public ServiceFailureBuilder WithMessage(ref ResultErrorMessageHandler message)
            {
                _message = message.GetFormattedText();
                if (message.Metadata is not null)
                    foreach (var kvp in message.Metadata)
                    {
                        _metadata[kvp.Key] = kvp.Value;
                    }

                return this;
            }
            /// <summary>
            /// Adds or replaces a metadata entry associated with the error.
            /// </summary>
            /// <param name="key">Metadata key.</param>
            /// <param name="value">Raw value (stored unmasked for diagnostics).</param>
            /// <returns>This builder for chaining.</returns>
            public ServiceFailureBuilder AddMetadata(string key, object? value)
            {
                _metadata[key] = value;
                return this;
            }

            /// <summary>
            /// Builds a <see cref="FailedResult"/> using the configured type, integration code,
            /// message, and metadata.
            /// </summary>
            /// <returns>A non-generic failed result.</returns>
            internal FailedResult Build()
            {
                return new FailedResult(_errorType, BuildIntegrationCode(_serviceName, _code), _message, _metadata);
            }
        }
    }
}