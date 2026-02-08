using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class EmailDataServiceTests : IDisposable
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly EmailDataService _service;
    private readonly string _tempDir;

    public EmailDataServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-email-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new EmailDataService(
            _fileSystem, _processRunner, NullLogger<EmailDataService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UserProfile CreateProfileWithOutlook(string username, params string[] dataPaths)
    {
        return new UserProfile
        {
            Username = username,
            ProfilePath = $@"C:\Users\{username}",
            EmailData =
            [
                new EmailClientData
                {
                    ClientName = "Outlook",
                    DataPaths = dataPaths.ToList()
                }
            ]
        };
    }

    private static UserProfile CreateProfileWithThunderbird(string username, params string[] dataPaths)
    {
        return new UserProfile
        {
            Username = username,
            ProfilePath = $@"C:\Users\{username}",
            EmailData =
            [
                new EmailClientData
                {
                    ClientName = "Thunderbird",
                    DataPaths = dataPaths.ToList()
                }
            ]
        };
    }

    private MigrationItem CreateEmailItem(UserProfile profile, string displayName)
    {
        return new MigrationItem
        {
            DisplayName = displayName,
            ItemType = MigrationItemType.EmailData,
            IsSelected = true,
            SourceData = profile
        };
    }

    #region CaptureAsync — General

    [Fact]
    public async Task CaptureAsync_CreatesManifest()
    {
        var pstPath = Path.Combine(_tempDir, "test.pst");
        File.WriteAllText(pstPath, "fake-pst");
        _fileSystem.FileExists(pstPath).Returns(true);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var profile = CreateProfileWithOutlook("Alice", pstPath);
        var items = new List<MigrationItem> { CreateEmailItem(profile, "Alice - Outlook") };

        var outputDir = Path.Combine(_tempDir, "capture");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "email-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonEmailItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "SomeApp",
                ItemType = MigrationItemType.Application,
                IsSelected = true
            }
        };

        var outputDir = Path.Combine(_tempDir, "skip-test");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "email-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SkipsDeselectedItems()
    {
        var profile = CreateProfileWithOutlook("Alice");
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice - Outlook",
                ItemType = MigrationItemType.EmailData,
                IsSelected = false,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "deselected-test");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "email-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatus()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var profile = CreateProfileWithOutlook("Alice");
        var item = CreateEmailItem(profile, "Alice - Outlook");

        await _service.CaptureAsync(new[] { item }, Path.Combine(_tempDir, "status-out"));

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    #endregion

    #region CaptureAsync — Outlook

    [Fact]
    public async Task CaptureOutlook_CopiesPstFiles()
    {
        var pstPath = Path.Combine(_tempDir, "mail.pst");
        File.WriteAllText(pstPath, "pst-data");
        _fileSystem.FileExists(pstPath).Returns(true);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData
        {
            ClientName = "Outlook",
            DataPaths = [pstPath]
        };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = $@"C:\Users\Alice"
        };

        var outputDir = Path.Combine(_tempDir, "outlook-pst");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Components.Should().Contain(c => c.ComponentType == EmailComponentType.PST);
        File.Exists(Path.Combine(outputDir, "Alice_Outlook", "PST", "mail.pst")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureOutlook_CopiesOstFiles()
    {
        var ostPath = Path.Combine(_tempDir, "exchange.ost");
        File.WriteAllText(ostPath, "ost-data");
        _fileSystem.FileExists(ostPath).Returns(true);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData
        {
            ClientName = "Outlook",
            DataPaths = [ostPath]
        };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = $@"C:\Users\Alice"
        };

        var outputDir = Path.Combine(_tempDir, "outlook-ost");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        var ostComponent = entry!.Components.First(c => c.ComponentType == EmailComponentType.OST);
        ostComponent.Note.Should().Contain("Exchange cache");
    }

    [Fact]
    public async Task CaptureOutlook_CopiesSignatures()
    {
        var sigDir = Path.Combine(_tempDir, "AppData", "Roaming", "Microsoft", "Signatures");
        Directory.CreateDirectory(sigDir);
        File.WriteAllText(Path.Combine(sigDir, "mysig.htm"), "<html>sig</html>");
        _fileSystem.DirectoryExists(sigDir).Returns(true);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData { ClientName = "Outlook", DataPaths = [] };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = _tempDir
        };

        var outputDir = Path.Combine(_tempDir, "outlook-sig-output");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Components.Should().Contain(c => c.ComponentType == EmailComponentType.Signatures);
    }

    [Fact]
    public async Task CaptureOutlook_CopiesAutocomplete()
    {
        var roamDir = Path.Combine(_tempDir, "AppData", "Local", "Microsoft", "Outlook", "RoamCache");
        Directory.CreateDirectory(roamDir);
        File.WriteAllText(Path.Combine(roamDir, "Stream_Autocomplete.dat"), "data");
        _fileSystem.DirectoryExists(roamDir).Returns(true);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData { ClientName = "Outlook", DataPaths = [] };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = _tempDir
        };

        var outputDir = Path.Combine(_tempDir, "outlook-ac-output");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Components.Should().Contain(c => c.ComponentType == EmailComponentType.Autocomplete);
    }

    [Fact]
    public async Task CaptureOutlook_CopiesTemplates()
    {
        var templatesDir = Path.Combine(_tempDir, "AppData", "Roaming", "Microsoft", "Templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(Path.Combine(templatesDir, "reply.oft"), "template");
        File.WriteAllText(Path.Combine(templatesDir, "Normal.dotm"), "not-a-template");
        _fileSystem.DirectoryExists(templatesDir).Returns(true);
        _fileSystem.GetFiles(templatesDir, "*.oft").Returns([Path.Combine(templatesDir, "reply.oft")]);
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData { ClientName = "Outlook", DataPaths = [] };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = _tempDir
        };

        var outputDir = Path.Combine(_tempDir, "outlook-tpl-output");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Components.Should().Contain(c => c.ComponentType == EmailComponentType.Templates);
        File.Exists(Path.Combine(outputDir, "Alice_Outlook", "Templates", "reply.oft")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureOutlook_ExportsRegistry()
    {
        _processRunner.RunAsync("reg", Arg.Is<string>(a => a.Contains("16.0")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });
        _processRunner.RunAsync("reg", Arg.Is<string>(a => a.Contains("15.0")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });
        _processRunner.RunAsync("reg", Arg.Is<string>(a => a.Contains("14.0")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var emailClient = new EmailClientData { ClientName = "Outlook", DataPaths = [] };
        var profile = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" };

        var outputDir = Path.Combine(_tempDir, "outlook-reg-output");
        var entry = await _service.CaptureOutlookAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.Components.Should().Contain(c =>
            c.ComponentType == EmailComponentType.Registry &&
            c.Note!.Contains("16.0"));
    }

    #endregion

    #region CaptureAsync — Thunderbird

    [Fact]
    public async Task CaptureThunderbird_CopiesProfileDirectories()
    {
        var tbProfileDir = Path.Combine(_tempDir, "abc123.default-release");
        Directory.CreateDirectory(tbProfileDir);
        File.WriteAllText(Path.Combine(tbProfileDir, "prefs.js"), "user_pref('test', true);");
        File.WriteAllText(Path.Combine(tbProfileDir, "key4.db"), "key-data");
        File.WriteAllText(Path.Combine(tbProfileDir, "logins.json"), "{}");
        _fileSystem.DirectoryExists(tbProfileDir).Returns(true);

        var emailClient = new EmailClientData
        {
            ClientName = "Thunderbird",
            DataPaths = [tbProfileDir]
        };
        var profile = new UserProfile
        {
            Username = "Alice",
            ProfilePath = @"C:\Users\Alice"
        };

        var outputDir = Path.Combine(_tempDir, "tb-capture");
        var entry = await _service.CaptureThunderbirdAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.ClientName.Should().Be("Thunderbird");
        entry.Components.Should().Contain(c => c.ComponentType == EmailComponentType.Profile);

        var capturedProfile = Path.Combine(outputDir, "Alice_Thunderbird", "Profiles", "abc123.default-release");
        File.Exists(Path.Combine(capturedProfile, "prefs.js")).Should().BeTrue();
        File.Exists(Path.Combine(capturedProfile, "key4.db")).Should().BeTrue();
        File.Exists(Path.Combine(capturedProfile, "logins.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureThunderbird_ExcludesCacheDirectories()
    {
        var tbProfileDir = Path.Combine(_tempDir, "default-profile");
        Directory.CreateDirectory(tbProfileDir);
        Directory.CreateDirectory(Path.Combine(tbProfileDir, "cache2"));
        Directory.CreateDirectory(Path.Combine(tbProfileDir, "startupCache"));
        Directory.CreateDirectory(Path.Combine(tbProfileDir, "extensions"));
        File.WriteAllText(Path.Combine(tbProfileDir, "cache2", "data.tmp"), "cached");
        File.WriteAllText(Path.Combine(tbProfileDir, "startupCache", "cache.bin"), "startup");
        File.WriteAllText(Path.Combine(tbProfileDir, "extensions", "ext.json"), "{}");
        File.WriteAllText(Path.Combine(tbProfileDir, "prefs.js"), "prefs");
        _fileSystem.DirectoryExists(tbProfileDir).Returns(true);

        var emailClient = new EmailClientData
        {
            ClientName = "Thunderbird",
            DataPaths = [tbProfileDir]
        };
        var profile = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" };

        var outputDir = Path.Combine(_tempDir, "tb-cache-exclude");
        await _service.CaptureThunderbirdAsync(emailClient, profile, outputDir, CancellationToken.None);

        var capturedDir = Path.Combine(outputDir, "Alice_Thunderbird", "Profiles", "default-profile");
        Directory.Exists(Path.Combine(capturedDir, "cache2")).Should().BeFalse();
        Directory.Exists(Path.Combine(capturedDir, "startupCache")).Should().BeFalse();
        Directory.Exists(Path.Combine(capturedDir, "extensions")).Should().BeTrue();
        File.Exists(Path.Combine(capturedDir, "prefs.js")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureThunderbird_CopiesProfilesIni()
    {
        var profilesIniPath = Path.Combine(_tempDir, "AppData", "Roaming", "Thunderbird", "profiles.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(profilesIniPath)!);
        File.WriteAllText(profilesIniPath, "[Profile0]\nName=default-release\nIsRelative=1\nPath=Profiles/abc.default-release");
        _fileSystem.FileExists(profilesIniPath).Returns(true);

        var emailClient = new EmailClientData
        {
            ClientName = "Thunderbird",
            DataPaths = []
        };
        var profile = new UserProfile { Username = "Alice", ProfilePath = _tempDir };

        var outputDir = Path.Combine(_tempDir, "tb-ini-capture");
        var entry = await _service.CaptureThunderbirdAsync(emailClient, profile, outputDir, CancellationToken.None);

        entry.Should().NotBeNull();
        File.Exists(Path.Combine(outputDir, "Alice_Thunderbird", "profiles.ini")).Should().BeTrue();
    }

    #endregion

    #region RestoreAsync — General

    [Fact]
    public async Task RestoreAsync_HandlesNoManifest()
    {
        var captureDir = Path.Combine(_tempDir, "no-manifest");
        Directory.CreateDirectory(captureDir);

        await _service.RestoreAsync(captureDir, new List<UserMapping>());
        // Should not throw
    }

    [Fact]
    public async Task RestoreAsync_SkipsUnmappedUsers()
    {
        var captureDir = Path.Combine(_tempDir, "unmapped");
        Directory.CreateDirectory(captureDir);

        var manifest = new EmailCaptureManifest
        {
            Entries =
            [
                new CapturedEmailEntry
                {
                    ClientName = "Outlook",
                    SourceUsername = "UnknownUser",
                    CaptureSubDir = "UnknownUser_Outlook"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "email-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "DifferentUser" },
                DestinationUsername = "Bob"
            }
        };

        await _service.RestoreAsync(captureDir, mappings);
        // Should not throw — just logs warning and skips
    }

    #endregion

    #region RestoreAsync — Outlook

    [Fact]
    public async Task RestoreOutlook_CopiesPstToDestination()
    {
        var capturedDir = Path.Combine(_tempDir, "outlook-restore-src", "Alice_Outlook");
        var pstDir = Path.Combine(capturedDir, "PST");
        Directory.CreateDirectory(pstDir);
        File.WriteAllText(Path.Combine(pstDir, "mail.pst"), "pst-data");

        var destProfile = Path.Combine(_tempDir, "dest-profile");
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
            DestinationUsername = "Bob",
            DestinationProfilePath = destProfile
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Outlook",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Outlook"
        };

        await _service.RestoreOutlookAsync(
            entry, Path.Combine(_tempDir, "outlook-restore-src"), mapping, CancellationToken.None);

        var expectedPst = Path.Combine(destProfile, "Documents", "Outlook Files", "mail.pst");
        File.Exists(expectedPst).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreOutlook_CopiesSignatures()
    {
        var capturedDir = Path.Combine(_tempDir, "outlook-sig-restore", "Alice_Outlook");
        var sigDir = Path.Combine(capturedDir, "Signatures");
        Directory.CreateDirectory(sigDir);
        File.WriteAllText(Path.Combine(sigDir, "mysig.htm"), "<html>sig</html>");

        var destProfile = Path.Combine(_tempDir, "dest-sig");
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
            DestinationUsername = "Bob",
            DestinationProfilePath = destProfile
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Outlook",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Outlook"
        };

        await _service.RestoreOutlookAsync(
            entry, Path.Combine(_tempDir, "outlook-sig-restore"), mapping, CancellationToken.None);

        var expectedSig = Path.Combine(destProfile, "AppData", "Roaming", "Microsoft", "Signatures", "mysig.htm");
        File.Exists(expectedSig).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreOutlook_ImportsRegistryWithPathRemapping()
    {
        var capturedDir = Path.Combine(_tempDir, "outlook-reg-restore", "Alice_Outlook");
        var regDir = Path.Combine(capturedDir, "Registry");
        Directory.CreateDirectory(regDir);

        // Write a fake .reg file with source paths (double-backslash as in real .reg files)
        var regContent = """
            Windows Registry Editor Version 5.00

            [HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Outlook]
            "DefaultProfile"="Outlook"
            "PSTPath"="C:\\Users\\Alice\\Documents\\Outlook Files\\mail.pst"
            """;
        File.WriteAllText(Path.Combine(regDir, "outlook-160.reg"), regContent);

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var destProfile = Path.Combine(_tempDir, "dest-reg");
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
            DestinationUsername = "Bob",
            DestinationProfilePath = @"C:\Users\Bob"
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Outlook",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Outlook"
        };

        await _service.RestoreOutlookAsync(
            entry, Path.Combine(_tempDir, "outlook-reg-restore"), mapping, CancellationToken.None);

        // Verify reg import was called
        await _processRunner.Received().RunAsync(
            "reg", Arg.Is<string>(a => a.StartsWith("import")), Arg.Any<CancellationToken>());

        // Verify the remapped file contains the new path
        var remappedFile = Path.Combine(regDir, "outlook-160.reg.remapped.reg");
        File.Exists(remappedFile).Should().BeTrue();
        var remapped = await File.ReadAllTextAsync(remappedFile);
        remapped.Should().Contain(@"C:\\Users\\Bob");
        remapped.Should().NotContain(@"C:\\Users\\Alice");
    }

    #endregion

    #region RestoreAsync — Thunderbird

    [Fact]
    public async Task RestoreThunderbird_CopiesProfilesToDestination()
    {
        var capturedDir = Path.Combine(_tempDir, "tb-restore-src", "Alice_Thunderbird");
        var profilesDir = Path.Combine(capturedDir, "Profiles", "abc123.default-release");
        Directory.CreateDirectory(profilesDir);
        File.WriteAllText(Path.Combine(profilesDir, "prefs.js"), "user_pref('test', true);");
        File.WriteAllText(Path.Combine(profilesDir, "key4.db"), "key-data");

        var destProfile = Path.Combine(_tempDir, "tb-dest");
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
            DestinationUsername = "Bob",
            DestinationProfilePath = destProfile
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Thunderbird",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Thunderbird"
        };

        await _service.RestoreThunderbirdAsync(
            entry, Path.Combine(_tempDir, "tb-restore-src"), mapping, CancellationToken.None);

        var expectedProfile = Path.Combine(
            destProfile, "AppData", "Roaming", "Thunderbird", "Profiles", "abc123.default-release");
        File.Exists(Path.Combine(expectedProfile, "prefs.js")).Should().BeTrue();
        File.Exists(Path.Combine(expectedProfile, "key4.db")).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreThunderbird_WritesProfilesIniWithRemapping()
    {
        var capturedDir = Path.Combine(_tempDir, "tb-ini-restore", "Alice_Thunderbird");
        Directory.CreateDirectory(capturedDir);

        var destProfile = Path.Combine(_tempDir, "tb-ini-dest");

        var iniContent = $@"[General]
StartWithLastProfile=1

[Profile0]
Name=default-release
IsRelative=0
Path=C:\Users\Alice\AppData\Roaming\Thunderbird\Profiles\abc.default-release
Default=1";
        File.WriteAllText(Path.Combine(capturedDir, "profiles.ini"), iniContent);

        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
            DestinationUsername = "Bob",
            DestinationProfilePath = destProfile
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Thunderbird",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Thunderbird"
        };

        await _service.RestoreThunderbirdAsync(
            entry, Path.Combine(_tempDir, "tb-ini-restore"), mapping, CancellationToken.None);

        var destIniPath = Path.Combine(destProfile, "AppData", "Roaming", "Thunderbird", "profiles.ini");
        File.Exists(destIniPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(destIniPath);
        content.Should().Contain(destProfile.Replace(@"\", @"\"));
        content.Should().NotContain(@"C:\Users\Alice");
    }

    [Fact]
    public async Task RestoreThunderbird_NoRemapping_WhenSameUsername()
    {
        var capturedDir = Path.Combine(_tempDir, "tb-noremap", "Alice_Thunderbird");
        Directory.CreateDirectory(capturedDir);

        var destProfile = Path.Combine(_tempDir, "tb-noremap-dest");
        var sourceProfilePath = destProfile; // same path = no remapping

        var iniContent = $@"[Profile0]
Name=default
IsRelative=0
Path={sourceProfilePath}\AppData\Roaming\Thunderbird\Profiles\default";
        File.WriteAllText(Path.Combine(capturedDir, "profiles.ini"), iniContent);

        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Alice", ProfilePath = sourceProfilePath },
            DestinationUsername = "Alice",
            DestinationProfilePath = destProfile
        };

        var entry = new CapturedEmailEntry
        {
            ClientName = "Thunderbird",
            SourceUsername = "Alice",
            CaptureSubDir = "Alice_Thunderbird"
        };

        await _service.RestoreThunderbirdAsync(
            entry, Path.Combine(_tempDir, "tb-noremap"), mapping, CancellationToken.None);

        var destIniPath = Path.Combine(destProfile, "AppData", "Roaming", "Thunderbird", "profiles.ini");
        File.Exists(destIniPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(destIniPath);
        content.Should().Contain(sourceProfilePath);
    }

    #endregion

    #region Manifest Serialization

    [Fact]
    public void Manifest_RoundTrips_Correctly()
    {
        var manifest = new EmailCaptureManifest
        {
            CapturedUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Entries =
            [
                new CapturedEmailEntry
                {
                    ClientName = "Outlook",
                    SourceUsername = "Alice",
                    CaptureSubDir = "Alice_Outlook",
                    Components =
                    [
                        new CapturedEmailComponent
                        {
                            ComponentType = EmailComponentType.PST,
                            RelativePath = "PST/mail.pst"
                        },
                        new CapturedEmailComponent
                        {
                            ComponentType = EmailComponentType.Registry,
                            RelativePath = "Registry/outlook-160.reg",
                            Note = "Outlook 16.0 registry profile"
                        }
                    ]
                }
            ]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<EmailCaptureManifest>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Entries.Should().HaveCount(1);
        deserialized.Entries[0].ClientName.Should().Be("Outlook");
        deserialized.Entries[0].Components.Should().HaveCount(2);
        deserialized.Entries[0].Components[0].ComponentType.Should().Be(EmailComponentType.PST);
        deserialized.Entries[0].Components[1].Note.Should().Contain("16.0");
    }

    #endregion
}
