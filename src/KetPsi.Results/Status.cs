namespace KetPsi.Results;

/// <summary>
/// Represents the fundamental operational state of a result.
/// </summary>
public enum Status : byte
{
    /// <summary>The operation completed successfully.</summary>
    Success = 1,

    /// <summary>The operation failed and contains error details.</summary>
    Failed = 2,

    /// <summary>The operation has been accepted and is currently in progress.</summary>
    InProgress = 3
}
