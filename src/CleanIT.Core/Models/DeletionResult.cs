namespace CleanIT.Core.Models;

public enum CleanupErrorKind
{
    Locked,
    AccessDenied,
    PathTooLong,
    Protected,
    Excluded,
    OutsideRuleRoots,
    ReparsePoint,

    /// <summary>The file disappeared between the scan and the cleanup.</summary>
    Vanished,
    Unknown,
}

/// <summary>Result of deleting one file.</summary>
public readonly record struct DeletionResult(bool Success, CleanupErrorKind? ErrorKind, string? Message)
{
    public static DeletionResult Ok { get; } = new(true, null, null);

    public static DeletionResult Fail(CleanupErrorKind kind, string message) => new(false, kind, message);
}
