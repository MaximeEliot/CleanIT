using CleanIT.Core.Models;

namespace CleanIT.Core.Rules;

/// <summary>Content of %TEMP%. Recent files are skipped: they may still be in use by a running program.</summary>
public sealed class UserTempRule : DirectoryCleanerRule
{
    public override string Id => "user-temp";

    public override string DisplayName => "Fichiers temporaires de l'utilisateur";

    public override string Description => "Contenu du dossier %TEMP% (fichiers de plus de 24 h par défaut).";

    public override CleanerCategory Category => CleanerCategory.TemporaryFiles;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [new ScanTarget(context.Environment.UserTemp, ApplyMinimumAge: true)];
    }
}

/// <summary>Content of C:\Windows\Temp (administrator only).</summary>
public sealed class SystemTempRule : DirectoryCleanerRule
{
    public override string Id => "system-temp";

    public override string DisplayName => "Fichiers temporaires du système";

    public override string Description => "Contenu du dossier C:\\Windows\\Temp.";

    public override CleanerCategory Category => CleanerCategory.TemporaryFiles;

    public override bool RequiresElevation => true;

    public override bool IsEnabledByDefault => false;

    public override IReadOnlyList<ScanTarget> GetTargets(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [new ScanTarget(Path.Combine(context.Environment.WindowsDirectory, "Temp"), ApplyMinimumAge: true)];
    }
}
