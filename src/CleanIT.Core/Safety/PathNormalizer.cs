namespace CleanIT.Core.Safety;

/// <summary>
/// Canonical form for path comparisons: absolute, local (drive letter), backslashes,
/// no "." / "..", no trailing separator, no trailing dots/spaces on segments
/// (Windows ignores them, so "C:\Windows." is "C:\Windows").
/// Comparisons are always case-insensitive.
/// </summary>
public static class PathNormalizer
{
    private static readonly char[] ForbiddenChars = ['*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Returns the canonical form of <paramref name="path"/>, or null when the path is not
    /// a fully qualified local path (relative, UNC/network, device path, alternate data stream...).
    /// Callers must treat null as "do not touch".
    /// </summary>
    public static string? TryNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim().Replace('/', '\\');

        // Extended-length prefix: accepted for local paths only.
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (p.StartsWith(@"\\?\", StringComparison.Ordinal) || p.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            p = p[4..];
        }

        // Network shares and anything that is not "X:\..." are out of scope.
        if (p.Length < 3 || !char.IsAsciiLetter(p[0]) || p[1] != ':' || p[2] != '\\')
        {
            return null;
        }

        // Wildcards, or a ':' after the drive letter (alternate data stream).
        if (p.IndexOfAny(ForbiddenChars) >= 0 || p.IndexOf(':', 2) >= 0)
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(p);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var segments = full.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < segments.Length; i++)
        {
            var trimmed = segments[i].TrimEnd('.', ' ');
            if (trimmed.Length == 0)
            {
                return null;
            }

            segments[i] = trimmed;
        }

        var drive = char.ToUpperInvariant(segments[0][0]) + ":";
        return segments.Length == 1 ? drive + "\\" : drive + "\\" + string.Join('\\', segments, 1, segments.Length - 1);
    }

    /// <summary>True for "C:\".</summary>
    public static bool IsDriveRoot(string normalizedPath) =>
        normalizedPath.Length == 3 && normalizedPath[1] == ':' && normalizedPath[2] == '\\';

    /// <summary>True when both normalized paths designate the same location.</summary>
    public static bool AreSame(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="parent"/> (both normalized).</summary>
    public static bool IsStrictlyUnder(string path, string parent)
    {
        var prefix = parent.EndsWith('\\') ? parent : parent + "\\";
        return path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when <paramref name="path"/> equals <paramref name="parent"/> or is inside it (both normalized).</summary>
    public static bool IsSameOrUnder(string path, string parent) => AreSame(path, parent) || IsStrictlyUnder(path, parent);

    /// <summary>Splits a normalized path into segments; the first one is the drive ("C:").</summary>
    public static string[] Segments(string normalizedPath) =>
        normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
}
