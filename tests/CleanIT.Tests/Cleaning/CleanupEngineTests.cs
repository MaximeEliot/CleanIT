using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;
using CleanIT.Tests.TestDoubles;

namespace CleanIT.Tests.Cleaning;

public class CleanupEngineTests
{
    private const string Temp = TestHost.Temp;
    private readonly TestHost _host = new();
    private readonly TestRule _tempRule = new("temp", new ScanTarget(Temp));

    private async Task<ScanReport> ScanAsync(params ICleanerRule[] rules) =>
        await _host.CreateScanEngine().ScanAsync(rules.Length == 0 ? [_tempRule] : rules, new ScanOptions());

    private static CleanupRequest Request(CleanupMode mode, bool confirmed = true, params string[] ruleIds) => new()
    {
        RuleIds = new HashSet<string>(ruleIds.Length == 0 ? ["temp"] : ruleIds),
        Mode = mode,
        UserConfirmed = confirmed,
    };

    [Theory]
    [InlineData(CleanupMode.RecycleBin)]
    [InlineData(CleanupMode.Permanent)]
    public async Task Clean_WithoutConfirmation_IsRefused(CleanupMode mode)
    {
        _host.AddFile($@"{Temp}\a.tmp");
        var report = await ScanAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _host.CreateCleanupEngine().CleanAsync(report, Request(mode, confirmed: false)));

