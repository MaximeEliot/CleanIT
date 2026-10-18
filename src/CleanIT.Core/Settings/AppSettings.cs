namespace CleanIT.Core.Settings;

/// <summary>User preferences, persisted as JSON in %APPDATA%\CleanIT\settings.json.</summary>
public sealed class AppSettings
{
    public const int MaxMinimumAgeHours = 24 * 30;

    /// <summary>Folders, files or file name patterns ("*.docx") never deleted.</summary>
    public List<string> Exclusions { get; set; } = [];

    /// <summary>Off by default: files go to the Recycle Bin.</summary>
    public bool PermanentDeletion { get; set; }

    /// <summary>Temp files modified more recently than this are kept.</summary>
    public int MinimumTempFileAgeHours { get; set; } = 24;

    /// <summary>Checked categories; null means "use each rule's default".</summary>
    public List<string>? SelectedRuleIds { get; set; }

    /// <summary>Returns a copy with out-of-range values fixed (the file can be edited by hand).</summary>
    public AppSettings Sanitized() => new()
    {
        Exclusions = (Exclusions ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        PermanentDeletion = PermanentDeletion,
        MinimumTempFileAgeHours = Math.Clamp(MinimumTempFileAgeHours, 0, MaxMinimumAgeHours),
        SelectedRuleIds = SelectedRuleIds?.Distinct(StringComparer.Ordinal).ToList(),
    };
}
