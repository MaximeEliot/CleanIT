namespace CleanIT.Core.Models;

public enum SkipReason
{
    /// <summary>Matched the hard-coded protected-path policy.</summary>
    Protected,

    /// <summary>Matched a user exclusion.</summary>
    Excluded,

    /// <summary>Outside the roots declared by the rule.</summary>
    OutsideRuleRoots,

    /// <summary>Symbolic link or junction: never followed nor deleted.</summary>
    ReparsePoint,

    /// <summary>Directory could not be listed (access denied, I/O error...).</summary>
    Inaccessible,

    /// <summary>Modified too recently (probably still in use).</summary>
    TooRecent,
}

/// <summary>Something the scan deliberately left out, kept for the report and the log.</summary>
public sealed record SkippedEntry(string FullPath, SkipReason Reason, string Detail);
