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
}
