using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class ProfileTransferServiceTests : IDisposable
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly ProfileTransferService _service;
    private readonly string _tempDir;

    public ProfileTransferServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-ptsvc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new ProfileTransferService(
            _fileSystem, _processRunner, NullLogger<ProfileTransferService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UserProfile CreateTestProfile(string username, string? docsPath = null)
    {
        return new UserProfile
        {
            Username = username,
            Sid = "S-1-5-21-1234-5678-9012-1001",
            ProfilePath = $@"C:\Users\{username}",
            IsLocal = true,
            Folders = new UserProfileFolders
            {
                Documents = docsPath ?? $@"C:\Users\{username}\Documents",
                Desktop = $@"C:\Users\{username}\Desktop",
                Downloads = $@"C:\Users\{username}\Downloads"
            }
        };
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesManifest()
    {
        var profile = CreateTestProfile("Alice");
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice",
                ItemType = MigrationItemType.UserProfile,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "profile-transfer-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CopiesExistingFolders()
    {
        var profile = CreateTestProfile("Alice");

        // Create a real source directory with a test file
        var docsSource = Path.Combine(_tempDir, "source", "Alice", "Documents");
        Directory.CreateDirectory(docsSource);
        await File.WriteAllTextAsync(Path.Combine(docsSource, "test.txt"), "Hello");

        profile.Folders.Documents = docsSource;
        profile.Folders.Desktop = null;
        profile.Folders.Downloads = null;

        _fileSystem.DirectoryExists(docsSource).Returns(true);
        _fileSystem.GetDirectorySize(docsSource).Returns(1024L);

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice",
                ItemType = MigrationItemType.UserProfile,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "Alice", "Documents", "test.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatus()
    {
        var profile = CreateTestProfile("Alice");
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var item = new MigrationItem
        {
            DisplayName = "Alice",
            ItemType = MigrationItemType.UserProfile,
            IsSelected = true,
            SourceData = profile
        };

        await _service.CaptureAsync(new[] { item }, Path.Combine(_tempDir, "out"));

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonProfileItems()
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

        var outputDir = Path.Combine(_tempDir, "capture-skip");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "profile-transfer-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SkipsUnselectedItems()
    {
        var profile = CreateTestProfile("Skipped");

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Skipped",
                ItemType = MigrationItemType.UserProfile,
                IsSelected = false,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture-unsel");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "profile-transfer-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SkipsFoldersThatDontExist()
    {
        var profile = CreateTestProfile("Alice");
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice",
                ItemType = MigrationItemType.UserProfile,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture-nodir");
        await _service.CaptureAsync(items, outputDir);

        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "profile-transfer-manifest.json"));
        json.Should().Contain("\"Folders\": []");
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_CopiesFoldersToDestination()
    {
        // Create a capture with a Documents folder
        var captureDir = Path.Combine(_tempDir, "restore-src");
        var docsDir = Path.Combine(captureDir, "Alice", "Documents");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "resume.docx"), "My resume");

        var manifest = new ProfileTransferManifest
        {
            Profiles =
            [
                new CapturedProfileEntry
                {
                    Username = "Alice",
                    SourceProfilePath = @"C:\Users\Alice",
                    Folders = [new CapturedFolderEntry { FolderName = "Documents", OriginalPath = @"C:\Users\Alice\Documents" }]
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(captureDir, "profile-transfer-manifest.json"), manifestJson);

        var destProfile = Path.Combine(_tempDir, "restore-dest", "User");
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
                DestinationUsername = "User",
                DestinationProfilePath = destProfile,
                DestinationSid = "S-1-5-21-999-888-777-1001"
            }
        };

        _processRunner.RunAsync("icacls", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, mappings);

        File.Exists(Path.Combine(destProfile, "Documents", "resume.docx")).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_CallsIcaclsForReAcl()
    {
        var captureDir = Path.Combine(_tempDir, "restore-acl");
        Directory.CreateDirectory(Path.Combine(captureDir, "Alice", "Documents"));

        var manifest = new ProfileTransferManifest
        {
            Profiles =
            [
                new CapturedProfileEntry
                {
                    Username = "Alice",
                    Folders = [new CapturedFolderEntry { FolderName = "Documents" }]
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "profile-transfer-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var destProfile = Path.Combine(_tempDir, "dest-acl");
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Alice" },
                DestinationUsername = "Bob",
                DestinationProfilePath = destProfile,
                DestinationSid = "S-1-5-21-999-888-777-1001"
            }
        };

        _processRunner.RunAsync("icacls", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, mappings);

        await _processRunner.Received().RunAsync("icacls",
            Arg.Is<string>(s => s.Contains("Bob") && s.Contains("/grant")),
            Arg.Any<CancellationToken>());

        await _processRunner.Received().RunAsync("icacls",
            Arg.Is<string>(s => s.Contains("Bob") && s.Contains("/setowner")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_SkipsUnmappedUsers()
    {
        var captureDir = Path.Combine(_tempDir, "restore-unmapped");
        Directory.CreateDirectory(captureDir);

        var manifest = new ProfileTransferManifest
        {
            Profiles =
            [
                new CapturedProfileEntry
                {
                    Username = "UnknownUser",
                    Folders = [new CapturedFolderEntry { FolderName = "Documents" }]
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "profile-transfer-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "DifferentUser" },
                DestinationUsername = "Bob"
            }
        };

        // Should not throw
        await _service.RestoreAsync(captureDir, mappings);
    }

    [Fact]
    public async Task RestoreAsync_HandlesNoManifest()
    {
        var captureDir = Path.Combine(_tempDir, "no-manifest");
        Directory.CreateDirectory(captureDir);

        // Should not throw, just log a warning
        await _service.RestoreAsync(captureDir, new List<UserMapping>());
    }

    #endregion

    #region CopyDirectoryWithTimestamps

    [Fact]
    public void CopyDirectoryWithTimestamps_PreservesTimestamps()
    {
        var srcDir = Path.Combine(_tempDir, "ts-src");
        Directory.CreateDirectory(srcDir);
        var testFile = Path.Combine(srcDir, "file.txt");
        File.WriteAllText(testFile, "test");

        var knownTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        File.SetCreationTimeUtc(testFile, knownTime);
        File.SetLastWriteTimeUtc(testFile, knownTime);

        var destDir = Path.Combine(_tempDir, "ts-dest");
        ProfileTransferService.CopyDirectoryWithTimestamps(srcDir, destDir);

        var destFile = Path.Combine(destDir, "file.txt");
        File.Exists(destFile).Should().BeTrue();
        new FileInfo(destFile).CreationTimeUtc.Should().Be(knownTime);
        new FileInfo(destFile).LastWriteTimeUtc.Should().Be(knownTime);
    }

    [Fact]
    public void CopyDirectoryWithTimestamps_CopiesSubdirectories()
    {
        var srcDir = Path.Combine(_tempDir, "sub-src");
        var subDir = Path.Combine(srcDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var destDir = Path.Combine(_tempDir, "sub-dest");
        ProfileTransferService.CopyDirectoryWithTimestamps(srcDir, destDir);

        File.Exists(Path.Combine(destDir, "sub", "nested.txt")).Should().BeTrue();
    }

    #endregion
}
