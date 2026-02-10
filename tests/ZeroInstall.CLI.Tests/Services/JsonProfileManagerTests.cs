using Microsoft.Extensions.Logging;
using NSubstitute;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Tests.Services;

public class JsonProfileManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _localPath;
    private readonly string _nasPath;
    private readonly JsonProfileManager _manager;
    private readonly JsonProfileManager _managerWithNas;

    public JsonProfileManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-profile-test-{Guid.NewGuid():N}");
        _localPath = Path.Combine(_tempDir, "local");
        _nasPath = Path.Combine(_tempDir, "nas");
        Directory.CreateDirectory(_localPath);
        Directory.CreateDirectory(_nasPath);

        var logger = Substitute.For<ILogger<JsonProfileManager>>();
        _manager = new JsonProfileManager(_localPath, null, logger);
        _managerWithNas = new JsonProfileManager(_localPath, _nasPath, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveLocalProfileAsync_PersistsProfile()
    {
        var profile = CreateTestProfile("Test Profile");

        await _manager.SaveLocalProfileAsync(profile);

        var files = Directory.GetFiles(_localPath, "*.json");
        files.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadLocalProfileAsync_ReturnsPersistedProfile()
    {
        var profile = CreateTestProfile("My Profile");
        await _manager.SaveLocalProfileAsync(profile);

        var loaded = await _manager.LoadLocalProfileAsync("My Profile");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("My Profile");
        loaded.Author.Should().Be("TestTech");
    }

    [Fact]
    public async Task LoadLocalProfileAsync_ReturnsNullForMissing()
    {
        var result = await _manager.LoadLocalProfileAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListLocalProfilesAsync_ReturnsAllProfiles()
    {
        await _manager.SaveLocalProfileAsync(CreateTestProfile("Alpha"));
        await _manager.SaveLocalProfileAsync(CreateTestProfile("Beta"));
        await _manager.SaveLocalProfileAsync(CreateTestProfile("Charlie"));

        var profiles = await _manager.ListLocalProfilesAsync();

        profiles.Should().HaveCount(3);
        profiles[0].Name.Should().Be("Alpha");
        profiles[1].Name.Should().Be("Beta");
        profiles[2].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task ListLocalProfilesAsync_ReturnsEmptyWhenNone()
    {
        var profiles = await _manager.ListLocalProfilesAsync();

        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteLocalProfileAsync_RemovesProfile()
    {
        await _manager.SaveLocalProfileAsync(CreateTestProfile("ToDelete"));

        await _manager.DeleteLocalProfileAsync("ToDelete");

        var profiles = await _manager.ListLocalProfilesAsync();
        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteLocalProfileAsync_DoesNotThrowForMissing()
    {
        var act = async () => await _manager.DeleteLocalProfileAsync("nonexistent");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListNasProfilesAsync_ReturnsEmptyWhenNasNotConfigured()
    {
        var profiles = await _manager.ListNasProfilesAsync();

        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ListNasProfilesAsync_ReturnsNasProfiles()
    {
        // Write a profile directly to NAS path
        var profile = CreateTestProfile("NAS Profile");
        var nasManager = new JsonProfileManager(_nasPath, null,
            Substitute.For<ILogger<JsonProfileManager>>());
        await nasManager.SaveLocalProfileAsync(profile);

        var profiles = await _managerWithNas.ListNasProfilesAsync();

        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("NAS Profile");
    }

    [Fact]
    public async Task LoadNasProfileAsync_LoadsFromNas()
    {
        var profile = CreateTestProfile("NAS Config");
        var nasManager = new JsonProfileManager(_nasPath, null,
            Substitute.For<ILogger<JsonProfileManager>>());
        await nasManager.SaveLocalProfileAsync(profile);

        var loaded = await _managerWithNas.LoadNasProfileAsync("NAS Config");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("NAS Config");
    }

    [Fact]
    public async Task LoadNasProfileAsync_ReturnsNullWhenNotConfigured()
    {
        var result = await _manager.LoadNasProfileAsync("anything");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveLocalProfileAsync_ThrowsForEmptyName()
    {
        var profile = new MigrationProfile { Name = "" };

        var act = async () => await _manager.SaveLocalProfileAsync(profile);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidChars()
    {
        JsonProfileManager.SanitizeFileName("Hello World!").Should().Be("Hello-World");
        JsonProfileManager.SanitizeFileName("test/file\\name").Should().Be("test-file-name");
        JsonProfileManager.SanitizeFileName("  spaces  ").Should().Be("spaces");
        JsonProfileManager.SanitizeFileName("normal-name").Should().Be("normal-name");
    }

    [Fact]
    public async Task LoadLocalProfileAsync_FindsByNameInsideFile()
    {
        // Save a profile whose Name contains characters that get sanitized differently
        var profile = CreateTestProfile("Fancy Name!");
        await _manager.SaveLocalProfileAsync(profile);

        // The filename will be "Fancy-Name.json" but the profile name is "Fancy Name!"
        // Loading by exact name should exercise FindProfileByNameAsync fallback
        var loaded = await _manager.LoadLocalProfileAsync("Fancy Name!");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Fancy Name!");
    }

    [Fact]
    public async Task SaveLocalProfileAsync_UpdatesModifiedUtc()
    {
        var profile = CreateTestProfile("TimestampTest");
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _manager.SaveLocalProfileAsync(profile);

        profile.ModifiedUtc.Should().BeAfter(before);
        profile.ModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ListLocalProfilesAsync_SkipsMalformedJsonFiles()
    {
        await _manager.SaveLocalProfileAsync(CreateTestProfile("ValidProfile"));

        // Write a malformed JSON file directly
        await File.WriteAllTextAsync(Path.Combine(_localPath, "bad-profile.json"), "not json {{{");

        var profiles = await _manager.ListLocalProfilesAsync();

        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("ValidProfile");
    }

    [Fact]
    public async Task ListNasProfilesAsync_NonExistentDirectory_ReturnsEmpty()
    {
        var nonExistentNas = Path.Combine(_tempDir, "nonexistent-nas-path");
        var logger = Substitute.For<ILogger<JsonProfileManager>>();
        var manager = new JsonProfileManager(_localPath, nonExistentNas, logger);

        var profiles = await manager.ListNasProfilesAsync();

        profiles.Should().BeEmpty();
    }

    private static MigrationProfile CreateTestProfile(string name) => new()
    {
        Name = name,
        Description = $"Test profile: {name}",
        Author = "TestTech",
        Version = "1.0.0"
    };
}