        Assert.True(_host.FileSystem.File.Exists($@"{Temp}\a.tmp"));
    }

    [Fact]
    public async Task DryRun_DeletesNothing_AndListsEverything()
    {
        _host.AddFile($@"{Temp}\a.tmp", 100);
        _host.AddFile($@"{Temp}\sub\b.tmp", 50);
        var report = await ScanAsync();
        var before = _host.FileSystem.AllPaths.ToList();
        var recycle = new FakeRecycleBinDeleter(_host.FileSystem);

        // Dry-run needs no confirmation.
        var result = await _host.CreateCleanupEngine(recycle).CleanAsync(report, Request(CleanupMode.DryRun, confirmed: false));

        Assert.Equal(before, _host.FileSystem.AllPaths.ToList());
        Assert.Empty(recycle.Recycled);
        Assert.Equal(150, result.BytesFreed);
        Assert.Equal(2, result.ItemsDeleted);
        Assert.Equal(0, result.DirectoriesRemoved);
        Assert.Equal(CleanupMode.DryRun, result.Mode);
        Assert.Equal(
            new[] { $@"{Temp}\a.tmp", $@"{Temp}\sub\b.tmp" }.Order(),
            result.DryRunEntries.Order());
    }

    [Fact]
    public async Task DryRun_NeverExecutesCustomRules()
    {
        var bin = new FakeRecycleBin { SizeBytes = 1000, ItemCount = 3 };
        var rule = new CleanIT.Core.Rules.RecycleBinRule(bin);
        var report = await ScanAsync(rule);

        var result = await _host.CreateCleanupEngine(null, rule).CleanAsync(report, Request(CleanupMode.DryRun, false, rule.Id));

        Assert.Equal(0, bin.EmptyCalls);
        Assert.Equal(1000, result.BytesFreed);
        Assert.Single(result.DryRunEntries);
    }

    [Fact]
    public async Task RecycleBinMode_IsTheDefault_AndUsesTheRecycleBinDeleter()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        var report = await ScanAsync();
        var recycle = new FakeRecycleBinDeleter(_host.FileSystem);

        var request = new CleanupRequest { RuleIds = new HashSet<string> { "temp" }, UserConfirmed = true };
        var result = await _host.CreateCleanupEngine(recycle).CleanAsync(report, request);

        Assert.Equal(CleanupMode.RecycleBin, request.Mode);
        Assert.Equal($@"{Temp}\a.tmp", Assert.Single(recycle.Recycled));
        Assert.Equal(1, result.ItemsDeleted);
    }

    [Fact]
    public async Task Permanent_DeletesFiles_AndRemovesEmptiedDirectoriesButNotTheRoot()
    {
        _host.AddFile($@"{Temp}\a.tmp", 10);
        _host.AddFile($@"{Temp}\sub\deep\b.tmp", 20);
        var report = await ScanAsync();

        var result = await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.Equal(30, result.BytesFreed);
        Assert.Equal(2, result.ItemsDeleted);
        Assert.Equal(2, result.DirectoriesRemoved);
        Assert.False(_host.FileSystem.Directory.Exists($@"{Temp}\sub"));
        Assert.True(_host.FileSystem.Directory.Exists(Temp));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Permanent_KeepsDirectoriesThatStillContainFiles()
    {
        _host.AddFile($@"{Temp}\sub\old.tmp");
        _host.AddFile($@"{Temp}\sub\recent.tmp", lastWriteUtc: _host.Time.GetUtcNow().UtcDateTime);
        var rule = new TestRule("temp", new ScanTarget(Temp, ApplyMinimumAge: true));
        var report = await ScanAsync(rule);

        await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.True(_host.FileSystem.File.Exists($@"{Temp}\sub\recent.tmp"));
        Assert.False(_host.FileSystem.File.Exists($@"{Temp}\sub\old.tmp"));
    }

    [Fact]
    public async Task Permanent_DeletesReadOnlyFiles()
    {
        _host.AddFile($@"{Temp}\ro.tmp");
        _host.FileSystem.File.SetAttributes($@"{Temp}\ro.tmp", FileAttributes.ReadOnly);
        var report = await ScanAsync();

        var result = await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.False(_host.FileSystem.File.Exists($@"{Temp}\ro.tmp"));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Clean_ReappliesExclusionsAddedAfterTheScan()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\keep.tmp");
        var report = await ScanAsync();

        var request = Request(CleanupMode.Permanent) with { Exclusions = ["keep.tmp"] };
        var result = await _host.CreateCleanupEngine().CleanAsync(report, request);

        Assert.True(_host.FileSystem.File.Exists($@"{Temp}\keep.tmp"));
        Assert.Equal(CleanupErrorKind.Excluded, Assert.Single(result.Errors).Kind);
    }

    [Fact]
    public async Task Clean_FileVanishedSinceScan_IsReportedAndOthersContinue()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\b.tmp");
        var report = await ScanAsync();
        _host.FileSystem.File.Delete($@"{Temp}\a.tmp");

        var result = await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.Equal(CleanupErrorKind.Vanished, Assert.Single(result.Errors).Kind);
        Assert.Equal(1, result.ItemsDeleted);
    }

    [Fact]
    public async Task Clean_FileReplacedByLinkSinceScan_IsNotDeleted()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        var report = await ScanAsync();
        _host.FileSystem.File.SetAttributes($@"{Temp}\a.tmp", FileAttributes.ReparsePoint);

        var result = await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.True(_host.FileSystem.File.Exists($@"{Temp}\a.tmp"));
        Assert.Equal(CleanupErrorKind.ReparsePoint, Assert.Single(result.Errors).Kind);
    }

    [Fact]
    public async Task Clean_LockedAndDeniedFiles_AreLoggedAndSkipped()
    {
        _host.AddFile($@"{Temp}\locked.tmp");
        _host.AddFile($@"{Temp}\denied.tmp");
        _host.AddFile($@"{Temp}\ok.tmp");
        var report = await ScanAsync();
        var recycle = new FakeRecycleBinDeleter(_host.FileSystem);
        recycle.Failures[$@"{Temp}\locked.tmp"] = CleanupErrorKind.Locked;
        recycle.Failures[$@"{Temp}\denied.tmp"] = CleanupErrorKind.AccessDenied;

        var result = await _host.CreateCleanupEngine(recycle).CleanAsync(report, Request(CleanupMode.RecycleBin));

        Assert.Equal(1, result.ItemsDeleted);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Kind == CleanupErrorKind.Locked);
        Assert.Contains(result.Errors, e => e.Kind == CleanupErrorKind.AccessDenied);
        Assert.Equal(2, Assert.Single(result.Categories).ErrorCount);
    }

    [Fact]
    public async Task Clean_Cancelled_ReturnsPartialResult()
    {
        for (var i = 0; i < 10; i++)
        {
            _host.AddFile($@"{Temp}\f{i}.tmp");
        }

        var report = await ScanAsync();
        using var cts = new CancellationTokenSource();
        var recycle = new FakeRecycleBinDeleter(_host.FileSystem);
        recycle.AfterDelete = () =>
        {
            if (recycle.Recycled.Count == 3)
            {
                cts.Cancel();
            }
        };

        var result = await _host.CreateCleanupEngine(recycle).CleanAsync(report, Request(CleanupMode.RecycleBin), null, cts.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(3, result.ItemsDeleted);
        Assert.Equal(7, _host.FileSystem.Directory.GetFiles(Temp).Length);
    }

    [Fact]
    public async Task Clean_OnlyProcessesSelectedCategories()
    {
        _host.AddFile($@"{Temp}\a\x.tmp");
        _host.AddFile($@"{Temp}\b\y.tmp");
        var ruleA = new TestRule("a", new ScanTarget($@"{Temp}\a"));
        var ruleB = new TestRule("b", new ScanTarget($@"{Temp}\b"));
        var report = await ScanAsync(ruleA, ruleB);

        await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent, true, "b"));

        Assert.True(_host.FileSystem.File.Exists($@"{Temp}\a\x.tmp"));
        Assert.False(_host.FileSystem.File.Exists($@"{Temp}\b\y.tmp"));
    }

    [Fact]
    public async Task Clean_TamperedReport_CannotDeleteProtectedFiles()
    {
        // A report forged (or corrupted) to target system files must be rejected item by item.
        const string system = @"C:\Windows\System32\kernel32.dll";
        const string outside = @"C:\Users\Alice\Documents\cv.docx";
        _host.AddFile(system);
        _host.AddFile(outside);
        var category = new CategoryScanResult(
            "temp",
            "Forged",
            RuleAvailability.Available,
            [@"C:\Windows", Temp],
            [new CleanupItem(system, 100, _host.OldDate), new CleanupItem(outside, 100, _host.OldDate)],
            [],
            0);
        var report = new ScanReport(_host.Time.GetUtcNow(), [category]);

        var result = await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.Permanent));

        Assert.True(_host.FileSystem.File.Exists(system));
        Assert.True(_host.FileSystem.File.Exists(outside));
        Assert.Equal(0, result.ItemsDeleted);
        Assert.Contains(result.Errors, e => e.FullPath == system && e.Kind == CleanupErrorKind.Protected);
        Assert.Contains(result.Errors, e => e.FullPath == outside && e.Kind == CleanupErrorKind.OutsideRuleRoots);
    }

    [Fact]
    public async Task Clean_CustomRuleRunsBeforeFileCategories()
    {
        // Emptying the Recycle Bin after recycling temp files would make their deletion permanent.
        _host.AddFile($@"{Temp}\a.tmp");
        var bin = new FakeRecycleBin { SizeBytes = 10, ItemCount = 1 };
        var recycleRule = new CleanIT.Core.Rules.RecycleBinRule(bin);
        var report = await ScanAsync(_tempRule, recycleRule);
        var recycle = new FakeRecycleBinDeleter(_host.FileSystem);
        var recycledWhenEmptied = -1;
        bin.EmptyResult = 0;
        recycle.AfterDelete = () => recycledWhenEmptied = recycledWhenEmptied == -1 ? bin.EmptyCalls : recycledWhenEmptied;

        await _host.CreateCleanupEngine(recycle, recycleRule).CleanAsync(report, Request(CleanupMode.RecycleBin, true, "temp", recycleRule.Id));

        Assert.Equal(1, bin.EmptyCalls);
        Assert.Equal(1, recycledWhenEmptied); // the bin was already emptied when the first temp file was recycled
    }

    [Fact]
    public async Task Clean_ReportsProgress()
    {
        for (var i = 0; i < 60; i++)
        {
            _host.AddFile($@"{Temp}\f{i}.tmp", 1);
        }

        var report = await ScanAsync();
        var updates = new List<CleanupProgress>();
        var progress = new SynchronousProgress<CleanupProgress>(updates.Add);

        await _host.CreateCleanupEngine().CleanAsync(report, Request(CleanupMode.DryRun, false), progress);

        Assert.NotEmpty(updates);
        Assert.Equal(60, updates[^1].ProcessedItems);
        Assert.Equal(60, updates[^1].TotalItems);
    }
}

/// <summary>IProgress that invokes the callback inline (Progress&lt;T&gt; posts asynchronously).</summary>
internal sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
