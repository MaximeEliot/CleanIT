using CleanIT.Core.Abstractions;

namespace CleanIT.Core.Rules;

/// <summary>The V1 catalog of cleanup categories, in display order.</summary>
public static class DefaultRules
{
    public static IReadOnlyList<ICleanerRule> Create(IRecycleBin recycleBin) =>
    [
        new UserTempRule(),
        new SystemTempRule(),
        new RecycleBinRule(recycleBin),
        new ChromeCacheRule(),
        new EdgeCacheRule(),
        new FirefoxCacheRule(),
        new ThumbnailCacheRule(),
        new UserErrorReportsRule(),
        new SystemErrorReportsRule(),
        new WindowsUpdateDownloadRule(),
    ];
}
