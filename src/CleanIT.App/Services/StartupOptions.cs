namespace CleanIT.App.Services;

/// <summary>
/// Command line options. Used when CleanIT relaunches itself as administrator,
/// to keep the categories the user had checked.
/// </summary>
public sealed record StartupOptions(IReadOnlySet<string> PreselectedRuleIds)
{
    public const string SelectArgument = "--select=";

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arg in args)
        {
            if (arg.StartsWith(SelectArgument, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var id in arg[SelectArgument.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ids.Add(id);
                }
            }
        }

        return new StartupOptions(ids);
    }

    public static string FormatSelect(IEnumerable<string> ruleIds) => SelectArgument + string.Join(',', ruleIds);
}
