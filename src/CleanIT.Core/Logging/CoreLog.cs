using CleanIT.Core.Models;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.Logging;

/// <summary>
/// All log messages of the Core library, compiled by the logging source generator
/// (no boxing, no parsing of templates at runtime).
/// </summary>
internal static partial class CoreLog
{
    // --- Scan (1xxx) ---------------------------------------------------------------------

    [LoggerMessage(1000, LogLevel.Information, "Scan started for {RuleCount} categories")]
    public static partial void ScanStarted(this ILogger logger, int ruleCount);

    [LoggerMessage(1001, LogLevel.Information, "Rule {RuleId}: {ItemCount} items, {TotalBytes} bytes, {SkippedCount} skipped ({Status})")]
    public static partial void RuleScanned(this ILogger logger, string ruleId, long itemCount, long totalBytes, int skippedCount, AvailabilityStatus status);

    [LoggerMessage(1002, LogLevel.Information, "Scan finished: {TotalBytes} bytes reclaimable")]
    public static partial void ScanFinished(this ILogger logger, long totalBytes);

    [LoggerMessage(1003, LogLevel.Error, "Scan failed for rule {RuleId}")]
    public static partial void RuleScanFailed(this ILogger logger, Exception exception, string ruleId);

    [LoggerMessage(1004, LogLevel.Warning, "Rule {RuleId} declared an unsafe root {Root}; ignored")]
    public static partial void UnsafeRuleRoot(this ILogger logger, string ruleId, string root);

    [LoggerMessage(1005, LogLevel.Debug, "Inaccessible {Path}: {Error}")]
    public static partial void Inaccessible(this ILogger logger, string path, string? error);

    // --- Cleanup (2xxx) ------------------------------------------------------------------

    [LoggerMessage(2000, LogLevel.Information, "Cleanup started: mode {Mode}, {CategoryCount} categories, report {ReportId}")]
    public static partial void CleanupStarted(this ILogger logger, CleanupMode mode, int categoryCount, Guid reportId);

    [LoggerMessage(2001, LogLevel.Information, "Cleanup finished: mode {Mode}, {ItemCount} items, {Bytes} bytes, {ErrorCount} errors, cancelled: {Cancelled}")]
    public static partial void CleanupFinished(this ILogger logger, CleanupMode mode, long itemCount, long bytes, int errorCount, bool cancelled);

    [LoggerMessage(2002, LogLevel.Information, "Deleted {Path} ({Bytes} bytes, {Mode})")]
    public static partial void Deleted(this ILogger logger, string path, long bytes, CleanupMode mode);

    [LoggerMessage(2003, LogLevel.Information, "[DRY-RUN] Would delete {Path} ({Bytes} bytes)")]
    public static partial void WouldDelete(this ILogger logger, string path, long bytes);

    [LoggerMessage(2004, LogLevel.Warning, "Not deleted {Path}: {Kind} - {Message}")]
    public static partial void DeletionFailed(this ILogger logger, string path, CleanupErrorKind kind, string message);

    [LoggerMessage(2005, LogLevel.Error, "Custom rule {RuleId} failed")]
    public static partial void CustomRuleFailed(this ILogger logger, Exception exception, string ruleId);

    [LoggerMessage(2006, LogLevel.Debug, "Directory not removed {Path}: {Message}")]
    public static partial void DirectoryNotRemoved(this ILogger logger, string path, string message);

    // --- Disk analysis (3xxx) ------------------------------------------------------------

    [LoggerMessage(3000, LogLevel.Information, "Disk analysis started on {Root}")]
    public static partial void DiskAnalysisStarted(this ILogger logger, string root);

    [LoggerMessage(3001, LogLevel.Information, "Disk analysis of {Root} finished: {Bytes} bytes, {FileCount} files, {Inaccessible} inaccessible directories")]
    public static partial void DiskAnalysisFinished(this ILogger logger, string root, long bytes, long fileCount, int inaccessible);

    // --- Settings (4xxx) -----------------------------------------------------------------

    [LoggerMessage(4000, LogLevel.Warning, "Settings file {Path} could not be read; defaults used")]
    public static partial void SettingsUnreadable(this ILogger logger, Exception exception, string path);

    [LoggerMessage(4001, LogLevel.Error, "Settings file {Path} could not be saved")]
    public static partial void SettingsNotSaved(this ILogger logger, Exception exception, string path);
}
