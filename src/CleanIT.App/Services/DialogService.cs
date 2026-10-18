using System.Windows;
using CleanIT.App.Views;
using Microsoft.Win32;

namespace CleanIT.App.Services;

public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } window ? window : null;

    public bool Confirm(string title, string message, bool isWarning = false, string confirmLabel = "Confirmer", string cancelLabel = "Annuler")
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel, cancelLabel, isWarning);
        if (Owner is { } owner)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
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
