using CleanIT.Core.Safety;

namespace CleanIT.Tests.Safety;

public class ExclusionListTests
{
    [Theory]
    [InlineData(@"C:\Temp\keep", @"C:\Temp\keep\a.txt", true)]
    [InlineData(@"C:\Temp\keep\", @"C:\TEMP\KEEP\sub\a.txt", true)]
    [InlineData(@"C:\Temp\keep", @"C:\Temp\keep", true)]
    [InlineData(@"C:\Temp\keep", @"C:\Temp\keeper\a.txt", false)]
    [InlineData(@"C:\Temp\a.txt", @"C:\Temp\a.txt", true)]
    [InlineData("*.docx", @"C:\Temp\x\report.DOCX", true)]
    [InlineData("setup*.log", @"C:\Temp\setup_2024.log", true)]
    [InlineData("*.docx", @"C:\Temp\report.txt", false)]
    public void IsExcluded_MatchesPathsAndPatterns(string entry, string path, bool expected)
    {
        var list = new ExclusionList([entry]);

        Assert.Equal(expected, list.IsExcluded(PathNormalizer.TryNormalize(path)!, out _));
    }

    [Fact]
    public void InvalidEntries_AreReportedAndIgnored()
    {
        var list = new ExclusionList([@"\\server\share", "", "   ", @"relative\dir"]);

        Assert.Equal(2, list.InvalidEntries.Count);
        Assert.False(list.IsExcluded(@"C:\Temp\a.txt", out _));
    }
}
