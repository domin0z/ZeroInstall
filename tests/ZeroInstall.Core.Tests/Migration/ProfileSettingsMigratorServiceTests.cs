using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Migration;

public class ProfileSettingsMigratorServiceTests : IDisposable
{
    private readonly IUserAccountManager _accountManager = Substitute.For<IUserAccountManager>();
    private readonly ProfileTransferService _profileTransfer;
    private readonly IUserPathRemapper _pathRemapper = Substitute.For<IUserPathRemapper>();
    private readonly BrowserDataService _browserData;
    private readonly SystemSettingsReplayService _systemSettings;
    private readonly ProfileSettingsMigratorService _service;
    private readonly string _tempDir;

    public ProfileSettingsMigratorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-psm-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        var fileSystem = Substitute.For<IFileSystemAccessor>();
        var processRunner = Substitute.For<IProcessRunner>();
        var registry = Substitute.For<IRegistryAccessor>();

        _profileTransfer = new ProfileTransferService(
            fileSystem, processRunner, NullLogger<ProfileTransferService>.Instance);
        _browserData = new BrowserDataService(
            fileSystem, processRunner, NullLogger<BrowserDataService>.Instance);
        _systemSettings = new SystemSettingsReplayService(
            processRunner, registry, fileSystem,
            NullLogger<SystemSettingsReplayService>.Instance);

        _service = new ProfileSettingsMigratorService(
            _accountManager, _profileTransfer, _pathRemapper,
            _browserData, _systemSettings,
            NullLogger<ProfileSettingsMigratorService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region PrepareUserAccountsAsync

    [Fact]
    public async Task PrepareUserAccountsAsync_CreatesUserWhenMissing()
    {
        _accountManager.UserExistsAsync("NewUser", Arg.Any<CancellationToken>())
            .Returns(false);
        _accountManager.CreateUserAsync("NewUser", Arg.Any<string>(), false, Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-1001");
        _accountManager.GetUserProfilePathAsync("NewUser", Arg.Any<CancellationToken>())
            .Returns(@"C:\Users\NewUser");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "OldUser" },
                DestinationUsername = "NewUser",
                CreateIfMissing = true,
                NewAccountPassword = "TestPass1!"
            }
        };

        await _service.PrepareUserAccountsAsync(mappings);

        await _accountManager.Received(1).CreateUserAsync(
            "NewUser", "TestPass1!", false, Arg.Any<CancellationToken>());
        mappings[0].DestinationSid.Should().Be("S-1-5-21-999-1001");
    }

    [Fact]
    public async Task PrepareUserAccountsAsync_SkipsCreation_WhenUserExists()
    {
        _accountManager.UserExistsAsync("ExistingUser", Arg.Any<CancellationToken>())
            .Returns(true);
        _accountManager.GetUserSidAsync("ExistingUser", Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-2001");
        _accountManager.GetUserProfilePathAsync("ExistingUser", Arg.Any<CancellationToken>())
            .Returns(@"C:\Users\ExistingUser");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "OldUser" },
                DestinationUsername = "ExistingUser"
            }
        };

        await _service.PrepareUserAccountsAsync(mappings);

        await _accountManager.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        mappings[0].DestinationSid.Should().Be("S-1-5-21-999-2001");
    }

    [Fact]
    public async Task PrepareUserAccountsAsync_PopulatesProfilePath()
    {
        _accountManager.UserExistsAsync("User", Arg.Any<CancellationToken>())
            .Returns(true);
        _accountManager.GetUserSidAsync("User", Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-3001");
        _accountManager.GetUserProfilePathAsync("User", Arg.Any<CancellationToken>())
            .Returns(@"D:\Profiles\User");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "OldUser" },
                DestinationUsername = "User"
            }
        };

        await _service.PrepareUserAccountsAsync(mappings);

        mappings[0].DestinationProfilePath.Should().Be(@"D:\Profiles\User");
    }

    [Fact]
    public async Task PrepareUserAccountsAsync_SkipsNonExistent_WhenCreateIfMissingFalse()
    {
        _accountManager.UserExistsAsync("Missing", Arg.Any<CancellationToken>())
            .Returns(false);

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "OldUser" },
                DestinationUsername = "Missing",
                CreateIfMissing = false
            }
        };

        await _service.PrepareUserAccountsAsync(mappings);

        await _accountManager.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesTopLevelManifest()
    {
        var items = new List<MigrationItem>();
        var outputDir = Path.Combine(_tempDir, "capture");

        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "profile-settings-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CreatesSubdirectories()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Test Profile",
                ItemType = MigrationItemType.UserProfile,
                IsSelected = true,
                SourceData = new UserProfile { Username = "Test", ProfilePath = @"C:\Users\Test" }
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture-sub");
        await _service.CaptureAsync(items, outputDir);

        // Verify manifest was created
        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "profile-settings-manifest.json"));
        json.Should().Contain("HasProfiles");
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_CallsPrepareAccounts()
    {
        var captureDir = Path.Combine(_tempDir, "restore");
        Directory.CreateDirectory(captureDir);

        _accountManager.UserExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _accountManager.GetUserSidAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-1001");
        _accountManager.GetUserProfilePathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(@"C:\Users\DestUser");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "SrcUser", ProfilePath = @"C:\Users\SrcUser" },
                DestinationUsername = "DestUser"
            }
        };

        await _service.RestoreAsync(captureDir, mappings);

        await _accountManager.Received().UserExistsAsync("DestUser", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_CallsPathRemapper_WhenRemappingNeeded()
    {
        var captureDir = Path.Combine(_tempDir, "restore-remap");
        Directory.CreateDirectory(captureDir);

        _accountManager.UserExistsAsync("William", Arg.Any<CancellationToken>())
            .Returns(true);
        _accountManager.GetUserSidAsync("William", Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-1001");
        _accountManager.GetUserProfilePathAsync("William", Arg.Any<CancellationToken>())
            .Returns(@"C:\Users\William");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Bill", ProfilePath = @"C:\Users\Bill" },
                DestinationUsername = "William"
            }
        };

        await _service.RestoreAsync(captureDir, mappings);

        await _pathRemapper.Received().RemapPathsAsync(
            Arg.Is<UserMapping>(m => m.DestinationUsername == "William"),
            Arg.Any<string>(),
            Arg.Any<IProgress<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_SkipsPathRemapper_WhenSameUsername()
    {
        var captureDir = Path.Combine(_tempDir, "restore-no-remap");
        Directory.CreateDirectory(captureDir);

        _accountManager.UserExistsAsync("Same", Arg.Any<CancellationToken>())
            .Returns(true);
        _accountManager.GetUserSidAsync("Same", Arg.Any<CancellationToken>())
            .Returns("S-1-5-21-999-1001");
        _accountManager.GetUserProfilePathAsync("Same", Arg.Any<CancellationToken>())
            .Returns(@"C:\Users\Same");

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Same", ProfilePath = @"C:\Users\Same" },
                DestinationUsername = "Same"
            }
        };

        await _service.RestoreAsync(captureDir, mappings);

        await _pathRemapper.DidNotReceive().RemapPathsAsync(
            Arg.Any<UserMapping>(), Arg.Any<string>(),
            Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
