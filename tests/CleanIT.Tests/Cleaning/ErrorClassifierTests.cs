using CleanIT.Core.Cleaning;
using CleanIT.Core.Models;

namespace CleanIT.Tests.Cleaning;

public class ErrorClassifierTests
{
    public static TheoryData<Exception, CleanupErrorKind> Cases => new()
    {
        { new IOException("sharing", unchecked((int)0x80070020)), CleanupErrorKind.Locked },
        { new IOException("lock", unchecked((int)0x80070021)), CleanupErrorKind.Locked },
        { new UnauthorizedAccessException(), CleanupErrorKind.AccessDenied },
        { new PathTooLongException(), CleanupErrorKind.PathTooLong },
        { new FileNotFoundException(), CleanupErrorKind.Vanished },
        { new DirectoryNotFoundException(), CleanupErrorKind.Vanished },
        { new IOException("other"), CleanupErrorKind.Unknown },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void FromException_MapsKnownFailures(Exception exception, CleanupErrorKind expected)
    {
        Assert.Equal(expected, ErrorClassifier.FromException(exception).Kind);
    }

    [Theory]
    [InlineData(32, CleanupErrorKind.Locked)]
    [InlineData(5, CleanupErrorKind.AccessDenied)]
    [InlineData(206, CleanupErrorKind.PathTooLong)]
    [InlineData(2, CleanupErrorKind.Vanished)]
    [InlineData(12345, CleanupErrorKind.Unknown)]
    public void FromWin32Code_MapsKnownCodes(int code, CleanupErrorKind expected)
    {
        Assert.Equal(expected, ErrorClassifier.FromWin32Code(code));
    }
}
