namespace CleanIT.Core.Models;

/// <summary>A file found during the scan that is eligible for deletion.</summary>
public sealed record CleanupItem(string FullPath, long SizeBytes, DateTime LastWriteUtc);
