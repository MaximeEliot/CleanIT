using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
    }

    public IReadOnlyList<string> Drives { get; }

    public ObservableCollection<DirectoryNodeViewModel> Tree { get; } = [];

    public ObservableCollection<FileEntry> LargestFiles { get; } = [];

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
