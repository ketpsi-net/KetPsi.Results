namespace KetPsi.Results;

public partial record Result
{
    /// <summary>
    /// Factory methods for business-rule and domain-state failures.
    /// </summary>
    public static class Business
    {
        private const string RuleViolationCode = "business.rule_violation";
        private const string QuotaExceededCode = "business.quota_exceeded";
        private const string StateConflictCode = "business.state_conflict";

        private static readonly FailedResult _ruleViolation =
            new(ErrorType.RuleViolation, RuleViolationCode, "A domain business rule prevented this operation.");

        private static readonly FailedResult _quotaExceeded =
            new(ErrorType.QuotaExceeded, QuotaExceededCode, "The usage limit or quota for this operation has been exceeded.");

        private static readonly FailedResult _stateConflict =
            new(ErrorType.Conflict, StateConflictCode, "The operation cannot be completed due to a conflict with the current state of the system.");

        /// <summary>Creates a standard rule-violation failure.</summary>
        public static FailedResult RuleViolation() => _ruleViolation;

        /// <summary>Creates a rule-violation failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult RuleViolation(string message)
            => new(ErrorType.RuleViolation, RuleViolationCode, message);

        /// <summary>Creates a rule-violation failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult RuleViolation(ref ResultErrorMessageHandler message)
            => new(ErrorType.RuleViolation, RuleViolationCode, message.GetFormattedText(), message.Metadata);

        /// <summary>Creates a standard quota-exceeded failure.</summary>
        public static FailedResult QuotaExceeded() => _quotaExceeded;

        /// <summary>Creates a quota-exceeded failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult QuotaExceeded(string message)
            => new(ErrorType.QuotaExceeded, QuotaExceededCode, message);

        /// <summary>Creates a quota-exceeded failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult QuotaExceeded(ref ResultErrorMessageHandler message)
            => new(ErrorType.QuotaExceeded, QuotaExceededCode, message.GetFormattedText(), message.Metadata);

        /// <summary>Creates a standard state-conflict failure.</summary>
        public static FailedResult StateConflict() => _stateConflict;

        /// <summary>Creates a state-conflict failure with a custom message.</summary>
        /// <param name="message">The custom error message.</param>
        public static FailedResult StateConflict(string message)
            => new(ErrorType.Conflict, StateConflictCode, message);

        /// <summary>Creates a state-conflict failure using an interpolated message handler.</summary>
        /// <param name="message">The interpolated message handler.</param>
        public static FailedResult StateConflict(ref ResultErrorMessageHandler message)
            => new(ErrorType.Conflict, StateConflictCode, message.GetFormattedText(), message.Metadata);
    }
}
