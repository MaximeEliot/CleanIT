using System.Globalization;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Core.Rules;

/// <summary>
/// Empties the Recycle Bin of all drives through the Shell. This is permanent by nature,
/// so the category is unchecked by default.
/// </summary>
public sealed class RecycleBinRule : ICustomCleanupRule
{
    private readonly IRecycleBin _recycleBin;

    public RecycleBinRule(IRecycleBin recycleBin) => _recycleBin = recycleBin;

    public string Id => "recycle-bin";

    public string DisplayName => "Corbeille";

    public string Description => "Vide la corbeille de tous les lecteurs (suppression définitive).";

    public CleanerCategory Category => CleanerCategory.RecycleBin;

    public bool RequiresElevation => false;

    public bool IsEnabledByDefault => false;

    public RuleAvailability CheckAvailability(RuleContext context) => RuleAvailability.Available;

    public IReadOnlyList<ScanTarget> GetTargets(RuleContext context) => [];

    public CustomScanSummary Measure(RuleContext context)
    {
        var (size, count) = _recycleBin.Query();
        return new CustomScanSummary(size, count);
    }

    public CustomCleanupOutcome Execute(RuleContext context)
    {
        var (size, count) = _recycleBin.Query();
        if (count == 0)
        {
            // SHEmptyRecycleBin reports an error on an already empty bin.
            return new CustomCleanupOutcome(true, 0, 0);
        }

        var hr = _recycleBin.Empty();
        return hr == 0
            ? new CustomCleanupOutcome(true, size, count)
            : new CustomCleanupOutcome(false, 0, 0, string.Format(CultureInfo.InvariantCulture, "Impossible de vider la corbeille (code 0x{0:X8}).", hr));
    }
}
