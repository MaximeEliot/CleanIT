using System.Windows;
using Microsoft.Win32;

namespace CleanIT.App.Services;

public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } window ? window : null;

    public bool Confirm(string title, string message, bool isWarning = false)
    {
        var icon = isWarning ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var result = Owner is { } owner
            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public void ShowInfo(string title, string message) => Show(title, message, MessageBoxImage.Information);

    public void ShowError(string title, string message) => Show(title, message, MessageBoxImage.Error);

    public string? PickFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickSaveFile(string title, string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = defaultFileName, Filter = filter, AddExtension = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    private static void Show(string title, string message, MessageBoxImage icon)
    {
        if (Owner is { } owner)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }
}
