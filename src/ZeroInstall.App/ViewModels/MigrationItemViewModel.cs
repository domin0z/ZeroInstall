using CommunityToolkit.Mvvm.ComponentModel;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Wraps a <see cref="MigrationItem"/> with UI-specific property change notifications.
/// </summary>
public partial class MigrationItemViewModel : ObservableObject
{
    private readonly MigrationItem _model;
    private readonly Action? _selectionChangedCallback;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => _model.DisplayName;
    public string Description => _model.Description;
    public MigrationItemType ItemType => _model.ItemType;
    public MigrationTier RecommendedTier => _model.RecommendedTier;
    public long EstimatedSizeBytes => _model.EstimatedSizeBytes;

    public string EstimatedSizeFormatted => FormatBytes(_model.EstimatedSizeBytes);

    public MigrationItemViewModel(MigrationItem model, Action? selectionChangedCallback = null)
    {
        _model = model;
        _selectionChangedCallback = selectionChangedCallback;
        _isSelected = model.IsSelected;
    }

    /// <summary>
    /// Gets the underlying model.
    /// </summary>
    internal MigrationItem Model => _model;

    partial void OnIsSelectedChanged(bool value)
    {
        _model.IsSelected = value;
        _selectionChangedCallback?.Invoke();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0
            ? $"{size:F0} {units[order]}"
            : $"{size:F1} {units[order]}";
    }
}
