using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using CleanIT.App.Logging;
using CleanIT.App.Services;
using CleanIT.Core.Abstractions;
using CleanIT.Core.DiskAnalysis;
using CleanIT.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CleanIT.App.ViewModels;

/// <summary>"Analyseur d'espace" tab. Strictly read-only: nothing can be deleted from here.</summary>
public sealed partial class DiskAnalyzerViewModel : ObservableObject
{
    private static readonly CultureInfo French = FrenchCulture.Instance;

    private readonly DiskAnalyzer _analyzer;
    private readonly IDialogService _dialogs;
    private readonly ILogger<DiskAnalyzerViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string _rootPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(BrowseCommand))]
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

    public DiskAnalyzerViewModel(DiskAnalyzer analyzer, IEnvironmentInfo environment, IDialogService dialogs, ILogger<DiskAnalyzerViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _analyzer = analyzer;
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
