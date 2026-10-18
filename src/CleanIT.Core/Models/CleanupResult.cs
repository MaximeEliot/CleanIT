namespace CleanIT.Core.Models;

/// <summary>A file that could not be deleted (or was deliberately not deleted).</summary>
public sealed record CleanupError(string RuleId, string FullPath, CleanupErrorKind Kind, string Message);

/// <summary>Per-category summary of a cleanup.</summary>
public sealed record CategoryCleanupResult(string RuleId, string DisplayName, long BytesFreed, long ItemsDeleted, int ErrorCount);

/// <summary>Final summary of a cleanup (or a dry-run).</summary>
public sealed class CleanupResult
{
    public CleanupResult(
        CleanupMode mode,
        long bytesFreed,
        long itemsDeleted,
        int directoriesRemoved,
        IReadOnlyList<CategoryCleanupResult> categories,
        IReadOnlyList<CleanupError> errors,
        IReadOnlyList<string> dryRunEntries,
        bool wasCancelled,
        TimeSpan duration)
    {
        Mode = mode;
        BytesFreed = bytesFreed;
        ItemsDeleted = itemsDeleted;
        DirectoriesRemoved = directoriesRemoved;
        Categories = categories;
        Errors = errors;
        DryRunEntries = dryRunEntries;
        WasCancelled = wasCancelled;
        Duration = duration;
    }

    public CleanupMode Mode { get; }

    /// <summary>
    /// Bytes deleted (or that would be, in dry-run). In Recycle Bin mode the space is only
    /// actually released once the Recycle Bin is emptied.
    /// </summary>
    public long BytesFreed { get; }

    public long ItemsDeleted { get; }

    /// <summary>Empty directories removed after their files were deleted.</summary>
    public int DirectoriesRemoved { get; }

    public IReadOnlyList<CategoryCleanupResult> Categories { get; }

    public IReadOnlyList<CleanupError> Errors { get; }

    /// <summary>In dry-run mode: every path that would have been deleted.</summary>
    public IReadOnlyList<string> DryRunEntries { get; }

    public bool WasCancelled { get; }

    public TimeSpan Duration { get; }
}
