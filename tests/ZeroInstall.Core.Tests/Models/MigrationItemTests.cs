using System.Text.Json;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class MigrationItemTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void NewItem_HasDefaults()
    {
        var item = new MigrationItem();

        item.Id.Should().NotBeNullOrEmpty();
        item.IsSelected.Should().BeTrue();
        item.Status.Should().Be(MigrationItemStatus.Queued);
    }

    [Fact]
    public void EffectiveTier_WhenNoOverride_ReturnsRecommended()
    {
        var item = new MigrationItem
        {
            RecommendedTier = MigrationTier.Package,
            OverrideTier = null
        };

        item.EffectiveTier.Should().Be(MigrationTier.Package);
    }

    [Fact]
    public void EffectiveTier_WhenOverrideSet_ReturnsOverride()
    {
        var item = new MigrationItem
        {
            RecommendedTier = MigrationTier.Package,
            OverrideTier = MigrationTier.RegistryFile
        };

        item.EffectiveTier.Should().Be(MigrationTier.RegistryFile);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesEnumsAsStrings()
    {
        var item = new MigrationItem
        {
            DisplayName = "Microsoft Office",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            Status = MigrationItemStatus.Completed,
            EstimatedSizeBytes = 2_000_000_000
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);

        // Enums should be serialized as strings
        json.Should().Contain("\"Application\"");
        json.Should().Contain("\"Package\"");
        json.Should().Contain("\"Completed\"");

        var deserialized = JsonSerializer.Deserialize<MigrationItem>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.ItemType.Should().Be(MigrationItemType.Application);
        deserialized.RecommendedTier.Should().Be(MigrationTier.Package);
    }

    [Fact]
    public void Serialization_SourceData_IsNotSerialized()
    {
        var item = new MigrationItem
        {
            DisplayName = "Test App",
            SourceData = new DiscoveredApplication { Name = "Test" }
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);
        json.Should().NotContain("sourceData");
    }
}
