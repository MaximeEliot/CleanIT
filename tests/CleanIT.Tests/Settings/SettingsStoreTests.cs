using System.IO.Abstractions.TestingHelpers;
using CleanIT.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanIT.Tests.Settings;

public class SettingsStoreTests
{
    private const string FilePath = @"C:\Users\Alice\AppData\Roaming\CleanIT\settings.json";
    private readonly MockFileSystem _fs = new();

    private SettingsStore Store => new(_fs, FilePath, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void Load_MissingFile_ReturnsSafeDefaults()
    {
        var settings = Store.Load();

        Assert.False(settings.PermanentDeletion);
        Assert.Equal(24, settings.MinimumTempFileAgeHours);
        Assert.Empty(settings.Exclusions);
        Assert.Null(settings.SelectedRuleIds);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var saved = new AppSettings
        {
            Exclusions = [@"C:\Keep", "*.docx"],
            PermanentDeletion = true,
            MinimumTempFileAgeHours = 48,
            SelectedRuleIds = ["user-temp"],
        };

        Assert.True(Store.Save(saved));
        var loaded = Store.Load();

        Assert.Equal(saved.Exclusions, loaded.Exclusions);
        Assert.True(loaded.PermanentDeletion);
        Assert.Equal(48, loaded.MinimumTempFileAgeHours);
        Assert.Equal(["user-temp"], loaded.SelectedRuleIds!);
        Assert.False(_fs.File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaults()
    {
        _fs.AddFile(FilePath, new MockFileData("{ not json"));

        var settings = Store.Load();

        Assert.False(settings.PermanentDeletion);
    }

    [Fact]
    public void Load_SanitizesOutOfRangeValues()
    {
        _fs.AddFile(FilePath, new MockFileData("""{ "MinimumTempFileAgeHours": -5, "Exclusions": ["", " C:\\A ", "c:\\a"] }"""));

        var settings = Store.Load();

        Assert.Equal(0, settings.MinimumTempFileAgeHours);
        Assert.Equal([@"C:\A"], settings.Exclusions);
    }
}
