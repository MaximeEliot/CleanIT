namespace CleanIT.Core.Abstractions;

/// <summary>Access to the Windows Recycle Bin (all drives).</summary>
public interface IRecycleBin
{
    /// <summary>Returns the total size and number of items currently in the Recycle Bin.</summary>
    (long SizeBytes, long ItemCount) Query();

    /// <summary>Empties the Recycle Bin without any UI. Returns 0 on success, or an HRESULT.</summary>
    int Empty();
}
