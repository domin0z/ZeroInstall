using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.App.Models;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.Services;

public class JsonAppSettingsTests
{
    private readonly string _tempDir;
    private readonly JsonAppSettings _sut;

    public JsonAppSettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new JsonAppSettings(_tempDir, NullLogger<JsonAppSettings>.Instance);
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        await _sut.LoadAsync();

        _sut.Current.Should().NotBeNull();
        _sut.Current.NasPath.Should().BeNull();
        _sut.Current.DefaultTransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.Current.DefaultLogLevel.Should().Be("Information");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsAllProperties()
    {
        var settings = new AppSettings
        {
            NasPath = @"\\nas\share",
            DefaultTransportMethod = TransportMethod.NetworkShare,
            DefaultLogLevel = "Warning"
        };

        await _sut.SaveAsync(settings);

        // Create a new instance to force re-read from disk
        var sut2 = new JsonAppSettings(_tempDir, NullLogger<JsonAppSettings>.Instance);
        await sut2.LoadAsync();

        sut2.Current.NasPath.Should().Be(@"\\nas\share");
        sut2.Current.DefaultTransportMethod.Should().Be(TransportMethod.NetworkShare);
        sut2.Current.DefaultLogLevel.Should().Be("Warning");
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var nestedDir = Path.Combine(_tempDir, "nested", "config");
        var sut = new JsonAppSettings(nestedDir, NullLogger<JsonAppSettings>.Instance);

        await sut.SaveAsync(new AppSettings { NasPath = @"\\test" });

        File.Exists(Path.Combine(nestedDir, "settings.json")).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithCorruptJson_ReturnsDefaults()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(filePath, "not valid json {{{");

        await _sut.LoadAsync();

        _sut.Current.Should().NotBeNull();
        _sut.Current.DefaultLogLevel.Should().Be("Information");
    }

    [Fact]
    public async Task SaveAsync_UpdatesCurrentProperty()
    {
        var settings = new AppSettings { NasPath = @"\\updated" };

        await _sut.SaveAsync(settings);

        _sut.Current.NasPath.Should().Be(@"\\updated");
    }

    [Fact]
    public async Task LoadAsync_WithPartialJson_FillsDefaults()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(filePath, """{ "NasPath": "\\\\partial" }""");

        await _sut.LoadAsync();

        _sut.Current.NasPath.Should().Be(@"\\partial");
        _sut.Current.DefaultTransportMethod.Should().Be(TransportMethod.ExternalStorage);
    }

    [Fact]
    public async Task SaveAsync_ProducesValidJson()
    {
        var settings = new AppSettings
        {
            NasPath = @"\\nas\test",
            DefaultTransportMethod = TransportMethod.DirectWiFi,
            DefaultLogLevel = "Error"
        };

        await _sut.SaveAsync(settings);

        var filePath = Path.Combine(_tempDir, "settings.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        deserialized.Should().NotBeNull();
        deserialized!.NasPath.Should().Be(@"\\nas\test");
        deserialized.DefaultTransportMethod.Should().Be(TransportMethod.DirectWiFi);
    }
}
