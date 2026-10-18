using System.IO.Abstractions.TestingHelpers;
using CleanIT.Core.Safety;
using CleanIT.Core.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanIT.Tests.TestDoubles;

/// <summary>Wires the Core engines on top of an in-memory file system.</summary>
internal sealed class TestHost
{
    public const string Temp = @"C:\Users\Alice\AppData\Local\Temp";

    public TestHost(FakeEnvironment? environment = null)
    {
        Environment = environment ?? new FakeEnvironment();
        FileSystem = new MockFileSystem();
        FileSystem.AddDirectory(Environment.UserTemp);
        Policy = new ProtectedPathPolicy(Environment);
        Time = new FixedTimeProvider();
    }

    public MockFileSystem FileSystem { get; }

    public FakeEnvironment Environment { get; }

    public ProtectedPathPolicy Policy { get; }

    public FixedTimeProvider Time { get; }

    /// <summary>Old enough to pass the minimum-age filter.</summary>
    public DateTime OldDate => Time.GetUtcNow().UtcDateTime.AddDays(-10);

    public ScanEngine CreateScanEngine() =>
        new(FileSystem, Environment, Policy, Time, NullLogger<ScanEngine>.Instance);

    public void AddFile(string path, int size = 100, DateTime? lastWriteUtc = null)
    {
        FileSystem.AddFile(path, new MockFileData(new byte[size]) { LastWriteTime = lastWriteUtc ?? OldDate });
    }
}
