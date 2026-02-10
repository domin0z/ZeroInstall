using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.ViewModels;

public class ProfileEditorViewModelTests
{
    private readonly IProfileManager _profileManager = Substitute.For<IProfileManager>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly ProfileEditorViewModel _sut;

    public ProfileEditorViewModelTests()
    {
        _sut = new ProfileEditorViewModel(_profileManager, _navService);
    }

    [Fact]
    public void Title_ShouldBeProfileEditor()
    {
        _sut.Title.Should().Be("Profile Editor");
    }

    [Fact]
    public void InitialState_IsEmptyNewProfile()
    {
        _sut.ProfileName.Should().BeEmpty();
        _sut.Description.Should().BeEmpty();
        _sut.Author.Should().BeEmpty();
        _sut.UserProfilesEnabled.Should().BeTrue();
        _sut.ApplicationsEnabled.Should().BeTrue();
        _sut.BrowserDataEnabled.Should().BeTrue();
        _sut.SystemSettingsEnabled.Should().BeTrue();
    }

    [Fact]
    public void LoadProfile_PopulatesAllFields()
    {
        var profile = new MigrationProfile
        {
            Name = "Test Profile",
            Description = "A test",
            Author = "Tech1",
            Items = new ProfileItemSelection
            {
                UserProfiles = new ProfileUserProfileSettings { Enabled = false, IncludeAll = false },
                Applications = new ProfileApplicationSettings { Enabled = true, PreferredTier = MigrationTier.RegistryFile },
                BrowserData = new ProfileBrowserSettings { Enabled = true, IncludeBookmarks = false, IncludePasswords = true },
                SystemSettings = new ProfileSystemSettings { Enabled = false, WifiProfiles = false, Certificates = true }
            },
            Transport = new ProfileTransportPreferences
            {
                PreferredMethod = TransportMethod.DirectWiFi,
                NasPath = @"\\nas\test",
                Compression = false
            }
        };

        _sut.LoadProfile(profile);

        _sut.ProfileName.Should().Be("Test Profile");
        _sut.Description.Should().Be("A test");
        _sut.Author.Should().Be("Tech1");
        _sut.UserProfilesEnabled.Should().BeFalse();
        _sut.UserProfilesIncludeAll.Should().BeFalse();
        _sut.PreferredTier.Should().Be(MigrationTier.RegistryFile);
        _sut.IncludeBookmarks.Should().BeFalse();
        _sut.IncludePasswords.Should().BeTrue();
        _sut.SystemSettingsEnabled.Should().BeFalse();
        _sut.WifiProfiles.Should().BeFalse();
        _sut.Certificates.Should().BeTrue();
        _sut.PreferredTransport.Should().Be(TransportMethod.DirectWiFi);
        _sut.NasPath.Should().Be(@"\\nas\test");
        _sut.Compression.Should().BeFalse();
    }

    [Fact]
    public void LoadProfile_WithNull_KeepsDefaults()
    {
        _sut.LoadProfile(null);

        _sut.ProfileName.Should().BeEmpty();
        _sut.UserProfilesEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Save_CallsProfileManagerAndNavigatesBack()
    {
        _sut.ProfileName = "New Profile";
        _sut.Description = "Desc";
        _sut.Author = "Author";

        await _sut.SaveCommand.ExecuteAsync(null);

        await _profileManager.Received(1).SaveLocalProfileAsync(
            Arg.Is<MigrationProfile>(p =>
                p.Name == "New Profile" &&
                p.Description == "Desc" &&
                p.Author == "Author"),
            Arg.Any<CancellationToken>());
        _navService.Received(1).GoBack();
    }

    [Fact]
    public void CanSave_IsFalse_WhenNameEmpty()
    {
        _sut.ProfileName = "";
        _sut.CanSave().Should().BeFalse();
    }

    [Fact]
    public void CanSave_IsTrue_WhenNameSet()
    {
        _sut.ProfileName = "Valid";
        _sut.CanSave().Should().BeTrue();
    }

    [Fact]
    public void Cancel_NavigatesBack()
    {
        _sut.CancelCommand.Execute(null);

        _navService.Received(1).GoBack();
    }

    [Fact]
    public async Task Save_BuildsProfileWithAllSections()
    {
        _sut.ProfileName = "Full";
        _sut.UserProfilesEnabled = false;
        _sut.ApplicationsEnabled = false;
        _sut.BrowserDataEnabled = false;
        _sut.SystemSettingsEnabled = false;
        _sut.WifiProfiles = false;
        _sut.Printers = false;
        _sut.Compression = false;

        await _sut.SaveCommand.ExecuteAsync(null);

        await _profileManager.Received(1).SaveLocalProfileAsync(
            Arg.Is<MigrationProfile>(p =>
                !p.Items.UserProfiles.Enabled &&
                !p.Items.Applications.Enabled &&
                !p.Items.BrowserData.Enabled &&
                !p.Items.SystemSettings.Enabled &&
                !p.Transport.Compression),
            Arg.Any<CancellationToken>());
    }
}
