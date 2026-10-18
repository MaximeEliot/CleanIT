using System.Collections.ObjectModel;
using System.Globalization;
using CleanIT.Core.DiskAnalysis;
using CleanIT.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanIT.App.ViewModels;

/// <summary>
/// Tree node of the disk analyzer. Children view models are created lazily on expansion:
/// a whole drive can contain hundreds of thousands of folders.
/// </summary>
public sealed partial class DirectoryNodeViewModel : ObservableObject
{
    private static readonly DirectoryNodeViewModel Placeholder = new();
    private readonly DirectoryNode? _node;
    private readonly long _parentSize;

    [ObservableProperty]
    private bool _isExpanded;

    public DirectoryNodeViewModel(DirectoryNode node, long parentSize)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _parentSize = parentSize;
        if (node.Children.Count > 0)
        {
            Children.Add(Placeholder);
        }
    }

    private DirectoryNodeViewModel()
    {
    }

    public ObservableCollection<DirectoryNodeViewModel> Children { get; } = [];

    public string Name => _node?.Name ?? "…";

    public string FullPath => _node?.FullPath ?? string.Empty;

    public long SizeBytes => _node?.SizeBytes ?? 0;

    public string SizeText => _node is null ? string.Empty : ByteSizeFormatter.Format(_node.SizeBytes);

    /// <summary>Share of the parent folder, 0-100.</summary>
    public double Percent => _parentSize <= 0 ? 100 : 100.0 * SizeBytes / _parentSize;

    public string PercentText => _node is null ? string.Empty : Percent.ToString("0.0", FrenchCulture.Instance) + " %";

    public string FileCountText => _node is null
        ? string.Empty
        : string.Format(FrenchCulture.Instance, "{0:N0} fichier(s)", _node.FileCount);

    public string? Warning => _node switch
    {
        { IsReparsePoint: true } => "Lien (non suivi)",
        { IsAccessible: false } => "Accès refusé",
        _ => null,
    };

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || _node is null || Children.Count != 1 || !ReferenceEquals(Children[0], Placeholder))
        {
            return;
        }

        Children.Clear();
        foreach (var child in _node.Children)
        {
            Children.Add(new DirectoryNodeViewModel(child, _node.SizeBytes));
        }
    }
}
