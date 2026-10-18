namespace CleanIT.Core.Models;

/// <summary>
/// Result of an analysis. A cleanup can only be started from a report,
/// which guarantees that nothing is deleted without a prior scan.
/// </summary>
public sealed class ScanReport
{
    public ScanReport(DateTimeOffset createdAt, IReadOnlyList<CategoryScanResult> categories)
    {
        CreatedAt = createdAt;
        Categories = categories;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<CategoryScanResult> Categories { get; }

    public long TotalBytes => Categories.Sum(c => c.TotalBytes);

    public long TotalItems => Categories.Sum(c => c.ItemCount);
}
