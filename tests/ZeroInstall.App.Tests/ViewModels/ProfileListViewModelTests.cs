using NSubstitute;
using ZeroInstall.App.Models;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.ViewModels;

public class ProfileListViewModelTests
{
    private readonly IProfileManager _profileManager = Substitute.For<IProfileManager>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly ISessionState _session = Substitute.For<ISessionState>();
    private readonly IAppSettings _appSettings = Substitute.For<IAppSettings>();
    private readonly ProfileListViewModel _sut;

    public ProfileListViewModelTests()
    {
        _appSettings.Current.Returns(new AppSettings());
        _sut = new ProfileListViewModel(_profileManager, _navService, _session, _appSettings);
    }

    [Fact]
    public void Title_ShouldBeProfiles()
    {
        _sut.Title.Should().Be("Profiles");
    }

    [Fact]
    public async Task OnNavigatedTo_LoadsLocalProfiles()
    {
        var profiles = new List<MigrationProfile>
        {
            new() { Name = "Office PC", Author = "Tech1" },
            new() { Name = "Dev Workstation", Author = "Tech2" }
        };
        _profileManager.ListLocalProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(profiles.AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.LocalProfiles.Should().HaveCount(2);
        _sut.LocalProfiles[0].Name.Should().Be("Office PC");
    }

    [Fact]
    public async Task OnNavigatedTo_WithNasPath_LoadsNasProfiles()
    {
        _appSettings.Current.Returns(new AppSettings { NasPath = @"\\nas\profiles" });
        var nasProfiles = new List<MigrationProfile>
        {
            new() { Name = "Shared Profile" }
        };
        _profileManager.ListLocalProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MigrationProfile>().AsReadOnly());
        _profileManager.ListNasProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(nasProfiles.AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.HasNasPath.Should().BeTrue();
        _sut.NasProfiles.Should().ContainSingle();
    }

    [Fact]
    public void CreateNew_NavigatesToProfileEditor()
    {
        _sut.CreateNewCommand.Execute(null);

        _navService.Received(1).NavigateTo<ProfileEditorViewModel>();
    }

    [Fact]
    public void EditProfile_NavigatesToProfileEditorWithProfile()
    {
        var profile = new MigrationProfile { Name = "Test" };
        _sut.SelectedProfile = profile;

        var editorVm = new ProfileEditorViewModel(
            Substitute.For<IProfileManager>(),
            Substitute.For<INavigationService>());
        _navService.CurrentViewModel.Returns(editorVm);

        _sut.EditProfileCommand.Execute(null);

        _navService.Received(1).NavigateTo<ProfileEditorViewModel>();
    }

    [Fact]
    public async Task DeleteProfile_RemovesFromListAndCallsService()
    {
        var profile = new MigrationProfile { Name = "ToDelete" };
        _sut.LocalProfiles.Add(profile);
        _sut.SelectedProfile = profile;

        await _sut.DeleteProfileCommand.ExecuteAsync(null);

        _sut.LocalProfiles.Should().BeEmpty();
        await _profileManager.Received(1).DeleteLocalProfileAsync("ToDelete", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoadProfile_AppliesTransportAndNavigatesBack()
    {
        var profile = new MigrationProfile
        {
            Name = "Standard",
            Transport = new ProfileTransportPreferences { PreferredMethod = TransportMethod.DirectWiFi }
        };
        _sut.SelectedProfile = profile;

        _sut.LoadProfileCommand.Execute(null);

        _session.TransportMethod = TransportMethod.DirectWiFi;
        _navService.Received(1).NavigateTo<WelcomeViewModel>();
    }

    [Fact]
    public async Task Refresh_ClearsAndReloads()
    {
        _sut.LocalProfiles.Add(new MigrationProfile { Name = "Old" });
        _profileManager.ListLocalProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MigrationProfile> { new() { Name = "New" } }.AsReadOnly());

        await _sut.RefreshCommand.ExecuteAsync(null);

        _sut.LocalProfiles.Should().ContainSingle()
            .Which.Name.Should().Be("New");
    }

    [Fact]
    public async Task OnNavigatedTo_WithEmptyList_SetsNoError()
    {
        _profileManager.ListLocalProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MigrationProfile>().AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.LocalProfiles.Should().BeEmpty();
        _sut.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void GoBack_CallsNavigationGoBack()
    {
        _sut.GoBackCommand.Execute(null);

        _navService.Received(1).GoBack();
    }
}
