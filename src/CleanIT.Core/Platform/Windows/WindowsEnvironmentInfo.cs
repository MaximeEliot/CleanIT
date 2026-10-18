using System.Diagnostics;
using System.Security.Principal;
using CleanIT.Core.Abstractions;

namespace CleanIT.Core.Platform.Windows;

/// <summary>Real machine locations, resolved once at startup.</summary>
public sealed class WindowsEnvironmentInfo : IEnvironmentInfo
{
    public WindowsEnvironmentInfo()
    {
        WindowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        SystemDrive = Path.GetPathRoot(WindowsDirectory) is { Length: > 0 } root ? root : @"C:\";
        ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        UserTemp = Path.GetTempPath();

        using var identity = WindowsIdentity.GetCurrent();
        IsElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public string SystemDrive { get; }

    public string WindowsDirectory { get; }

    public string ProgramFiles { get; }

    public string ProgramFilesX86 { get; }

    public string ProgramData { get; }

    public string UserProfile { get; }

    public string LocalAppData { get; }

    public string RoamingAppData { get; }

    public string UserTemp { get; }

    public bool IsElevated { get; }

    public bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }
}
