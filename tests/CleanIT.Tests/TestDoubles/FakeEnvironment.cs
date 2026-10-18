using CleanIT.Core.Abstractions;

namespace CleanIT.Tests.TestDoubles;

/// <summary>A typical Windows machine for user "Alice", with no real disk access.</summary>
internal sealed class FakeEnvironment : IEnvironmentInfo
{
    public string SystemDrive { get; init; } = @"C:\";

    public string WindowsDirectory { get; init; } = @"C:\Windows";

    public string ProgramFiles { get; init; } = @"C:\Program Files";

    public string ProgramFilesX86 { get; init; } = @"C:\Program Files (x86)";

    public string ProgramData { get; init; } = @"C:\ProgramData";

    public string UserProfile { get; init; } = @"C:\Users\Alice";

    public string LocalAppData { get; init; } = @"C:\Users\Alice\AppData\Local";

    public string RoamingAppData { get; init; } = @"C:\Users\Alice\AppData\Roaming";

    public string UserTemp { get; init; } = @"C:\Users\Alice\AppData\Local\Temp";

    public bool IsElevated { get; init; }

    public HashSet<string> RunningProcesses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsProcessRunning(string processName) => RunningProcesses.Contains(processName);
}
