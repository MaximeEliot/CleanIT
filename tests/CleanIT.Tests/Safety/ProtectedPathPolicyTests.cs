using CleanIT.Core.Safety;
using CleanIT.Tests.TestDoubles;

namespace CleanIT.Tests.Safety;

public class ProtectedPathPolicyTests
{
    private readonly ProtectedPathPolicy _policy = new(new FakeEnvironment());

    [Theory]
    // Drive roots and invalid paths
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"relative\path.txt")]
    [InlineData(@"\\server\share\file.tmp")]
    // System folders
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\explorer.exe")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"c:\windows\system32\drivers\etc\hosts")]
    [InlineData(@"C:\Windows\SysWOW64\a.dll")]
    [InlineData(@"C:\Windows\WinSxS\x\y.dll")]
    [InlineData(@"C:\Windows\SoftwareDistribution\DataStore\DataStore.edb")]
    [InlineData(@"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files (x86)\App\app.exe")]
    [InlineData(@"D:\Program Files\App\app.exe")]
    [InlineData(@"D:\Windows\System32\a.dll")]
    [InlineData(@"C:\ProgramData\Microsoft\Crypto\key")]
    [InlineData(@"C:\System Volume Information\x")]
    [InlineData(@"C:\$Recycle.Bin\S-1-5-21\$R123.txt")]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    // Traversal / aliasing tricks
    [InlineData(@"C:\Windows\Temp\..\System32\kernel32.dll")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\..\..\Roaming\x.txt")]
    [InlineData(@"\\?\C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\PROGRA~1\App\app.exe")]
    [InlineData(@"C:\Windows.\System32\a.dll")]
    [InlineData(@"C:/Windows/System32/a.dll")]
    // Profile
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\Alice")]
    [InlineData(@"C:\Users\Alice\ntuser.dat")]
    [InlineData(@"C:\Users\Alice\Documents\report.docx")]
    [InlineData(@"C:\Users\Alice\Desktop\a.txt")]
    [InlineData(@"C:\Users\Alice\Downloads\setup.exe")]
    [InlineData(@"C:\Users\Alice\Pictures\a.jpg")]
    [InlineData(@"C:\Users\Alice\OneDrive\a.txt")]
    [InlineData(@"C:\Users\Alice\AppData\Roaming\app\config.json")]
    [InlineData(@"C:\Users\Alice\AppData\Local\SomeApp\data.db")]
    [InlineData(@"C:\Users\Bob\AppData\Local\Temp\a.tmp")]
    // Category roots themselves (only their content may be cleaned)
    [InlineData(@"C:\Windows\Temp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download")]
    // Browser data other than cache
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Cookies")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Login Data")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\History")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Local Storage\leveldb\000003.log")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Local State")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Microsoft\Edge\User Data\Default\Web Data")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Mozilla\Firefox\Profiles\abc.default\places.sqlite")]
    [InlineData(@"C:\Users\Alice\AppData\Roaming\Mozilla\Firefox\Profiles\abc.default\logins.json")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Cache\Cookies")]
    public void IsProtected_ReturnsTrue_ForCriticalLocations(string path)
    {
        Assert.True(_policy.IsProtected(path, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp\setup.log")]
    [InlineData(@"C:\Windows\Temp\sub\dir\file.tmp")]
    [InlineData(@"C:\WINDOWS\TEMP\file.tmp")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download\abc\update.cab")]
    [InlineData(@"C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_x\Report.wer")]
    [InlineData(@"C:\ProgramData\Microsoft\Windows\WER\ReportQueue\a.wer")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\a.tmp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\sub\b.tmp")]
    [InlineData(@"\\?\C:\Users\Alice\AppData\Local\Temp\a.tmp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Microsoft\Windows\Explorer\thumbcache_256.db")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Microsoft\Windows\WER\ReportArchive\x\Report.wer")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Cache\Cache_Data\f_000001")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Profile 1\Code Cache\js\index")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\GPUCache\data_0")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\ShaderCache\data_1")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Microsoft\Edge\User Data\Default\Cache\Cache_Data\data_2")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Mozilla\Firefox\Profiles\abc.default-release\cache2\entries\0A1B")]
    [InlineData(@"D:\Games\cache\file.bin")]
    public void IsProtected_ReturnsFalse_ForCleanableLocations(string path)
    {
        Assert.False(_policy.IsProtected(path, out _));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\Alice")]
    [InlineData(@"C:\Users\Alice\AppData\Local")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files\App")]
    [InlineData(@"C:\Users\Alice\Documents\Temp")]
    [InlineData(@"D:\Program Files\App")]
    [InlineData(@"D:\Windows")]
    public void IsSafeCleanupRoot_RejectsDangerousRoots(string root)
    {
        Assert.False(_policy.IsSafeCleanupRoot(root));
    }

    [Theory]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp")]
    [InlineData(@"C:\Windows\Temp")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download")]
    [InlineData(@"D:\Temp")]
    public void IsSafeCleanupRoot_AcceptsCategoryRoots(string root)
    {
        Assert.True(_policy.IsSafeCleanupRoot(root));
    }

    [Theory]
    [InlineData(@"C:\Users\Alice")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\Alice\AppData\Local")]
    [InlineData(@"C:\Users\Alice\Documents")]
    public void MisconfiguredTemp_DoesNotOpenProtectedFolders(string dangerousTemp)
    {
        var policy = new ProtectedPathPolicy(new FakeEnvironment { UserTemp = dangerousTemp });

        Assert.True(policy.IsProtected(@"C:\Users\Alice\Documents\report.docx", out _));
        Assert.True(policy.IsProtected(@"C:\Users\Alice\notes.txt", out _));
        Assert.True(policy.IsProtected(@"C:\Users\Alice\AppData\Local\App\db.sqlite", out _));
    }

    [Fact]
    public void CustomTempOutsideProfile_IsCleanableButRootIsNot()
    {
        var policy = new ProtectedPathPolicy(new FakeEnvironment { UserTemp = @"D:\Temp" });

        Assert.False(policy.IsProtected(@"D:\Temp\a.tmp", out _));
        Assert.True(policy.IsProtected(@"D:\Temp", out _));
    }
}
