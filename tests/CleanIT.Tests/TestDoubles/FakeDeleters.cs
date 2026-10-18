using System.IO.Abstractions;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Tests.TestDoubles;

/// <summary>Stands in for the Shell Recycle Bin: records and removes the file from the mock file system.</summary>
internal sealed class FakeRecycleBinDeleter(IFileSystem fileSystem) : IFileDeleter
{
    public List<string> Recycled { get; } = [];

    /// <summary>Paths for which the deletion fails with the given error.</summary>
    public Dictionary<string, CleanupErrorKind> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Invoked after each successful deletion (used to trigger cancellation).</summary>
    public Action? AfterDelete { get; set; }

    public CleanupMode Mode => CleanupMode.RecycleBin;

    public DeletionResult DeleteFile(string fullPath)
    {
        if (Failures.TryGetValue(fullPath, out var kind))
        {
            return DeletionResult.Fail(kind, "simulated " + kind);
        }

        fileSystem.File.Delete(fullPath);
        Recycled.Add(fullPath);
        AfterDelete?.Invoke();
        return DeletionResult.Ok;
    }
}

/// <summary>Fake Recycle Bin for <c>RecycleBinRule</c> tests.</summary>
internal sealed class FakeRecycleBin : IRecycleBin
{
    public long SizeBytes { get; set; }

    public long ItemCount { get; set; }

    public int EmptyCalls { get; private set; }

    public int EmptyResult { get; set; }

    public (long SizeBytes, long ItemCount) Query() => (SizeBytes, ItemCount);

    public int Empty()
    {
        EmptyCalls++;
        if (EmptyResult == 0)
        {
            SizeBytes = 0;
            ItemCount = 0;
        }

        return EmptyResult;
    }
}
