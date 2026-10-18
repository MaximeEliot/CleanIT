using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Windows.Data;
using CleanIT.App.Logging;
using CleanIT.App.Services;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Cleaning;
using CleanIT.Core.Formatting;
using CleanIT.Core.Models;
using CleanIT.Core.Scanning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CleanIT.App.ViewModels;

/// <summary>
/// "Nettoyage" tab. Workflow: select categories → analyse → review the report →
/// clean (after explicit confirmation) → summary. A cleanup can only start from the
/// report of the last analysis; changing the selection to an unanalysed category discards it.
/// </summary>
public sealed partial class CleanupViewModel : ObservableObject
{
    private static readonly CultureInfo French = FrenchCulture.Instance;

    private readonly ScanEngine _scanEngine;
    private readonly CleanupEngine _cleanupEngine;
    private readonly IEnvironmentInfo _environment;
    private readonly IFileSystem _fileSystem;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly ILogger<CleanupViewModel> _logger;
    private bool _initializing = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand), nameof(AnalyzeCommand))]
    [NotifyPropertyChangedFor(nameof(TotalSelectedText), nameof(HasReport))]
    private ScanReport? _report;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand), nameof(AnalyzeCommand), nameof(ExportCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Sélectionnez les catégories puis lancez l'analyse. Rien n'est supprimé sans votre confirmation.";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText), nameof(CleanButtonText))]
    private bool _isDryRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private CleanupSummaryViewModel? _summary;

    [ObservableProperty]
    private RuleItemViewModel? _selectedRule;

    public CleanupViewModel(
        ScanEngine scanEngine,
        CleanupEngine cleanupEngine,
        IReadOnlyList<ICleanerRule> rules,
        IEnvironmentInfo environment,
        IFileSystem fileSystem,
        SettingsService settings,
        IDialogService dialogs,
        IElevationService elevation,
        StartupOptions startupOptions,
        ILogger<CleanupViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(startupOptions);
        _scanEngine = scanEngine;
        _cleanupEngine = cleanupEngine;
        _environment = environment;
        _fileSystem = fileSystem;
        _settings = settings;
        _dialogs = dialogs;
        _elevation = elevation;
        _logger = logger;

        var saved = settings.Current.SelectedRuleIds;
        var preselected = startupOptions.PreselectedRuleIds;
        foreach (var rule in rules)
        {
            var selected = preselected.Count > 0 ? preselected.Contains(rule.Id)
                : saved is not null ? saved.Contains(rule.Id)
                : rule.IsEnabledByDefault;
            Rules.Add(new RuleItemViewModel(rule, selected, OnRuleSelectionChanged));
        }

        RulesView = CollectionViewSource.GetDefaultView(Rules);
        RulesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RuleItemViewModel.CategoryName)));
        SelectedRule = Rules.FirstOrDefault();

        RefreshAvailability();
        settings.Changed += (_, _) => OnPropertyChanged(nameof(ModeText));
        _initializing = false;
    }

    public ObservableCollection<RuleItemViewModel> Rules { get; } = [];

    public ICollectionView RulesView { get; }

    public bool HasReport => Report is not null;

    public string ModeText => IsDryRun
        ? "Mode simulation : rien ne sera supprimé, la liste des fichiers concernés sera affichée."
        : _settings.Current.PermanentDeletion
            ? "Mode suppression DÉFINITIVE (modifiable dans les paramètres)."
            : "Les fichiers seront envoyés à la corbeille (restaurables).";

    public string CleanButtonText => IsDryRun ? "Simuler le nettoyage" : "Nettoyer";

    public string TotalSelectedText
    {
        get
        {
            if (Report is null)
            {
                return string.Empty;
            }

            var selected = CleanableSelection().ToList();
            return string.Format(
                French,
                "Espace récupérable : {0} ({1:N0} élément(s) dans {2} catégorie(s) sélectionnée(s))",
                ByteSizeFormatter.Format(selected.Sum(r => r.Result!.TotalBytes)),
                selected.Sum(r => r.Result!.ItemCount),
                selected.Count);
        }
    }

    private bool CanAnalyze() => !IsBusy;

    private bool CanClean() => !IsBusy && Report is not null;

    private bool CanExport() => !IsBusy && Summary is not null;

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        var selected = Rules.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.ShowInfo("Analyse", "Cochez au moins une catégorie à analyser.");
            return;
        }

        if (!_environment.IsElevated && selected.Any(r => r.RequiresElevation))
        {
            var names = string.Join("\n", selected.Where(r => r.RequiresElevation).Select(r => "  • " + r.DisplayName));
            var restart = _dialogs.Confirm(
                "Droits administrateur requis",
                "Ces catégories nécessitent les droits administrateur :\n" + names
                + "\n\nRelancer CleanIT en tant qu'administrateur ?\n\n(Non : l'analyse continue sans ces catégories.)");
            if (restart && _elevation.RestartElevated(selected.Select(r => r.Id)))
            {
                return;
            }
        }

        IsBusy = true;
        Summary = null;
        Report = null;
        foreach (var rule in Rules)
        {
            rule.Result = null;
        }

        ProgressValue = 0;
        IsProgressIndeterminate = false;
        StatusText = "Analyse en cours…";
        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressValue = 100.0 * p.RuleIndex / Math.Max(1, p.RuleCount);
            StatusText = string.Format(French, "Analyse : {0} ({1}/{2}) — {3:N0} fichier(s), {4}", p.DisplayName, p.RuleIndex + 1, p.RuleCount, p.FilesFound, ByteSizeFormatter.Format(p.BytesFound));
        });

        try
        {
            var options = new ScanOptions
            {
                MinimumFileAge = TimeSpan.FromHours(_settings.Current.MinimumTempFileAgeHours),
                Exclusions = _settings.Current.Exclusions,
            };
            var report = await _scanEngine.ScanAsync(selected.Select(r => r.Rule).ToList(), options, progress, cancellationToken);

            foreach (var category in report.Categories)
            {
                Rules.First(r => r.Id == category.RuleId).Result = category;
            }

            Report = report;
            ProgressValue = 100;
            StatusText = string.Format(French, "Analyse terminée : {0} récupérables. Vérifiez le rapport avant de nettoyer.", ByteSizeFormatter.Format(report.TotalBytes));
        }
        catch (OperationCanceledException)
        {
            ProgressValue = 0;
            StatusText = "Analyse annulée.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.OperationFailed(ex, "scan");
            StatusText = "L'analyse a échoué.";
            _dialogs.ShowError("Analyse", "L'analyse a échoué : " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanClean))]
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        var report = Report;
        if (report is null)
        {
            return;
        }

        var categories = CleanableSelection().ToList();
        if (categories.Count == 0)
        {
            _dialogs.ShowInfo("Nettoyage", "Aucun élément à nettoyer dans les catégories sélectionnées.");
            return;
        }

        var mode = IsDryRun ? CleanupMode.DryRun
            : _settings.Current.PermanentDeletion ? CleanupMode.Permanent
            : CleanupMode.RecycleBin;

        var confirmed = false;
        if (mode != CleanupMode.DryRun)
        {
            var dangerous = mode == CleanupMode.Permanent || categories.Any(c => c.Result!.IsCustom);
            confirmed = _dialogs.Confirm("Confirmer le nettoyage", BuildConfirmation(categories, mode), dangerous);
            if (!confirmed)
            {
                StatusText = "Nettoyage annulé : rien n'a été supprimé.";
                return;
            }

            _logger.CleanupConfirmed(mode, categories.Sum(c => c.Result!.ItemCount), categories.Sum(c => c.Result!.TotalBytes));
        }

        IsBusy = true;
        Summary = null;
        ProgressValue = 0;
        StatusText = mode == CleanupMode.DryRun ? "Simulation en cours…" : "Nettoyage en cours…";
        var progress = new Progress<CleanupProgress>(p =>
        {
            ProgressValue = p.TotalItems == 0 ? 100 : 100.0 * p.ProcessedItems / p.TotalItems;
            StatusText = string.Format(French, "{0} : {1:N0} / {2:N0} — {3}", p.CurrentCategory ?? "Finalisation", p.ProcessedItems, p.TotalItems, ByteSizeFormatter.Format(p.ProcessedBytes));
        });

        try
        {
            var request = new CleanupRequest
            {
                RuleIds = categories.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
                Mode = mode,
                UserConfirmed = confirmed,
                Exclusions = _settings.Current.Exclusions,
            };
            var result = await _cleanupEngine.CleanAsync(report, request, progress, cancellationToken);
            Summary = new CleanupSummaryViewModel(result);
            StatusText = Summary.Title;

            if (mode != CleanupMode.DryRun)
            {
                // The report no longer reflects the disk: a new analysis is required.
                Report = null;
                foreach (var rule in Rules)
                {
                    rule.Result = null;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.OperationFailed(ex, "cleanup");
            StatusText = "Le nettoyage a échoué.";
            _dialogs.ShowError("Nettoyage", "Le nettoyage a échoué : " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        if (Summary is null)
        {
            return;
        }

        var path = _dialogs.PickSaveFile(
            "Exporter le rapport",
            string.Format(CultureInfo.InvariantCulture, "CleanIT-{0:yyyyMMdd-HHmmss}.txt", DateTime.Now),
            "Fichier texte (*.txt)|*.txt");
        if (path is null)
        {
            return;
        }

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"CleanIT — {Summary.Title}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Date : {DateTime.Now.ToString("F", French)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"{Summary.FreedLabel} : {Summary.FreedText}");
        text.AppendLine(Summary.ItemsText);
        if (Summary.DryRunEntries.Count > 0)
        {
            text.AppendLine().AppendLine("Fichiers qui seraient supprimés :");
            foreach (var entry in Summary.DryRunEntries)
            {
                text.AppendLine(entry);
            }
        }

        if (Summary.Errors.Count > 0)
        {
            text.AppendLine().AppendLine("Éléments non supprimés :");
            foreach (var error in Summary.Errors)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"{error.Kind}\t{error.Path}\t{error.Message}");
            }
        }

        try
        {
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Export", "Impossible d'écrire le fichier : " + ex.Message);
        }
    }

    [RelayCommand]
    private void CloseSummary() => Summary = null;

    private IEnumerable<RuleItemViewModel> CleanableSelection() =>
        Rules.Where(r => r.IsSelected && r.Result is { Availability.IsAvailable: true, ItemCount: > 0 });

    private static string BuildConfirmation(IReadOnlyList<RuleItemViewModel> categories, CleanupMode mode)
    {
        var text = new StringBuilder("Les éléments suivants vont être supprimés :\n\n");
        foreach (var category in categories)
        {
            text.AppendLine(French, $"  • {category.DisplayName} — {category.SizeText} ({category.CountText})");
        }

        text.AppendLine(French, $"\nTotal : {ByteSizeFormatter.Format(categories.Sum(c => c.Result!.TotalBytes))}\n");
        text.AppendLine(mode == CleanupMode.Permanent
            ? "ATTENTION : les fichiers seront supprimés DÉFINITIVEMENT, sans passer par la corbeille."
            : "Les fichiers seront envoyés à la corbeille et pourront être restaurés.");

        if (categories.Any(c => c.Result!.IsCustom))
        {
            text.AppendLine("La corbeille sera vidée : son contenu actuel sera supprimé définitivement.");
        }

        text.Append("\nVoulez-vous continuer ?");
        return text.ToString();
    }

    private void OnRuleSelectionChanged(RuleItemViewModel item)
    {
        if (_initializing)
        {
            return;
        }

        if (item.IsSelected && Report is not null && item.Result is null)
        {
            Report = null;
            StatusText = "La sélection a changé : relancez l'analyse avant de nettoyer.";
        }

        OnPropertyChanged(nameof(TotalSelectedText));
        _settings.Update(s => s.SelectedRuleIds = Rules.Where(r => r.IsSelected).Select(r => r.Id).ToList());
    }

    private void RefreshAvailability()
    {
        foreach (var item in Rules)
        {
            RuleAvailability availability;
            try
            {
                availability = item.Rule.CheckAvailability(new RuleContext(_fileSystem, _environment));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                availability = RuleAvailability.Blocked(ex.Message);
            }

            if (availability.Status == AvailabilityStatus.NotApplicable)
            {
                item.IsSelectable = false;
                item.IsSelected = false;
            }

            item.AvailabilityMessage = availability.IsAvailable && item.RequiresElevation && !_environment.IsElevated
                ? "Nécessite les droits administrateur"
                : availability.Reason;
        }
    }
}
