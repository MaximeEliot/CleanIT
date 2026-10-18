using CleanIT.Core.Models;

namespace CleanIT.Core.Abstractions;

/// <summary>
/// A rule whose content cannot be cleaned file by file through the regular engine
/// (e.g. the Recycle Bin, which must be emptied through the Shell API).
/// Such rules usually return no <see cref="ScanTarget"/>.
/// </summary>
public interface ICustomCleanupRule : ICleanerRule
{
    /// <summary>Measures what would be reclaimed, without modifying anything.</summary>
    CustomScanSummary Measure(RuleContext context);

    /// <summary>Performs the cleanup. Never called in dry-run mode.</summary>
    CustomCleanupOutcome Execute(RuleContext context);
}
