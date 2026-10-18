namespace CleanIT.Core.Models;

/// <summary>Parameters of a cleanup, built from a <see cref="ScanReport"/>.</summary>
public sealed record CleanupRequest
{
    /// <summary>Rules (from the report) to clean. Others are ignored.</summary>
    public required IReadOnlySet<string> RuleIds { get; init; }

    public CleanupMode Mode { get; init; } = CleanupMode.RecycleBin;

    /// <summary>
    /// Must be set to true after the user explicitly confirmed. Mandatory for any mode
    /// other than <see cref="CleanupMode.DryRun"/>; the engine refuses to run otherwise.
    /// </summary>
    public bool UserConfirmed { get; init; }

    /// <summary>Current user exclusions, re-applied at cleanup time.</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];
}

/// <summary>Cleanup progress notification.</summary>
public sealed record CleanupProgress(long ProcessedItems, long TotalItems, long ProcessedBytes, string? CurrentCategory, string? CurrentPath);
