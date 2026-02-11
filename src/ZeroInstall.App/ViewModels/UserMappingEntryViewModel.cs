using CommunityToolkit.Mvvm.ComponentModel;
using ZeroInstall.Core.Enums;
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

    public string? DomainWarning => _model.DomainMigrationWarning;
    public bool ShowDomainWarning => !string.IsNullOrEmpty(_model.DomainMigrationWarning);
    public bool ShowDomainOptions => _model.SourceUser.AccountType != UserAccountType.Local
                                     && _model.SourceUser.AccountType != UserAccountType.Unknown;

    [ObservableProperty]
    private string _destinationUsername;

    [ObservableProperty]
    private bool _createIfMissing;

    [ObservableProperty]
    private PostMigrationAccountAction _postMigrationAction;

    [ObservableProperty]
    private bool _reassignInPlace;

    public UserMappingEntryViewModel(UserMapping model)
    {
        _model = model;
        _destinationUsername = model.DestinationUsername;
        _createIfMissing = model.CreateIfMissing;
        _postMigrationAction = model.PostMigrationAction;
        _reassignInPlace = model.ReassignInPlace;
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

    partial void OnPostMigrationActionChanged(PostMigrationAccountAction value)
    {
        _model.PostMigrationAction = value;
    }

    partial void OnReassignInPlaceChanged(bool value)
    {
        _model.ReassignInPlace = value;
    }
}
