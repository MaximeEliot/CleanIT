using CleanIT.Core.Models;

namespace CleanIT.Core.Abstractions;

/// <summary>
/// A cleanup category (user temp files, browser cache, ...).
/// </summary>
/// <remarks>
/// Rules are declarative on purpose: they only describe <em>where</em> junk lives
/// (<see cref="GetTargets"/>). Enumeration, safety checks and deletion are performed
/// centrally by the scan and cleanup engines, so a buggy rule cannot bypass the
/// protected-path policy or the user exclusions.
/// To add a category, implement this interface and register the class in the DI container.
/// </remarks>
public interface ICleanerRule
{
    /// <summary>Stable identifier, used in settings and logs (e.g. "user-temp").</summary>
    string Id { get; }

    /// <summary>Name shown to the user (French).</summary>
    string DisplayName { get; }

    /// <summary>Short explanation shown to the user (French).</summary>
    string Description { get; }

    CleanerCategory Category { get; }

    /// <summary>True when the targets can only be cleaned with administrator rights.</summary>
    bool RequiresElevation { get; }

    /// <summary>Whether the category is checked the first time the application runs.</summary>
    bool IsEnabledByDefault { get; }

    /// <summary>Tells whether the rule can run right now (installed software, rights, running processes...).</summary>
    RuleAvailability CheckAvailability(RuleContext context);

    /// <summary>
    /// Directories (and file filters) this rule is allowed to clean. Only items strictly
    /// below one of these roots can ever be deleted for this rule; the roots themselves never are.
    /// </summary>
    IReadOnlyList<ScanTarget> GetTargets(RuleContext context);
}
