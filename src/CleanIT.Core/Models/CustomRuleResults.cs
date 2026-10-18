namespace CleanIT.Core.Models;

/// <summary>Size measured by a <see cref="Abstractions.ICustomCleanupRule"/>.</summary>
public sealed record CustomScanSummary(long SizeBytes, long ItemCount);

/// <summary>Outcome of <see cref="Abstractions.ICustomCleanupRule.Execute"/>.</summary>
public sealed record CustomCleanupOutcome(bool Success, long BytesFreed, long ItemsDeleted, string? ErrorMessage = null);
