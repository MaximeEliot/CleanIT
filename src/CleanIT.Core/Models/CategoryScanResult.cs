namespace CleanIT.Core.Models;

/// <summary>Scan result for one rule.</summary>
public sealed class CategoryScanResult
{
    public CategoryScanResult(
        string ruleId,
        string displayName,
        RuleAvailability availability,
        IReadOnlyList<string> allowedRoots,
        IReadOnlyList<CleanupItem> items,
        IReadOnlyList<SkippedEntry> skipped,
        int skippedCount,
        CustomScanSummary? customSummary = null)
    {
        RuleId = ruleId;
        DisplayName = displayName;
        Availability = availability;
        AllowedRoots = allowedRoots;
        Items = items;
        Skipped = skipped;
        SkippedCount = skippedCount;
        CustomSummary = customSummary;
        TotalBytes = customSummary?.SizeBytes ?? items.Sum(i => i.SizeBytes);
        ItemCount = customSummary?.ItemCount ?? items.Count;
    }

    public string RuleId { get; }

    public string DisplayName { get; }

    /// <summary>Availability at scan time. When not available, the category is empty.</summary>
    public RuleAvailability Availability { get; }

    /// <summary>Normalized roots captured at scan time; re-checked again before each deletion.</summary>
    public IReadOnlyList<string> AllowedRoots { get; }

    /// <summary>Files eligible for deletion (empty for custom rules).</summary>
    public IReadOnlyList<CleanupItem> Items { get; }

    /// <summary>A sample of skipped entries (capped to keep memory bounded).</summary>
    public IReadOnlyList<SkippedEntry> Skipped { get; }

    /// <summary>Total number of skipped entries, including those not kept in <see cref="Skipped"/>.</summary>
    public int SkippedCount { get; }

    /// <summary>Set for <see cref="Abstractions.ICustomCleanupRule"/> results.</summary>
    public CustomScanSummary? CustomSummary { get; }

    public bool IsCustom => CustomSummary is not null;

    /// <summary>Reclaimable space, in bytes.</summary>
    public long TotalBytes { get; }

    public long ItemCount { get; }
}
