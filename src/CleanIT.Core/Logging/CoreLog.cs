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
}
