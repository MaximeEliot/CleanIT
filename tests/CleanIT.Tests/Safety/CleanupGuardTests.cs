using CleanIT.Core.Models;
using CleanIT.Core.Safety;
using CleanIT.Tests.TestDoubles;

namespace CleanIT.Tests.Safety;

public class CleanupGuardTests
{
    private static readonly string[] TempRoot = [@"C:\Users\Alice\AppData\Local\Temp"];

    private static CleanupGuard CreateGuard(params string[] exclusions) =>
        new(new ProtectedPathPolicy(new FakeEnvironment()), new ExclusionList(exclusions));

    [Fact]
    public void Allows_FileUnderRuleRoot()
    {
        var verdict = CreateGuard().Evaluate(@"C:\Users\Alice\AppData\Local\Temp\a.tmp", TempRoot);

        Assert.True(verdict.IsAllowed);
    }

    [Theory]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp")]
    [InlineData(@"C:\Windows\Temp\a.tmp")]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\..\Other\a.txt")]
    public void Denies_PathsOutsideRuleRoots(string path)
    {
        var verdict = CreateGuard().Evaluate(path, TempRoot);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(SkipReason.OutsideRuleRoots, verdict.Reason);
    }

    [Fact]
    public void Denies_ProtectedPath_EvenWhenRuleDeclaresItsParentAsRoot()
    {
        // A buggy rule declaring "C:\Windows" as root must not be able to delete system files.
        var verdict = CreateGuard().Evaluate(@"C:\Windows\System32\kernel32.dll", [@"C:\Windows"]);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(SkipReason.Protected, verdict.Reason);
    }

    [Fact]
    public void Denies_UserExclusion()
    {
        var verdict = CreateGuard("*.important").Evaluate(@"C:\Users\Alice\AppData\Local\Temp\x.important", TempRoot);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(SkipReason.Excluded, verdict.Reason);
    }
}
