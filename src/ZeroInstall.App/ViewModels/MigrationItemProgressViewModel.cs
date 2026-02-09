using CommunityToolkit.Mvvm.ComponentModel;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Wraps a <see cref="MigrationItem"/> with progress/status tracking for the migration progress view.
/// </summary>
public partial class MigrationItemProgressViewModel : ObservableObject
{
    private readonly MigrationItem _model;

    public string DisplayName => _model.DisplayName;

    [ObservableProperty]
    private MigrationItemStatus _status;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool IsComplete => Status is MigrationItemStatus.Completed or MigrationItemStatus.Skipped;
    public bool IsFailed => Status == MigrationItemStatus.Failed;

    public MigrationItemProgressViewModel(MigrationItem model)
    {
        _model = model;
        _status = model.Status;
        _statusMessage = model.StatusMessage ?? string.Empty;
    }

    partial void OnStatusChanged(MigrationItemStatus value)
    {
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(IsFailed));
    }
}
