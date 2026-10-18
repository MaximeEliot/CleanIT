using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Tests.TestDoubles;

/// <summary>Configurable rule used to drive the engines in tests.</summary>
internal sealed class TestRule(string id, params ScanTarget[] targets) : ICleanerRule
{
    public string Id { get; } = id;

    public string DisplayName => "Test " + Id;

    public string Description => "Règle de test";

    public CleanerCategory Category => CleanerCategory.TemporaryFiles;

    public bool RequiresElevation { get; init; }

    public bool IsEnabledByDefault => true;

    public RuleAvailability Availability { get; init; } = RuleAvailability.Available;

    public Func<IReadOnlyList<ScanTarget>>? TargetsFactory { get; init; }

    public RuleAvailability CheckAvailability(RuleContext context) => Availability;

    public IReadOnlyList<ScanTarget> GetTargets(RuleContext context) => TargetsFactory?.Invoke() ?? targets;
}
