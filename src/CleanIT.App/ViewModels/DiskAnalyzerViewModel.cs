using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using CleanIT.App.Logging;
using CleanIT.App.Services;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Cleaning;
using CleanIT.Core.DiskAnalysis;
using CleanIT.Core.Models;
using CleanIT.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CleanIT.App.ViewModels;

/// <summary>"Analyseur d'espace" tab. Strictly read-only: nothing can be deleted from here.</summary>
public sealed partial class DiskAnalyzerViewModel : ObservableObject
{
    private static readonly CultureInfo French = FrenchCulture.Instance;

    private const int MaxListedInDialog = 10;

    private readonly DiskAnalyzer _analyzer;
    private readonly ManualDeletionService _deletionService;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly ILogger<DiskAnalyzerViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string _rootPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(BrowseCommand), nameof(DeleteFilesCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Choisissez un lecteur ou un dossier, puis lancez l'analyse.";

    [ObservableProperty]
    private string? _summaryText;

    [ObservableProperty]
    private FileEntry? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeHeader), nameof(DateHeader), nameof(PathHeader))]
    private string _sortProperty = nameof(FileEntry.SizeBytes);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeHeader), nameof(DateHeader), nameof(PathHeader))]
    private bool _sortDescending = true;

    public DiskAnalyzerViewModel(
        DiskAnalyzer analyzer,
        ManualDeletionService deletionService,
        SettingsService settings,
        IEnvironmentInfo environment,
        IDialogService dialogs,
        ILogger<DiskAnalyzerViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _analyzer = analyzer;
        _deletionService = deletionService;
        _settings = settings;
        _dialogs = dialogs;
        _logger = logger;
        _rootPath = environment.SystemDrive;
        Drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable && d.IsReady)
            .Select(d => d.RootDirectory.FullName)
            .ToList();
        LargestFilesView = CollectionViewSource.GetDefaultView(LargestFiles);
        ApplySort();
    }

    public IReadOnlyList<string> Drives { get; }

    public ObservableCollection<DirectoryNodeViewModel> Tree { get; } = [];

    public ObservableCollection<FileEntry> LargestFiles { get; } = [];

    /// <summary>Sorted view of <see cref="LargestFiles"/> bound to the list.</summary>
    public ICollectionView LargestFilesView { get; }

    public string SizeHeader => Header("Taille", nameof(FileEntry.SizeBytes));

    public string DateHeader => Header("Modifié le", nameof(FileEntry.LastWriteUtc));

    public string PathHeader => Header("Chemin", nameof(FileEntry.FullPath));

    /// <summary>
    /// Sorts the largest files by the given column. Clicking the active column again reverses the order;
    /// a new column starts descending (biggest / most recent first), except the path which starts A→Z.
    /// </summary>
    [RelayCommand]
    private void SortFiles(string? property)
    {
        if (property is not (nameof(FileEntry.SizeBytes) or nameof(FileEntry.LastWriteUtc) or nameof(FileEntry.FullPath)))
        {
            return;
        }

        if (property == SortProperty)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortProperty = property;
            SortDescending = property != nameof(FileEntry.FullPath);
        }

        ApplySort();
    }

    private void ApplySort()
    {
        using (LargestFilesView.DeferRefresh())
        {
            LargestFilesView.SortDescriptions.Clear();
            LargestFilesView.SortDescriptions.Add(new SortDescription(
                SortProperty, SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        }
    }

    private bool CanDeleteFiles(IList? items) => !IsBusy;

    /// <summary>
    /// Deletes the selected files after an explicit confirmation. Files in system locations,
    /// excluded by the user, folders and links are refused before the confirmation is shown.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteFiles))]
    private async Task DeleteFilesAsync(IList? items)
    {
        var selected = items?.OfType<FileEntry>().ToList() ?? [];
        if (selected.Count == 0)
        {
            _dialogs.ShowInfo("Supprimer", "Sélectionnez un ou plusieurs fichiers dans la liste (Ctrl ou Maj pour en choisir plusieurs).");
            return;
        }

        var exclusions = _settings.Current.Exclusions;
        var checks = selected.Select(f => (File: f, Check: _deletionService.Check(f.FullPath, exclusions))).ToList();
        var allowed = checks.Where(c => c.Check.IsAllowed).Select(c => c.File).ToList();
        var refused = checks.Where(c => !c.Check.IsAllowed).ToList();

        if (allowed.Count == 0)
        {
            _dialogs.ShowError(
                "Suppression impossible",
                "Aucun des fichiers sélectionnés ne peut être supprimé :\n\n"
                + string.Join("\n", refused.Take(MaxListedInDialog).Select(r => $"• {r.File.FullPath}\n   {r.Check.RefusalReason}")));
            return;
        }

        var mode = _settings.Current.PermanentDeletion ? CleanupMode.Permanent : CleanupMode.RecycleBin;
        var message = BuildDeleteConfirmation(allowed, refused, mode, checks.Any(c => c.Check.IsAllowed && c.Check.IsApplicationData));
        if (!_dialogs.Confirm("Confirmer la suppression", message, isWarning: true, confirmLabel: "Supprimer"))
        {
            StatusText = "Suppression annulée : rien n'a été supprimé.";
            return;
        }

        _logger.CleanupConfirmed(mode, allowed.Count, allowed.Sum(f => f.SizeBytes));
        IsBusy = true;
        StatusText = "Suppression en cours…";
        try
        {
            var result = await _deletionService.DeleteAsync(allowed.Select(f => f.FullPath).ToList(), mode, userConfirmed: true, exclusions);
            var failed = result.Errors.Select(e => e.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in allowed.Where(f => !failed.Contains(f.FullPath)))
            {
                LargestFiles.Remove(file);
            }

            StatusText = string.Format(
                French,
                "{0:N0} fichier(s) supprimé(s), {1}. Les tailles de l'arborescence seront mises à jour à la prochaine analyse.",
                result.ItemsDeleted,
                ByteSizeFormatter.Format(result.BytesFreed));
            ShowDeleteSummary(result);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.OperationFailed(ex, "manual deletion");
            StatusText = "La suppression a échoué.";
            _dialogs.ShowError("Suppression", "La suppression a échoué : " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildDeleteConfirmation(
        IReadOnlyList<FileEntry> allowed,
        IReadOnlyList<(FileEntry File, ManualDeletionCheck Check)> refused,
        CleanupMode mode,
        bool touchesApplicationData)
    {
        var text = new StringBuilder();
        text.AppendLine(French, $"Supprimer {allowed.Count} fichier(s) ({ByteSizeFormatter.Format(allowed.Sum(f => f.SizeBytes))}) ?\n");
        foreach (var file in allowed.Take(MaxListedInDialog))
        {
            text.AppendLine(French, $"  • {ByteSizeFormatter.Format(file.SizeBytes)} — {file.FullPath}");
        }

        if (allowed.Count > MaxListedInDialog)
        {
            text.AppendLine(French, $"  … et {allowed.Count - MaxListedInDialog} autre(s)");
        }

        text.AppendLine();
        text.AppendLine(mode == CleanupMode.Permanent
            ? "ATTENTION : les fichiers seront supprimés DÉFINITIVEMENT, sans passer par la corbeille."
            : "Les fichiers seront envoyés à la corbeille et pourront être restaurés. L'espace ne sera libéré qu'une fois la corbeille vidée.");

        if (touchesApplicationData)
        {
            text.AppendLine("\nCertains fichiers se trouvent dans AppData : ils appartiennent à une application, qui pourrait ne plus fonctionner correctement.");
        }

        if (refused.Count > 0)
        {
            text.AppendLine(French, $"\n{refused.Count} fichier(s) sélectionné(s) seront ignorés (emplacement protégé, exclusion ou fichier introuvable).");
        }

        text.Append("\nVoulez-vous continuer ?");
        return text.ToString();
    }

    private void ShowDeleteSummary(CleanupResult result)
    {
        var text = new StringBuilder();
        text.AppendLine(French, $"{result.ItemsDeleted:N0} fichier(s) supprimé(s) — {ByteSizeFormatter.Format(result.BytesFreed)}.");
        if (result.Mode == CleanupMode.RecycleBin && result.ItemsDeleted > 0)
        {
            text.AppendLine("Ils sont dans la corbeille : videz-la pour libérer réellement l'espace.");
        }

        if (result.Errors.Count > 0)
        {
            text.AppendLine(French, $"\n{result.Errors.Count} fichier(s) non supprimé(s) :");
            foreach (var error in result.Errors.Take(MaxListedInDialog))
            {
                text.AppendLine(French, $"• {error.FullPath}\n   {ErrorClassifier.Describe(error.Kind)} — {error.Message}");
            }
        }

        if (result.Errors.Count == 0)
        {
            _dialogs.ShowInfo("Suppression terminée", text.ToString());
        }
        else
        {
            _dialogs.ShowError("Suppression terminée avec des erreurs", text.ToString());
        }
    }

    private string Header(string label, string property) =>
        property == SortProperty ? label + (SortDescending ? " ▼" : " ▲") : label;

    private bool CanAnalyze() => !IsBusy && !string.IsNullOrWhiteSpace(RootPath);

    private bool CanBrowse() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private void Browse()
    {
        if (_dialogs.PickFolder("Dossier à analyser", RootPath) is { } folder)
        {
            RootPath = folder;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        Tree.Clear();
        LargestFiles.Clear();
        SummaryText = null;
        StatusText = "Analyse en cours…";
        var progress = new Progress<DiskAnalysisProgress>(p =>
            StatusText = string.Format(French, "{0:N0} fichier(s), {1} — {2}", p.FilesScanned, ByteSizeFormatter.Format(p.BytesScanned), p.CurrentDirectory));

        try
        {
            var result = await _analyzer.AnalyzeAsync(RootPath, DiskAnalyzer.DefaultLargestFileCount, progress, cancellationToken);
            Tree.Add(new DirectoryNodeViewModel(result.Root, result.Root.SizeBytes) { IsExpanded = true });
            foreach (var file in result.LargestFiles)
            {
                LargestFiles.Add(file);
            }

            SummaryText = string.Format(
                French,
                "{0} dans {1:N0} fichier(s){2}",
                ByteSizeFormatter.Format(result.Root.SizeBytes),
                result.TotalFiles,
                result.InaccessibleDirectories > 0 ? string.Format(French, " — {0:N0} dossier(s) inaccessible(s) non comptés", result.InaccessibleDirectories) : string.Empty);
            StatusText = string.Format(French, "Analyse terminée en {0:0.0} s.", result.Duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse annulée.";
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.OperationFailed(ex, "disk analysis");
            StatusText = "L'analyse a échoué : " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Opens Explorer with the file selected (read-only action).</summary>
    [RelayCommand]
    private void ShowInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var argument = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        try
        {
            using (Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true }))
            {
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.OperationFailed(ex, "open explorer");
            _dialogs.ShowError("Explorateur", "Impossible d'ouvrir l'Explorateur : " + ex.Message);
        }
    }
}
