using CleanIT.Core.Models;

namespace CleanIT.Core.Abstractions;

/// <summary>
/// Deletes a single file using a given strategy (Recycle Bin, permanent, or no-op for dry-run).
/// Implementations must never throw for expected I/O failures: they report them in the result.
/// </summary>
public interface IFileDeleter
{
    CleanupMode Mode { get; }

    DeletionResult DeleteFile(string fullPath);
}
