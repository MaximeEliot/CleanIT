using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;
using CleanIT.Tests.TestDoubles;

namespace CleanIT.Tests.Scanning;

public class ScanEngineTests
{
    private const string Temp = TestHost.Temp;
    private readonly TestHost _host = new();

    private Task<ScanReport> ScanAsync(ICleanerRule rule, ScanOptions? options = null, CancellationToken ct = default) =>
        _host.CreateScanEngine().ScanAsync([rule], options ?? new ScanOptions(), null, ct);

    [Fact]
    public async Task Scan_ListsFilesRecursively_WithSizes()
    {
        _host.AddFile($@"{Temp}\a.tmp", 100);
        _host.AddFile($@"{Temp}\sub\b.tmp", 200);
        _host.AddFile($@"{Temp}\sub\deep\c.log", 300);

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)));

        var category = Assert.Single(report.Categories);
        Assert.Equal(3, category.ItemCount);
        Assert.Equal(600, category.TotalBytes);
        Assert.Equal(600, report.TotalBytes);
    }

    [Fact]
    public async Task Scan_NeverModifiesTheFileSystem()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\sub\b.tmp");
        var before = _host.FileSystem.AllPaths.ToList();

        await ScanAsync(new TestRule("t", new ScanTarget(Temp)));

        Assert.Equal(before, _host.FileSystem.AllPaths.ToList());
    }

    [Fact]
    public async Task Scan_NonRecursive_IgnoresSubdirectories()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\sub\b.tmp");

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp, Recursive: false)));

        Assert.Equal($@"{Temp}\a.tmp", Assert.Single(report.Categories[0].Items).FullPath);
    }

    [Fact]
    public async Task Scan_AppliesSearchPattern()
    {
        _host.AddFile($@"{Temp}\thumbcache_32.db");
        _host.AddFile($@"{Temp}\iconcache_16.db");
        _host.AddFile($@"{Temp}\other.db");

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp, "thumbcache_*.db")));

        Assert.Equal("thumbcache_32.db", Path.GetFileName(Assert.Single(report.Categories[0].Items).FullPath));
    }

    [Fact]
    public async Task Scan_SkipsRecentFiles_WhenMinimumAgeApplies()
    {
        _host.AddFile($@"{Temp}\old.tmp");
        _host.AddFile($@"{Temp}\recent.tmp", lastWriteUtc: _host.Time.GetUtcNow().UtcDateTime.AddHours(-1));

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp, ApplyMinimumAge: true)));

        var category = report.Categories[0];
        Assert.Equal("old.tmp", Path.GetFileName(Assert.Single(category.Items).FullPath));
        Assert.Contains(category.Skipped, s => s.Reason == SkipReason.TooRecent);
    }

    [Fact]
    public async Task Scan_KeepsRecentFiles_WhenMinimumAgeDoesNotApply()
    {
        _host.AddFile($@"{Temp}\recent.tmp", lastWriteUtc: _host.Time.GetUtcNow().UtcDateTime);

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)));

        Assert.Single(report.Categories[0].Items);
    }

    [Fact]
    public async Task Scan_NeverFollowsJunctions()
    {
        // A junction in %TEMP% pointing to Documents must not be traversed.
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\link\secret.docx");
        _host.FileSystem.DirectoryInfo.New($@"{Temp}\link").Attributes |= FileAttributes.ReparsePoint;

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)));

        var category = report.Categories[0];
        Assert.Equal("a.tmp", Path.GetFileName(Assert.Single(category.Items).FullPath));
        Assert.Contains(category.Skipped, s => s.Reason == SkipReason.ReparsePoint);
    }

    [Fact]
    public async Task Scan_IgnoresRootThatIsAReparsePoint()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.FileSystem.DirectoryInfo.New(Temp).Attributes |= FileAttributes.ReparsePoint;

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)));

        Assert.Empty(report.Categories[0].Items);
    }

    [Fact]
    public async Task Scan_AppliesUserExclusions()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        _host.AddFile($@"{Temp}\keep\b.tmp");
        _host.AddFile($@"{Temp}\c.important");

        var options = new ScanOptions { Exclusions = [$@"{Temp}\keep", "*.important"] };
        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)), options);

        var category = report.Categories[0];
        Assert.Equal("a.tmp", Path.GetFileName(Assert.Single(category.Items).FullPath));
        Assert.Equal(2, category.Skipped.Count(s => s.Reason == SkipReason.Excluded));
    }

    [Fact]
    public async Task Scan_SkipsProtectedFilesInsideCleanableRoot()
    {
        const string cache = @"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Cache";
        _host.AddFile($@"{cache}\Cache_Data\f_0001");
        _host.AddFile($@"{cache}\Cookies");

        var report = await ScanAsync(new TestRule("t", new ScanTarget(cache)));

        var category = report.Categories[0];
        Assert.Equal("f_0001", Path.GetFileName(Assert.Single(category.Items).FullPath));
        Assert.Contains(category.Skipped, s => s.Reason == SkipReason.Protected);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Users\Alice")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Users\Alice\Documents")]
    public async Task Scan_RefusesUnsafeRuleRoots(string root)
    {
        _host.AddFile($@"{root.TrimEnd('\\')}\important.docx");

        var report = await ScanAsync(new TestRule("bad", new ScanTarget(root)));

        var category = report.Categories[0];
        Assert.Empty(category.Items);
        Assert.Empty(category.AllowedRoots);
    }

    [Fact]
    public async Task Scan_DeduplicatesOverlappingTargets()
    {
        _host.AddFile($@"{Temp}\a.tmp");

        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp), new ScanTarget(Temp, "*.tmp")));

        Assert.Single(report.Categories[0].Items);
    }

    [Fact]
    public async Task Scan_MissingRoot_YieldsEmptyCategory()
    {
        var report = await ScanAsync(new TestRule("t", new ScanTarget(@"C:\Windows\Temp")));

        Assert.Empty(report.Categories[0].Items);
    }

    [Fact]
    public async Task Scan_ElevatedRule_IsSkippedWhenNotElevated()
    {
        _host.AddFile(@"C:\Windows\Temp\a.tmp");

        var report = await ScanAsync(new TestRule("sys", new ScanTarget(@"C:\Windows\Temp")) { RequiresElevation = true });

        var category = report.Categories[0];
        Assert.Equal(AvailabilityStatus.RequiresElevation, category.Availability.Status);
        Assert.Empty(category.Items);
    }

    [Fact]
    public async Task Scan_FaultyRule_DoesNotAbortOtherRules()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        var faulty = new TestRule("faulty") { TargetsFactory = () => throw new InvalidOperationException("boom") };
        var good = new TestRule("good", new ScanTarget(Temp));

        var report = await _host.CreateScanEngine().ScanAsync([faulty, good], new ScanOptions());

        Assert.Equal(AvailabilityStatus.Blocked, report.Categories[0].Availability.Status);
        Assert.Single(report.Categories[1].Items);
    }

    [Fact]
    public async Task Scan_Cancelled_Throws()
    {
        _host.AddFile($@"{Temp}\a.tmp");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScanAsync(new TestRule("t", new ScanTarget(Temp)), ct: cts.Token));
    }

    [Fact]
    public async Task Scan_CapsSkippedSampleButCountsAll()
    {
        for (var i = 0; i < 20; i++)
        {
            _host.AddFile($@"{Temp}\f{i}.keep");
        }

        var options = new ScanOptions { Exclusions = ["*.keep"], MaxSkippedEntriesPerCategory = 5 };
        var report = await ScanAsync(new TestRule("t", new ScanTarget(Temp)), options);

        Assert.Equal(5, report.Categories[0].Skipped.Count);
        Assert.Equal(20, report.Categories[0].SkippedCount);
    }
}
