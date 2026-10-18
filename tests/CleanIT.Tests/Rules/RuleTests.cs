using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;
using CleanIT.Core.Rules;
using CleanIT.Tests.TestDoubles;

namespace CleanIT.Tests.Rules;

public class RuleTests
{
    private const string Local = @"C:\Users\Alice\AppData\Local";
    private const string ChromeData = $@"{Local}\Google\Chrome\User Data";

    private readonly TestHost _host = new();

    private RuleContext Context => new(_host.FileSystem, _host.Environment);

    private async Task<CategoryScanResult> ScanAsync(ICleanerRule rule) =>
        (await _host.CreateScanEngine().ScanAsync([rule], new ScanOptions())).Categories[0];

    [Fact]
    public void DefaultRules_HaveUniqueIdsAndFrenchNames()
    {
        var rules = DefaultRules.Create(new FakeRecycleBin());

        Assert.Equal(rules.Count, rules.Select(r => r.Id).Distinct().Count());
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.DisplayName)));
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
    }

    [Fact]
    public void DefaultRules_AllTargetsAreSafeRootsWhoseContentIsCleanable()
    {
        // Guards against a rule whose folder is silently blocked (or wrongly opened) by the policy.
        _host.FileSystem.AddDirectory($@"{ChromeData}\Default");
        _host.FileSystem.AddDirectory($@"{ChromeData}\Profile 1");
        _host.FileSystem.AddDirectory($@"{Local}\Microsoft\Edge\User Data\Default");
        _host.FileSystem.AddDirectory($@"{Local}\Mozilla\Firefox\Profiles\abc.default-release");

        var targets = DefaultRules.Create(new FakeRecycleBin()).SelectMany(r => r.GetTargets(Context)).ToList();

        Assert.NotEmpty(targets);
        Assert.All(targets, t =>
        {
            Assert.True(_host.Policy.IsSafeCleanupRoot(t.Root), $"Unsafe root: {t.Root}");
            Assert.True(_host.Policy.IsProtected(t.Root, out _), $"Root itself must be protected: {t.Root}");
            Assert.False(_host.Policy.IsProtected(Path.Combine(t.Root, "sample.bin"), out var reason), $"{t.Root}: {reason}");
        });
    }

    [Fact]
    public void AdminRules_RequireElevation()
    {
        var rules = DefaultRules.Create(new FakeRecycleBin()).ToDictionary(r => r.Id);

        Assert.True(rules["system-temp"].RequiresElevation);
        Assert.True(rules["windows-update"].RequiresElevation);
        Assert.True(rules["system-error-reports"].RequiresElevation);
        Assert.False(rules["user-temp"].RequiresElevation);
        Assert.False(rules["chrome-cache"].RequiresElevation);
        Assert.False(rules["recycle-bin"].RequiresElevation);
    }

    [Fact]
    public void UserTempRule_TargetsTempWithMinimumAge()
    {
        var target = Assert.Single(new UserTempRule().GetTargets(Context));

        Assert.Equal(TestHost.Temp, target.Root);
        Assert.True(target.ApplyMinimumAge);
    }

    [Fact]
    public async Task ChromeRule_ListsOnlyCacheFolders_NothingElseInTheProfile()
    {
        // Names of profile files are irrelevant here: the rule must ignore everything outside
        // the cache folders. Name-based protection is covered by ProtectedPathPolicyTests.
        string[] otherProfileData =
        [
            $@"{ChromeData}\Default\profile-file-1",
            $@"{ChromeData}\Default\profile-file-2.db",
            $@"{ChromeData}\Default\SomeFeature\data.bin",
            $@"{ChromeData}\Profile 2\profile-file-3",
            $@"{ChromeData}\root-file",
        ];
        string[] cache =
        [
            $@"{ChromeData}\Default\Cache\Cache_Data\f_000001",
            $@"{ChromeData}\Default\Code Cache\js\abc",
            $@"{ChromeData}\Default\GPUCache\data_0",
            $@"{ChromeData}\Profile 2\Cache\Cache_Data\data_1",
            $@"{ChromeData}\ShaderCache\data_2",
        ];
        foreach (var path in otherProfileData.Concat(cache))
        {
            _host.AddFile(path);
        }

        var result = await ScanAsync(new ChromeCacheRule());

        Assert.Equal(cache.Order(), result.Items.Select(i => i.FullPath).Order());
    }

    [Fact]
    public void ChromeRule_IgnoresNonProfileFolders()
    {
        _host.FileSystem.AddDirectory($@"{ChromeData}\Default");
        _host.FileSystem.AddDirectory($@"{ChromeData}\Crashpad");
        _host.FileSystem.AddDirectory($@"{ChromeData}\Safe Browsing");

        var roots = new ChromeCacheRule().GetTargets(Context).Select(t => t.Root).ToList();

        Assert.Contains($@"{ChromeData}\Default\Cache", roots);
        Assert.DoesNotContain(roots, r => r.Contains("Crashpad", StringComparison.Ordinal) || r.Contains("Safe Browsing", StringComparison.Ordinal));
    }

    [Fact]
    public void BrowserRule_NotInstalled_IsNotApplicable()
    {
        Assert.Equal(AvailabilityStatus.NotApplicable, new ChromeCacheRule().CheckAvailability(Context).Status);
        Assert.Equal(AvailabilityStatus.NotApplicable, new FirefoxCacheRule().CheckAvailability(Context).Status);
    }

    [Theory]
    [InlineData("chrome", @"Google\Chrome\User Data")]
    [InlineData("msedge", @"Microsoft\Edge\User Data")]
    [InlineData("firefox", @"Mozilla\Firefox\Profiles")]
    public void BrowserRule_Running_IsBlocked(string process, string dataFolder)
    {
        _host.FileSystem.AddDirectory(Path.Combine(Local, dataFolder));
        _host.Environment.RunningProcesses.Add(process);
        ICleanerRule rule = process switch
        {
            "chrome" => new ChromeCacheRule(),
            "msedge" => new EdgeCacheRule(),
            _ => new FirefoxCacheRule(),
        };

        var availability = rule.CheckAvailability(Context);

        Assert.Equal(AvailabilityStatus.Blocked, availability.Status);
        Assert.Contains("Fermez", availability.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirefoxRule_ListsOnlyLocalCache2()
    {
        const string localProfile = $@"{Local}\Mozilla\Firefox\Profiles\abc.default-release";
        const string roamingProfile = @"C:\Users\Alice\AppData\Roaming\Mozilla\Firefox\Profiles\abc.default-release";
        _host.AddFile($@"{localProfile}\cache2\entries\0A1B2C");
        _host.AddFile($@"{localProfile}\startupCache\scriptCache.bin");
        _host.AddFile($@"{localProfile}\other-file.bin");
        _host.AddFile($@"{roamingProfile}\cache2\not-local.bin");
        _host.AddFile($@"{roamingProfile}\profile-file.db");

        var result = await ScanAsync(new FirefoxCacheRule());

        Assert.Equal($@"{localProfile}\cache2\entries\0A1B2C", Assert.Single(result.Items).FullPath);
    }

    [Fact]
    public async Task ThumbnailRule_ListsOnlyCacheDatabases()
    {
        const string explorer = $@"{Local}\Microsoft\Windows\Explorer";
        _host.AddFile($@"{explorer}\thumbcache_256.db");
        _host.AddFile($@"{explorer}\iconcache_32.db");
        _host.AddFile($@"{explorer}\ExplorerStartupLog.etl");
        _host.AddFile($@"{explorer}\sub\thumbcache_1.db");

        var result = await ScanAsync(new ThumbnailCacheRule());

        Assert.Equal(
            new[] { $@"{explorer}\iconcache_32.db", $@"{explorer}\thumbcache_256.db" },
            result.Items.Select(i => i.FullPath).Order());
    }

    [Fact]
    public async Task UserErrorReportsRule_ListsArchivedAndQueuedReports()
    {
        const string wer = $@"{Local}\Microsoft\Windows\WER";
        _host.AddFile($@"{wer}\ReportArchive\AppCrash_1\Report.wer");
        _host.AddFile($@"{wer}\ReportQueue\AppCrash_2\memory.hdmp");

        var result = await ScanAsync(new UserErrorReportsRule());

        Assert.Equal(2, result.ItemCount);
    }

    [Fact]
    public async Task WindowsUpdateRule_ListsDownloads_WhenElevated()
    {
        var host = new TestHost(new FakeEnvironment { IsElevated = true });
        host.AddFile(@"C:\Windows\SoftwareDistribution\Download\abc\update.cab", 1000);
        host.AddFile(@"C:\Windows\SoftwareDistribution\DataStore\DataStore.edb", 1000);

        var report = await host.CreateScanEngine().ScanAsync([new WindowsUpdateDownloadRule()], new ScanOptions());

        Assert.Equal(@"C:\Windows\SoftwareDistribution\Download\abc\update.cab", Assert.Single(report.Categories[0].Items).FullPath);
    }

    [Fact]
    public void RecycleBinRule_MeasuresAndEmpties()
    {
        var bin = new FakeRecycleBin { SizeBytes = 5000, ItemCount = 4 };
        var rule = new RecycleBinRule(bin);

        Assert.Equal(new CustomScanSummary(5000, 4), rule.Measure(Context));
        Assert.Empty(rule.GetTargets(Context));
        Assert.False(rule.IsEnabledByDefault);

        var outcome = rule.Execute(Context);

        Assert.True(outcome.Success);
        Assert.Equal(5000, outcome.BytesFreed);
        Assert.Equal(1, bin.EmptyCalls);
    }

    [Fact]
    public void RecycleBinRule_EmptyBin_DoesNotCallShell()
    {
        var bin = new FakeRecycleBin();

        var outcome = new RecycleBinRule(bin).Execute(Context);

        Assert.True(outcome.Success);
        Assert.Equal(0, bin.EmptyCalls);
    }

    [Fact]
    public void RecycleBinRule_ShellFailure_IsReported()
    {
        var bin = new FakeRecycleBin { SizeBytes = 10, ItemCount = 1, EmptyResult = unchecked((int)0x80004005) };

        var outcome = new RecycleBinRule(bin).Execute(Context);

        Assert.False(outcome.Success);
        Assert.Contains("0x80004005", outcome.ErrorMessage, StringComparison.Ordinal);
    }
}
