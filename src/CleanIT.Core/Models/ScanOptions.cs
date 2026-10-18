namespace CleanIT.Core.Models;

public sealed record ScanOptions
{
    /// <summary>Files modified more recently than this are skipped in temp folders.</summary>
    public TimeSpan MinimumFileAge { get; init; } = TimeSpan.FromHours(24);

    /// <summary>User exclusions: folders, files, or file name patterns such as "*.docx".</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>Maximum number of skipped entries kept per category in the report.</summary>
    public int MaxSkippedEntriesPerCategory { get; init; } = 500;
}

/// <summary>Scan progress notification.</summary>
public sealed record ScanProgress(string RuleId, string DisplayName, int RuleIndex, int RuleCount, long FilesFound, long BytesFound);
