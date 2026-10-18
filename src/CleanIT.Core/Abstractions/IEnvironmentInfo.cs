namespace CleanIT.Core.Abstractions;

/// <summary>
/// Well-known locations and runtime facts about the current machine.
/// Abstracted so that rules and the protected-path policy can be tested with fake paths.
/// </summary>
public interface IEnvironmentInfo
{
    /// <summary>System drive root, e.g. "C:\".</summary>
    string SystemDrive { get; }

    /// <summary>Windows directory, e.g. "C:\Windows".</summary>
    string WindowsDirectory { get; }

    string ProgramFiles { get; }

    string ProgramFilesX86 { get; }

    /// <summary>"C:\ProgramData".</summary>
    string ProgramData { get; }

    /// <summary>Current user profile, e.g. "C:\Users\Alice".</summary>
    string UserProfile { get; }

    /// <summary>"%LOCALAPPDATA%", e.g. "C:\Users\Alice\AppData\Local".</summary>
    string LocalAppData { get; }

    /// <summary>"%APPDATA%", e.g. "C:\Users\Alice\AppData\Roaming".</summary>
    string RoamingAppData { get; }

    /// <summary>User temporary directory ("%TEMP%").</summary>
    string UserTemp { get; }

    /// <summary>True when the process runs with administrator rights.</summary>
    bool IsElevated { get; }

    /// <summary>True when at least one process with this name (without ".exe") is running.</summary>
    bool IsProcessRunning(string processName);
}
