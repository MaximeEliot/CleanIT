namespace CleanIT.App.Services;

/// <summary>Modal dialogs, abstracted so view models stay free of WPF windows.</summary>
public interface IDialogService
{
    /// <summary>
    /// Explicit confirmation. Cancel is the default (Enter/Escape) and the confirm button
    /// only becomes clickable after a short delay. Returns true only on an explicit click.
    /// </summary>
    bool Confirm(string title, string message, bool isWarning = false, string confirmLabel = "Confirmer", string cancelLabel = "Annuler");

    void ShowInfo(string title, string message);

    void ShowError(string title, string message);

    string? PickFolder(string title, string? initialDirectory = null);

    string? PickSaveFile(string title, string defaultFileName, string filter);
}
