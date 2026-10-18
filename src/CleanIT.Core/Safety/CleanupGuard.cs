using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Core.Safety;

/// <summary>Decision returned by <see cref="CleanupGuard"/>.</summary>
public readonly record struct GuardVerdict(bool IsAllowed, SkipReason? Reason, string Detail)
{
    public static GuardVerdict Allowed { get; } = new(true, null, string.Empty);

    public static GuardVerdict Deny(SkipReason reason, string detail) => new(false, reason, detail);
}

/// <summary>
/// The single gate every candidate goes through, at scan time and again before deletion.
/// A path is allowed only if it is strictly below one of the rule roots, not protected by
/// the hard-coded policy, and not excluded by the user.
/// </summary>
public sealed class CleanupGuard
{
    private readonly IProtectedPathPolicy _policy;
    private readonly ExclusionList _exclusions;

    public CleanupGuard(IProtectedPathPolicy policy, ExclusionList exclusions)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _exclusions = exclusions ?? throw new ArgumentNullException(nameof(exclusions));
    }

    public GuardVerdict Evaluate(string fullPath, IReadOnlyList<string> allowedRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedRoots);

        var path = PathNormalizer.TryNormalize(fullPath);
        if (path is null)
        {
            return GuardVerdict.Deny(SkipReason.Protected, "Chemin invalide, relatif ou réseau.");
        }

        var underRoot = false;
        foreach (var root in allowedRoots)
        {
            if (PathNormalizer.TryNormalize(root) is { } normalizedRoot && PathNormalizer.IsStrictlyUnder(path, normalizedRoot))
            {
                underRoot = true;
                break;
            }
        }

        if (!underRoot)
        {
            return GuardVerdict.Deny(SkipReason.OutsideRuleRoots, "Hors des dossiers de la catégorie.");
        }

        if (_policy.IsProtected(path, out var reason))
        {
            return GuardVerdict.Deny(SkipReason.Protected, reason);
        }

        if (_exclusions.IsExcluded(path, out var entry))
        {
            return GuardVerdict.Deny(SkipReason.Excluded, $"Exclu par l'utilisateur ({entry}).");
        }

        return GuardVerdict.Allowed;
    }
}
