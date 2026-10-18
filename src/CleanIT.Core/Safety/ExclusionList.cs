using System.IO.Enumeration;

namespace CleanIT.Core.Safety;

/// <summary>
/// User-defined exclusions. Each entry is either:
/// <list type="bullet">
/// <item>an absolute folder or file path: the path and everything below it is kept;</item>
/// <item>a file name pattern without any backslash, such as "*.docx" or "setup*.log".</item>
/// </list>
/// Invalid entries are ignored (and reported through <see cref="InvalidEntries"/>).
/// </summary>
public sealed class ExclusionList
{
    private readonly List<string> _paths = [];
    private readonly List<string> _patterns = [];
    private readonly List<string> _invalid = [];

    public ExclusionList(IEnumerable<string>? entries)
    {
        foreach (var raw in entries ?? [])
        {
            var entry = raw?.Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            if (!entry.Contains('\\', StringComparison.Ordinal) && !entry.Contains('/', StringComparison.Ordinal))
            {
                _patterns.Add(entry);
            }
            else if (PathNormalizer.TryNormalize(entry) is { } normalized)
            {
                _paths.Add(normalized);
            }
            else
            {
                _invalid.Add(entry);
            }
        }
    }

    public static ExclusionList Empty { get; } = new([]);

    public IReadOnlyList<string> InvalidEntries => _invalid;

    /// <summary>Returns true when <paramref name="normalizedPath"/> matches an exclusion.</summary>
    public bool IsExcluded(string normalizedPath, out string matchedEntry)
    {
        foreach (var path in _paths)
        {
            if (PathNormalizer.IsSameOrUnder(normalizedPath, path))
            {
                matchedEntry = path;
                return true;
            }
        }

        if (_patterns.Count > 0)
        {
            var name = Path.GetFileName(normalizedPath);
            foreach (var pattern in _patterns)
            {
                if (FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true))
                {
                    matchedEntry = pattern;
                    return true;
                }
            }
        }

        matchedEntry = string.Empty;
        return false;
    }
}
