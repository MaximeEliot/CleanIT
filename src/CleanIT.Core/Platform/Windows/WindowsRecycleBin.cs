using System.Runtime.InteropServices;
using CleanIT.Core.Abstractions;

namespace CleanIT.Core.Platform.Windows;

/// <summary>Recycle Bin of all drives, through Shell32.</summary>
public sealed class WindowsRecycleBin : IRecycleBin
{
    public (long SizeBytes, long ItemCount) Query()
    {
        var info = new NativeMethods.ShQueryRbInfo { CbSize = Marshal.SizeOf<NativeMethods.ShQueryRbInfo>() };
        var hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        return hr == 0 ? (info.I64Size, info.I64NumItems) : (0, 0);
    }

    public int Empty() =>
        NativeMethods.SHEmptyRecycleBin(
            IntPtr.Zero,
            null,
            NativeMethods.SherbNoConfirmation | NativeMethods.SherbNoProgressUi | NativeMethods.SherbNoSound);
}
