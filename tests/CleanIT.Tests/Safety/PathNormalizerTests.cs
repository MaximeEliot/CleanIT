using CleanIT.Core.Safety;

namespace CleanIT.Tests.Safety;

public class PathNormalizerTests
{
    [Theory]
    [InlineData(@"C:\Windows", @"C:\Windows")]
    [InlineData(@"c:\windows\", @"C:\windows")]
    [InlineData(@"C:/Windows/Temp", @"C:\Windows\Temp")]
    [InlineData(@"C:\Windows\Temp\..\System32", @"C:\Windows\System32")]
    [InlineData(@"C:\Windows\.\System32", @"C:\Windows\System32")]
    [InlineData(@"\\?\C:\Windows\System32", @"C:\Windows\System32")]
    [InlineData(@"C:\Windows.\System32 ", @"C:\Windows\System32")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"C:\\Windows\\\Temp", @"C:\Windows\Temp")]
    public void TryNormalize_ReturnsCanonicalForm(string input, string expected)
    {
        Assert.Equal(expected, PathNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"Windows\System32")]
    [InlineData(@"\Windows")]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData(@"\\?\UNC\server\share")]
    [InlineData(@"C:\Temp\*.tmp")]
    [InlineData(@"C:\Temp\file.txt:hidden")]
    [InlineData(@"C:Windows")]
    public void TryNormalize_RejectsUnsupportedPaths(string? input)
    {
        Assert.Null(PathNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp\a.txt", @"C:\Windows\Temp", true)]
    [InlineData(@"C:\WINDOWS\TEMP\a.txt", @"C:\Windows\Temp", true)]
    [InlineData(@"C:\Windows\Temp", @"C:\Windows\Temp", false)]
    [InlineData(@"C:\Windows\TempX\a.txt", @"C:\Windows\Temp", false)]
    [InlineData(@"C:\a.txt", @"C:\", true)]
    public void IsStrictlyUnder_HandlesBoundaries(string path, string parent, bool expected)
    {
        Assert.Equal(expected, PathNormalizer.IsStrictlyUnder(path, parent));
    }
}
