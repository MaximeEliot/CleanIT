using System.IO.Abstractions;
using CleanIT.Core.Models;

namespace CleanIT.Core.Rules;

/// <summary>
/// Cache of a Chromium-based browser. Only the cache folders are targeted (Cache, Code Cache,
/// GPUCache, shader caches): cookies, passwords, history, bookmarks and local storage live
/// elsewhere in the profile and are additionally blacklisted by the protected-path policy.
/// </summary>
public abstract class ChromiumCacheRule : DirectoryCleanerRule
{
    private static readonly string[] ProfileCacheFolders = ["Cache", "Code Cache", "GPUCache"];
    private static readonly string[] SharedCacheFolders = ["ShaderCache", "GrShaderCache"];

    /// <summary>Path of "User Data" relative to %LOCALAPPDATA%.</summary>
    protected abstract string UserDataRelativePath { get; }

    /// <summary>Process name, used to ask the user to close the browser first.</summary>
    protected abstract string ProcessName { get; }

    protected abstract string BrowserName { get; }

    public override CleanerCategory Category => CleanerCategory.BrowserCache;

    public override string DisplayName => $"Cache de {BrowserName}";

    public override string Description =>
        $"Cache web de {BrowserName} (tous les profils). Les mots de passe, cookies et l'historique ne sont jamais touchés.";

    public override RuleAvailability CheckAvailability(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.FileSystem.Directory.Exists(UserDataPath(context)))
        {
            return RuleAvailability.NotApplicable($"{BrowserName} n'est pas installé.");
        }

        return context.Environment.IsProcessRunning(ProcessName)
            ? RuleAvailability.Blocked($"Fermez {BrowserName} pour nettoyer son cache.")
            : RuleAvailability.Available;
    }

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userData = UserDataPath(context);
        var targets = new List<ScanTarget>();

        foreach (var profile in EnumerateProfiles(context.FileSystem, userData))
        {
            targets.AddRange(ProfileCacheFolders.Select(folder => new ScanTarget(Path.Combine(profile, folder))));
        }

        targets.AddRange(SharedCacheFolders.Select(folder => new ScanTarget(Path.Combine(userData, folder))));
        return targets;
    }

    private string UserDataPath(RuleContext context) =>
        Path.Combine(context.Environment.LocalAppData, UserDataRelativePath);

    /// <summary>"Default", "Profile 1", "Profile 2", "Guest Profile", "System Profile".</summary>
    private static IEnumerable<string> EnumerateProfiles(IFileSystem fileSystem, string userData)
    {
        IEnumerable<string> directories;
        try
        {
            directories = fileSystem.Directory.Exists(userData) ? fileSystem.Directory.GetDirectories(userData) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            directories = [];
        }

        return directories.Where(dir =>
        {
            var name = Path.GetFileName(dir);
            return name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase)
                || name.Equals("System Profile", StringComparison.OrdinalIgnoreCase);
        });
    }
}

public sealed class ChromeCacheRule : ChromiumCacheRule
{
    public override string Id => "chrome-cache";

    protected override string BrowserName => "Google Chrome";

    protected override string UserDataRelativePath => @"Google\Chrome\User Data";

    protected override string ProcessName => "chrome";
}

public sealed class EdgeCacheRule : ChromiumCacheRule
{
    public override string Id => "edge-cache";

    protected override string BrowserName => "Microsoft Edge";

    protected override string UserDataRelativePath => @"Microsoft\Edge\User Data";

    protected override string ProcessName => "msedge";
}

/// <summary>
/// Firefox keeps its disk cache in the <em>local</em> profile ("%LOCALAPPDATA%\Mozilla\Firefox\Profiles\x\cache2"),
/// separate from the roaming profile holding passwords, cookies and history, which is never touched.
/// </summary>
public sealed class FirefoxCacheRule : DirectoryCleanerRule
{
    public override string Id => "firefox-cache";

    public override string DisplayName => "Cache de Mozilla Firefox";

    public override string Description =>
        "Cache web de Firefox (tous les profils). Les mots de passe, cookies et l'historique ne sont jamais touchés.";

    public override CleanerCategory Category => CleanerCategory.BrowserCache;

    public override RuleAvailability CheckAvailability(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.FileSystem.Directory.Exists(ProfilesPath(context)))
        {
            return RuleAvailability.NotApplicable("Mozilla Firefox n'est pas installé.");
        }

        return context.Environment.IsProcessRunning("firefox")
            ? RuleAvailability.Blocked("Fermez Mozilla Firefox pour nettoyer son cache.")
            : RuleAvailability.Available;
    }

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var profiles = ProfilesPath(context);
        try
        {
            return context.FileSystem.Directory.Exists(profiles)
                ? context.FileSystem.Directory.GetDirectories(profiles).Select(p => new ScanTarget(Path.Combine(p, "cache2"))).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string ProfilesPath(RuleContext context) =>
        Path.Combine(context.Environment.LocalAppData, @"Mozilla\Firefox\Profiles");
}
