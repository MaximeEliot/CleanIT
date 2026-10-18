using System.Globalization;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Formatting;
using CleanIT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanIT.App.ViewModels;

/// <summary>One checkable category of the cleanup screen.</summary>
public sealed partial class RuleItemViewModel : ObservableObject
{
    private const int MaxDetailItems = 200;
    private readonly Action<RuleItemViewModel> _selectionChanged;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isSelectable = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText), nameof(CountText), nameof(StatusText), nameof(HasResult), nameof(TopItems), nameof(SkippedText), nameof(HasItems))]
    private CategoryScanResult? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _availabilityMessage;

    public RuleItemViewModel(ICleanerRule rule, bool isSelected, Action<RuleItemViewModel> selectionChanged)
    {
        Rule = rule;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    public ICleanerRule Rule { get; }

    public string Id => Rule.Id;

    public string DisplayName => Rule.DisplayName;

    public string Description => Rule.Description;

    public bool RequiresElevation => Rule.RequiresElevation;

    public string CategoryName => Rule.Category switch
    {
        CleanerCategory.TemporaryFiles => "Fichiers temporaires",
        CleanerCategory.RecycleBin => "Corbeille",
        CleanerCategory.BrowserCache => "Navigateurs",
        CleanerCategory.SystemCache => "Caches et rapports Windows",
        CleanerCategory.WindowsUpdate => "Windows Update",
        _ => "Autres",
    };

    public bool HasResult => Result is not null;

    public bool HasItems => Result is { ItemCount: > 0 };

    public string SizeText => Result is { Availability.IsAvailable: true } r ? ByteSizeFormatter.Format(r.TotalBytes) : string.Empty;

    public string CountText => Result is { Availability.IsAvailable: true } r
        ? string.Format(FrenchCulture.Instance, "{0:N0} élément(s)", r.ItemCount)
        : string.Empty;

    /// <summary>Short status shown under the name: availability problem, or "non analysé".</summary>
    public string StatusText => Result switch
    {
        { Availability.IsAvailable: false } r => r.Availability.Reason ?? "Indisponible",
        not null => string.Empty,
        _ => AvailabilityMessage ?? "Non analysé",
    };

    /// <summary>Largest files of the category, for the details panel.</summary>
    public IReadOnlyList<CleanupItem> TopItems => Result?.Items.OrderByDescending(i => i.SizeBytes).Take(MaxDetailItems).ToList() ?? [];

    public string SkippedText => Result is { SkippedCount: > 0 } r
        ? string.Format(FrenchCulture.Instance, "{0:N0} élément(s) ignoré(s) (protégés, exclus, récents, verrouillés ou inaccessibles).", r.SkippedCount)
        : string.Empty;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged(this);
}
