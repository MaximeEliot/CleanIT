namespace CleanIT.Core.Models;

/// <summary>
/// A directory a rule wants cleaned, with an optional file filter.
/// </summary>
/// <param name="Root">Absolute directory. Never deleted itself, only its content.</param>
/// <param name="SearchPattern">File name pattern (e.g. "thumbcache_*.db"); "*" matches everything.</param>
/// <param name="Recursive">Whether sub-directories are scanned.</param>
/// <param name="ApplyMinimumAge">
/// Whether files modified recently (see <see cref="ScanOptions.MinimumFileAge"/>) are skipped.
/// Used for temp folders, where recent files are likely still in use.
/// </param>
public sealed record ScanTarget(
    string Root,
    string SearchPattern = "*",
    bool Recursive = true,
    bool ApplyMinimumAge = false);
