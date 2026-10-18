using System.Collections.ObjectModel;
using System.Diagnostics;
using CleanIT.App.Services;
using CleanIT.Core.Safety;
using CleanIT.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanIT.App.ViewModels;

/// <summary>"Paramètres" tab. Every change is saved immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private bool _loading;

    [ObservableProperty]
    private bool _permanentDeletion;

    [ObservableProperty]
    private int _minimumTempFileAgeHours;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPatternCommand))]
    private string _newPattern = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveExclusionCommand))]
    private string? _selectedExclusion;

    public SettingsViewModel(SettingsService settings, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _dialogs = dialogs;

        _loading = true;
        PermanentDeletion = settings.Current.PermanentDeletion;
        MinimumTempFileAgeHours = settings.Current.MinimumTempFileAgeHours;
        foreach (var exclusion in settings.Current.Exclusions)
        {
            Exclusions.Add(exclusion);
        }

        _loading = false;
    }

    public ObservableCollection<string> Exclusions { get; } = [];

    public static int MaxMinimumAgeHours => AppSettings.MaxMinimumAgeHours;

    public static string SettingsFilePath => AppPaths.SettingsFile;

    partial void OnPermanentDeletionChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        if (value && !_dialogs.Confirm(
                "Suppression définitive",
                "Avec cette option, les fichiers ne passeront plus par la corbeille : ils seront supprimés définitivement et ne pourront pas être restaurés.\n\nActiver la suppression définitive ?",
                isWarning: true))
        {
            _loading = true;
            PermanentDeletion = false;
            _loading = false;
            return;
        }

        _settings.Update(s => s.PermanentDeletion = value);
    }

    partial void OnMinimumTempFileAgeHoursChanged(int value)
    {
        if (!_loading)
        {
            _settings.Update(s => s.MinimumTempFileAgeHours = Math.Clamp(value, 0, AppSettings.MaxMinimumAgeHours));
        }
    }

    [RelayCommand]
    private void AddFolder()
    {
        if (_dialogs.PickFolder("Dossier à exclure du nettoyage") is { } folder)
        {
            AddExclusion(folder);
        }
    }

    private bool CanAddPattern() => !string.IsNullOrWhiteSpace(NewPattern);

    [RelayCommand(CanExecute = nameof(CanAddPattern))]
    private void AddPattern()
    {
        var entry = NewPattern.Trim();
        if (new ExclusionList([entry]).InvalidEntries.Count > 0)
        {
            _dialogs.ShowError("Exclusion", "Entrée invalide. Utilisez un chemin complet (C:\\Dossier) ou un motif de nom de fichier (*.docx).");
            return;
        }

        AddExclusion(entry);
        NewPattern = string.Empty;
    }

    private bool CanRemoveExclusion() => SelectedExclusion is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveExclusion))]
    private void RemoveExclusion()
    {
        if (SelectedExclusion is { } selected)
        {
            Exclusions.Remove(selected);
            SaveExclusions();
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            using (Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.LogDirectory}\"") { UseShellExecute = true }))
            {
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _dialogs.ShowError("Journal", "Impossible d'ouvrir le dossier des journaux : " + ex.Message);
        }
    }

    private void AddExclusion(string entry)
    {
        if (!Exclusions.Contains(entry, StringComparer.OrdinalIgnoreCase))
        {
            Exclusions.Add(entry);
            SaveExclusions();
        }
    }

    private void SaveExclusions() => _settings.Update(s => s.Exclusions = [.. Exclusions]);
}
