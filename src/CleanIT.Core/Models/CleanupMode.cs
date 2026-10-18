namespace CleanIT.Core.Models;

public enum CleanupMode
{
    /// <summary>Default: files are moved to the Recycle Bin and can be restored.</summary>
    RecycleBin,

    /// <summary>Files are deleted permanently. Must be explicitly enabled by the user.</summary>
    Permanent,

    /// <summary>Simulation: lists what would be deleted, touches nothing.</summary>
    DryRun,
}
