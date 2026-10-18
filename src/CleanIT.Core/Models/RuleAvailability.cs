namespace CleanIT.Core.Models;

public enum AvailabilityStatus
{
    Available,

    /// <summary>The rule needs administrator rights and the process is not elevated.</summary>
    RequiresElevation,

    /// <summary>Nothing to clean on this machine (e.g. browser not installed).</summary>
    NotApplicable,

    /// <summary>Temporarily impossible (e.g. the browser is running).</summary>
    Blocked,
}

/// <summary>Result of <see cref="Abstractions.ICleanerRule.CheckAvailability"/>.</summary>
public sealed record RuleAvailability(AvailabilityStatus Status, string? Reason = null)
{
    public static RuleAvailability Available { get; } = new(AvailabilityStatus.Available);

    public bool IsAvailable => Status == AvailabilityStatus.Available;

    public static RuleAvailability NeedsElevation() =>
        new(AvailabilityStatus.RequiresElevation, "Nécessite les droits administrateur.");

    public static RuleAvailability NotApplicable(string reason) => new(AvailabilityStatus.NotApplicable, reason);

    public static RuleAvailability Blocked(string reason) => new(AvailabilityStatus.Blocked, reason);
}
