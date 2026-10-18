using CleanIT.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanIT.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(IElevationService elevation, CleanupViewModel cleanup)
    {
        IsElevated = elevation.IsElevated;
        Cleanup = cleanup;
    }

    public bool IsElevated { get; }

    public CleanupViewModel Cleanup { get; }

    public string Title => IsElevated ? "CleanIT — Administrateur" : "CleanIT";

    public string ElevationText => IsElevated
        ? "Mode administrateur : toutes les catégories sont disponibles."
        : "Mode standard : les catégories système demanderont les droits administrateur.";

    /// <summary>True while a long-running operation would be interrupted by closing the window.</summary>
    public bool IsBusy => Cleanup.IsBusy;

    /// <summary>Requests cancellation of every running operation.</summary>
    public void CancelAll()
    {
        Cleanup.AnalyzeCancelCommand.Execute(null);
        Cleanup.CleanCancelCommand.Execute(null);
    }
}
