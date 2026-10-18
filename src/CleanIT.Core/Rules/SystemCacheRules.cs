using CleanIT.Core.Models;

namespace CleanIT.Core.Rules;

/// <summary>
/// Explorer thumbnail and icon caches. Windows regenerates them on demand.
/// Explorer usually keeps some of them open: those are reported as locked and skipped.
/// </summary>
public sealed class ThumbnailCacheRule : DirectoryCleanerRule
{
    public override string Id => "thumbnails";

    public override string DisplayName => "Miniatures";

    public override string Description => "Cache des miniatures et des icônes de l'Explorateur (recréé automatiquement).";

    public override CleanerCategory Category => CleanerCategory.SystemCache;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var explorer = Path.Combine(context.Environment.LocalAppData, @"Microsoft\Windows\Explorer");
        return
        [
            new ScanTarget(explorer, "thumbcache_*.db", Recursive: false),
            new ScanTarget(explorer, "iconcache_*.db", Recursive: false),
        ];
    }
}

/// <summary>Windows Error Reporting files of the current user.</summary>
public sealed class UserErrorReportsRule : DirectoryCleanerRule
{
    public override string Id => "user-error-reports";

    public override string DisplayName => "Rapports d'erreurs Windows (utilisateur)";

    public override string Description => "Rapports de plantage archivés ou en attente d'envoi de l'utilisateur.";

    public override CleanerCategory Category => CleanerCategory.SystemCache;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var wer = Path.Combine(context.Environment.LocalAppData, @"Microsoft\Windows\WER");
        return [new ScanTarget(Path.Combine(wer, "ReportArchive")), new ScanTarget(Path.Combine(wer, "ReportQueue"))];
    }
}

/// <summary>Windows Error Reporting files shared by all users (administrator only).</summary>
public sealed class SystemErrorReportsRule : DirectoryCleanerRule
{
    public override string Id => "system-error-reports";

    public override string DisplayName => "Rapports d'erreurs Windows (système)";

    public override string Description => "Rapports de plantage communs à tous les utilisateurs (C:\\ProgramData).";

    public override CleanerCategory Category => CleanerCategory.SystemCache;

    public override bool RequiresElevation => true;

    public override bool IsEnabledByDefault => false;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var wer = Path.Combine(context.Environment.ProgramData, @"Microsoft\Windows\WER");
        return
        [
            new ScanTarget(Path.Combine(wer, "ReportArchive")),
            new ScanTarget(Path.Combine(wer, "ReportQueue")),
            new ScanTarget(Path.Combine(wer, "Temp")),
        ];
    }
}

/// <summary>
/// Windows Update download cache (administrator only). Recent files are skipped so that an
/// update being downloaded or installed is not disturbed.
/// </summary>
public sealed class WindowsUpdateDownloadRule : DirectoryCleanerRule
{
    public override string Id => "windows-update";

    public override string DisplayName => "Anciens fichiers de mise à jour Windows";

    public override string Description => "Fichiers téléchargés par Windows Update (SoftwareDistribution\\Download).";

    public override CleanerCategory Category => CleanerCategory.WindowsUpdate;

    public override bool RequiresElevation => true;

    public override bool IsEnabledByDefault => false;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [new ScanTarget(Path.Combine(context.Environment.WindowsDirectory, @"SoftwareDistribution\Download"), ApplyMinimumAge: true)];
    }
}
