using System.Text.Json;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class MigrationProfileTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void NewProfile_HasSensibleDefaults()
    {
        var profile = new MigrationProfile();

        profile.Items.UserProfiles.Enabled.Should().BeTrue();
        profile.Items.Applications.Enabled.Should().BeTrue();
        profile.Items.Applications.PreferredTier.Should().Be(MigrationTier.Package);
        profile.Items.BrowserData.Enabled.Should().BeTrue();
        profile.Items.BrowserData.IncludePasswords.Should().BeFalse();
        profile.Items.SystemSettings.WifiProfiles.Should().BeTrue();
        profile.Items.SystemSettings.Printers.Should().BeTrue();
        profile.Items.SystemSettings.Credentials.Should().BeFalse();
        profile.Transport.PreferredMethod.Should().Be(TransportMethod.NetworkShare);
        profile.Transport.Compression.Should().BeTrue();
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesProfile()
    {
        var profile = new MigrationProfile
        {
            Name = "Developer Workstation",
            Description = "Full dev environment migration",
            Author = "TestTech",
            Items = new ProfileItemSelection
            {
                Applications = new ProfileApplicationSettings
                {
                    Enabled = true,
                    PreferredTier = MigrationTier.Package,
                    Include = ["Visual Studio*", "JetBrains*", "Git*"],
                    Exclude = ["Microsoft OneDrive"]
                },
                SystemSettings = new ProfileSystemSettings
                {
                    EnvironmentVariables = true,
                    Certificates = true
                }
            }
        };

        var json = JsonSerializer.Serialize(profile, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MigrationProfile>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Developer Workstation");
        deserialized.Items.Applications.Include.Should().Contain("Visual Studio*");
        deserialized.Items.Applications.Exclude.Should().Contain("Microsoft OneDrive");
        deserialized.Items.SystemSettings.EnvironmentVariables.Should().BeTrue();
        deserialized.Items.SystemSettings.Certificates.Should().BeTrue();
    }
}
