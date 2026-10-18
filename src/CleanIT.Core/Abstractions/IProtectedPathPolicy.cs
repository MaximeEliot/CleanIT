namespace CleanIT.Core.Abstractions;

/// <summary>
/// Hard-coded safety net: decides whether a path must never be deleted,
/// whatever the rule or the user settings say.
/// </summary>
public interface IProtectedPathPolicy
{
    /// <summary>Returns true when <paramref name="fullPath"/> must not be touched.</summary>
    /// <param name="fullPath">Absolute path of a file or directory.</param>
    /// <param name="reason">Human readable reason (French) when the path is protected.</param>
    bool IsProtected(string fullPath, out string reason);

    /// <summary>
    /// Returns true when a rule may use <paramref name="root"/> as a cleanup root:
    /// it must not be a drive root, be inside a protected tree, or contain a critical folder.
    /// </summary>
    bool IsSafeCleanupRoot(string root);

    /// <summary>
    /// Check for a file explicitly picked by the user (disk analyzer): personal folders are allowed,
    /// system locations, system files and browser secrets are not.
    /// </summary>
    bool IsProtectedFromManualDeletion(string fullPath, out string reason);

    /// <summary>True when the path is inside the user's AppData folder.</summary>
    bool IsInApplicationData(string fullPath);
}
