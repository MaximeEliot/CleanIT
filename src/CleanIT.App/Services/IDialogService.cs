namespace CleanIT.App.Services;

/// <summary>Modal dialogs, abstracted so view models stay free of WPF windows.</summary>
public interface IDialogService
{
    /// <summary>Yes/No question; "No" is the default button.</summary>
    bool Confirm(string title, string message, bool isWarning = false);

    void ShowInfo(string title, string message);

    void ShowError(string title, string message);

    string? PickFolder(string title, string? initialDirectory = null);

    string? PickSaveFile(string title, string defaultFileName, string filter);
}
