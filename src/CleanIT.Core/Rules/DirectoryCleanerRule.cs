using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Core.Rules;

/// <summary>
/// Base class for rules that clean the content of well-known folders.
/// Subclasses only declare metadata and targets.
/// </summary>
public abstract class DirectoryCleanerRule : ICleanerRule
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    public abstract CleanerCategory Category { get; }

    public virtual bool RequiresElevation => false;

    public virtual bool IsEnabledByDefault => true;

    /// <summary>Not applicable when none of the target folders exists.</summary>
    public virtual RuleAvailability CheckAvailability(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return GetTargets(context).Any(t => context.FileSystem.Directory.Exists(t.Root))
            ? RuleAvailability.Available
            : RuleAvailability.NotApplicable("Aucun dossier à nettoyer sur cet ordinateur.");
    }

    public abstract IReadOnlyList<ScanTarget> GetTargets(RuleContext context);
}
