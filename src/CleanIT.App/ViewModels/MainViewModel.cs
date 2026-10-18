using CleanIT.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanIT.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(IElevationService elevation)
    {
        IsElevated = elevation.IsElevated;
    }

    public bool IsElevated { get; }

    public string Title => IsElevated ? "CleanIT — Administrateur" : "CleanIT";

    public string ElevationText => IsElevated
        ? "Mode administrateur : toutes les catégories sont disponibles."
        : "Mode standard : les catégories système demanderont les droits administrateur.";
}
