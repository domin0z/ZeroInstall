using CommunityToolkit.Mvvm.ComponentModel;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Wraps a <see cref="UserMapping"/> for two-way binding in the restore config UI.
/// </summary>
public partial class UserMappingEntryViewModel : ObservableObject
{
    private readonly UserMapping _model;

    public string SourceUsername => _model.SourceUser.Username;
    public string SourceProfilePath => _model.SourceUser.ProfilePath;

    [ObservableProperty]
    private string _destinationUsername;

    [ObservableProperty]
    private bool _createIfMissing;

    public UserMappingEntryViewModel(UserMapping model)
    {
        _model = model;
        _destinationUsername = model.DestinationUsername;
        _createIfMissing = model.CreateIfMissing;
    }

    internal UserMapping Model => _model;

    partial void OnDestinationUsernameChanged(string value)
    {
        _model.DestinationUsername = value;
        _model.DestinationProfilePath = $@"C:\Users\{value}";
    }

    partial void OnCreateIfMissingChanged(bool value)
    {
        _model.CreateIfMissing = value;
    }
}
