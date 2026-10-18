using System.Globalization;
using CleanIT.Core.Cleaning;
using CleanIT.Core.Formatting;
using CleanIT.Core.Models;

namespace CleanIT.App.ViewModels;

/// <summary>Final summary of a cleanup or a simulation.</summary>
public sealed class CleanupSummaryViewModel
{
    private static readonly CultureInfo French = FrenchCulture.Instance;

    public CleanupSummaryViewModel(CleanupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
        Errors = result.Errors.Select(e => new ErrorRow(e.FullPath, ErrorClassifier.Describe(e.Kind), e.Message)).ToList();
        ErrorBreakdown = result.Errors
            .GroupBy(e => e.Kind)
            .OrderByDescending(g => g.Count())
            .Select(g => string.Format(French, "{0} : {1:N0}", ErrorClassifier.Describe(g.Key), g.Count()))
            .ToList();
    }

    public CleanupResult Result { get; }

    public bool IsDryRun => Result.Mode == CleanupMode.DryRun;

    public string Title => (Result.WasCancelled, IsDryRun) switch
    {
        (true, _) => "Opération annulée — résultat partiel",
        (false, true) => "Simulation terminée — aucun fichier n'a été supprimé",
        _ => "Nettoyage terminé",
    };

    public string FreedLabel => IsDryRun ? "Espace qui serait libéré" : "Espace libéré";

    public string FreedText => ByteSizeFormatter.Format(Result.BytesFreed);

    public string ItemsText => string.Format(
        French,
        IsDryRun ? "{0:N0} fichier(s) seraient supprimés" : "{0:N0} fichier(s) supprimé(s), {1:N0} dossier(s) vide(s) retiré(s)",
        Result.ItemsDeleted,
        Result.DirectoriesRemoved);

    public string ErrorsText => Result.Errors.Count == 0
        ? "Aucune erreur."
        : string.Format(French, "{0:N0} élément(s) non supprimé(s) :", Result.Errors.Count);

    public string? Note => Result.Mode switch
    {
        CleanupMode.RecycleBin when Result.ItemsDeleted > 0 =>
            "Les fichiers ont été déplacés dans la corbeille : vous pouvez les restaurer. L'espace disque sera réellement libéré une fois la corbeille vidée.",
        CleanupMode.Permanent => "Les fichiers ont été supprimés définitivement.",
        _ => null,
    };

    public string DurationText => string.Format(French, "Durée : {0:0.0} s", Result.Duration.TotalSeconds);

    public IReadOnlyList<string> ErrorBreakdown { get; }

    public IReadOnlyList<ErrorRow> Errors { get; }

    public IReadOnlyList<string> DryRunEntries => Result.DryRunEntries;

    public IReadOnlyList<CategoryCleanupResult> Categories => Result.Categories;

    public sealed record ErrorRow(string Path, string Kind, string Message);
}
