using System.Runtime.InteropServices;

namespace CleanIT.Core.Platform.Windows;

/// <summary>Shell32 interop used for the Recycle Bin.</summary>
internal static class NativeMethods
{
    public const uint FoDelete = 0x0003;

    public const ushort FofSilent = 0x0004;
    public const ushort FofNoConfirmation = 0x0010;
    public const ushort FofAllowUndo = 0x0040;
    public const ushort FofNoConfirmMkDir = 0x0200;
    public const ushort FofNoErrorUi = 0x0400;

    /// <summary>
    /// Asks before destroying a file that cannot be recycled (too big, drive without Recycle Bin)
    /// instead of silently deleting it permanently, which FOF_NOCONFIRMATION alone would do.
    /// </summary>
    public const ushort FofWantNukeWarning = 0x4000;

    public const uint SherbNoConfirmation = 0x00000001;
    public const uint SherbNoProgressUi = 0x00000002;
    public const uint SherbNoSound = 0x00000004;

    /// <summary>SHFileOperation does not support paths of MAX_PATH (260) characters or more.</summary>
    public const int MaxPath = 260;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint WFunc;

        /// <summary>Must be double null-terminated.</summary>
        public string PFrom;
        public string? PTo;
        public ushort FFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool FAnyOperationsAborted;
        public IntPtr HNameMappings;
        public string? LpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShQueryRbInfo
    {
        public int CbSize;
        public long I64Size;
        public long I64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    public static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHQueryRecycleBinW")]
    public static extern int SHQueryRecycleBin(string? rootPath, ref ShQueryRbInfo queryInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHEmptyRecycleBinW")]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
