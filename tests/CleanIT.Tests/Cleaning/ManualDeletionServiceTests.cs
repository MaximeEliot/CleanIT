using CleanIT.Core.Cleaning;
using CleanIT.Core.Models;
using CleanIT.Core.Safety;
using CleanIT.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanIT.Tests.Cleaning;

public class ManualDeletionServiceTests
{
    private const string Video = @"C:\Users\Alice\Videos\vacances.mp4";
    private readonly TestHost _host = new();
    private readonly FakeRecycleBinDeleter _recycle;
    private readonly ManualDeletionService _service;

    public ManualDeletionServiceTests()
    {
        _recycle = new FakeRecycleBinDeleter(_host.FileSystem);
        _service = new ManualDeletionService(
            _host.FileSystem,
            _host.Policy,
            [_recycle, new PermanentFileDeleter(_host.FileSystem)],
            NullLogger<ManualDeletionService>.Instance);
    }

    [Theory]
    [InlineData(@"C:\Users\Alice\Videos\vacances.mp4")]
    [InlineData(@"C:\Users\Alice\Downloads\setup.iso")]
    [InlineData(@"C:\Users\Alice\Documents\archive.zip")]
    [InlineData(@"C:\Users\Alice\Desktop\film.mkv")]
    [InlineData(@"D:\Jeux\vieux-jeu\data.pak")]
    [InlineData(@"C:\Users\Alice\AppData\Local\App\cache.bin")]
    public void Policy_AllowsUserPickedFilesOutsideSystemLocations(string path)
    {
        Assert.False(_host.Policy.IsProtectedFromManualDeletion(path, out var reason), reason);
    }

    [Theory]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\swapfile.sys")]
    [InlineData(@"C:\Windows\explorer.exe")]
    [InlineData(@"C:\Windows\Temp\big.tmp")]
    [InlineData(@"C:\Windows\System32\config\SOFTWARE")]
    [InlineData(@"C:\Program Files\App\app.dll")]
    [InlineData(@"C:\Program Files (x86)\App\app.dll")]
    [InlineData(@"D:\Program Files\App\app.dll")]
    [InlineData(@"C:\ProgramData\Microsoft\Windows\x.dat")]
    [InlineData(@"C:\System Volume Information\snapshot")]
    [InlineData(@"C:\$Recycle.Bin\S-1-5\$R1.mp4")]
    [InlineData(@"C:\Users\Alice\ntuser.dat")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Microsoft\Windows\UsrClass.dat")]
    [InlineData(@"C:\PROGRA~1\App\app.dll")]
    [InlineData(@"C:\")]
    [InlineData(@"\\server\share\file.iso")]
    public void Policy_RefusesSystemLocationsAndFiles(string path)
    {
        Assert.True(_host.Policy.IsProtectedFromManualDeletion(path, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void Policy_FlagsApplicationData()
    {
        Assert.True(_host.Policy.IsInApplicationData(@"C:\Users\Alice\AppData\Local\App\cache.bin"));
        Assert.False(_host.Policy.IsInApplicationData(Video));
    }

    [Fact]
    public async Task Delete_SendsFileToRecycleBin()
    {
        _host.AddFile(Video, 5000);

        var result = await _service.DeleteAsync([Video], CleanupMode.RecycleBin, userConfirmed: true, []);

        Assert.Equal(Video, Assert.Single(_recycle.Recycled));
        Assert.Equal(5000, result.BytesFreed);
        Assert.Equal(1, result.ItemsDeleted);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Delete_Permanent_RemovesTheFile()
    {
        _host.AddFile(Video);

        await _service.DeleteAsync([Video], CleanupMode.Permanent, userConfirmed: true, []);

        Assert.False(_host.FileSystem.File.Exists(Video));
        Assert.Empty(_recycle.Recycled);
    }

    [Fact]
    public async Task Delete_WithoutConfirmation_IsRefused()
    {
        _host.AddFile(Video);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteAsync([Video], CleanupMode.RecycleBin, userConfirmed: false, []));

        Assert.True(_host.FileSystem.File.Exists(Video));
    }

    [Fact]
    public async Task Delete_DryRun_IsNotSupported()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DeleteAsync([Video], CleanupMode.DryRun, userConfirmed: true, []));
    }

    [Fact]
    public async Task Delete_SkipsProtectedExcludedAndMissing_ButDeletesTheRest()
    {
        const string system = @"C:\Windows\System32\big.dll";
        const string kept = @"C:\Users\Alice\Videos\garder.mp4";
        const string missing = @"C:\Users\Alice\Videos\absent.mp4";
        _host.AddFile(system);
        _host.AddFile(kept);
        _host.AddFile(Video);

        var result = await _service.DeleteAsync([system, kept, missing, Video], CleanupMode.Permanent, true, [kept]);

        Assert.True(_host.FileSystem.File.Exists(system));
        Assert.True(_host.FileSystem.File.Exists(kept));
        Assert.False(_host.FileSystem.File.Exists(Video));
        Assert.Equal(1, result.ItemsDeleted);
        Assert.Contains(result.Errors, e => e.FullPath == system && e.Kind == CleanupErrorKind.Protected);
        Assert.Contains(result.Errors, e => e.FullPath == kept && e.Kind == CleanupErrorKind.Excluded);
        Assert.Contains(result.Errors, e => e.FullPath == missing && e.Kind == CleanupErrorKind.Vanished);
    }

    [Fact]
    public async Task Delete_NeverDeletesDirectoriesOrLinks()
    {
        const string folder = @"C:\Users\Alice\Videos\Album";
        const string link = @"C:\Users\Alice\Videos\lien.mp4";
        _host.AddFile($@"{folder}\a.mp4");
        _host.AddFile(link);
        _host.FileSystem.File.SetAttributes(link, FileAttributes.ReparsePoint);

        var result = await _service.DeleteAsync([folder, link], CleanupMode.Permanent, true, []);

        Assert.True(_host.FileSystem.File.Exists($@"{folder}\a.mp4"));
        Assert.True(_host.FileSystem.File.Exists(link));
        Assert.Equal(0, result.ItemsDeleted);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public async Task Delete_LockedFile_IsReportedAndOthersContinue()
    {
        const string other = @"C:\Users\Alice\Videos\autre.mp4";
        _host.AddFile(Video);
        _host.AddFile(other);
        _recycle.Failures[Video] = CleanupErrorKind.Locked;

        var result = await _service.DeleteAsync([Video, other], CleanupMode.RecycleBin, true, []);

        Assert.Equal(1, result.ItemsDeleted);
        Assert.Equal(CleanupErrorKind.Locked, Assert.Single(result.Errors).Kind);
    }

    [Fact]
    public void Check_ReportsRefusalAndApplicationData()
    {
        const string appData = @"C:\Users\Alice\AppData\Local\App\cache.bin";
        _host.AddFile(appData);
        _host.AddFile(@"C:\Windows\big.log");

        var allowed = _service.Check(appData, []);
        var refused = _service.Check(@"C:\Windows\big.log", []);

        Assert.True(allowed.IsAllowed);
        Assert.True(allowed.IsApplicationData);
        Assert.False(refused.IsAllowed);
        Assert.NotNull(refused.RefusalReason);
    }

    [Fact]
    public void CleanupPolicy_StillProtectsPersonalFolders()
    {
        // The relaxed manual check must not leak into automatic cleanup.
        Assert.True(_host.Policy.IsProtected(Video, out _));
        Assert.True(new CleanupGuard(_host.Policy, ExclusionList.Empty).Evaluate(Video, [@"C:\Users\Alice"]).Reason == SkipReason.Protected);
    }
}
