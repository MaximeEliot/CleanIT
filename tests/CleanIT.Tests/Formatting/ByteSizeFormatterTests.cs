using CleanIT.Core.Formatting;

namespace CleanIT.Tests.Formatting;

public class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 octet")]
    [InlineData(1, "1 octet")]
    [InlineData(512, "512 octets")]
    [InlineData(1024, "1 Ko")]
    [InlineData(1536, "1,5 Ko")]
    [InlineData(10 * 1024 * 1024 + 512 * 1024, "10,5 Mo")]
    [InlineData(350L * 1024 * 1024, "350 Mo")]
    [InlineData(5L * 1024 * 1024 * 1024 / 4, "1,25 Go")]
    [InlineData(3L * 1024 * 1024 * 1024 * 1024, "3 To")]
    public void Format_UsesFrenchUnitsAndSeparators(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes));
    }
}
