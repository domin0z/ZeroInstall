using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class DiscoveredApplicationTests
{
    [Fact]
    public void RecommendedTier_WithWingetId_ReturnsPackage()
    {
        var app = new DiscoveredApplication
        {
            Name = "Google Chrome",
            WingetPackageId = "Google.Chrome"
        };

        app.RecommendedTier.Should().Be(MigrationTier.Package);
    }

    [Fact]
    public void RecommendedTier_WithChocolateyId_ReturnsPackage()
    {
        var app = new DiscoveredApplication
        {
            Name = "7-Zip",
            ChocolateyPackageId = "7zip"
        };

        app.RecommendedTier.Should().Be(MigrationTier.Package);
    }

    [Fact]
    public void RecommendedTier_WithNoPackageId_ReturnsRegistryFile()
    {
        var app = new DiscoveredApplication
        {
            Name = "Custom Internal Tool",
            WingetPackageId = null,
            ChocolateyPackageId = null
        };

        app.RecommendedTier.Should().Be(MigrationTier.RegistryFile);
    }

    [Fact]
    public void RecommendedTier_WithBothPackageIds_ReturnsPackage()
    {
        var app = new DiscoveredApplication
        {
            Name = "Firefox",
            WingetPackageId = "Mozilla.Firefox",
            ChocolateyPackageId = "firefox"
        };

        app.RecommendedTier.Should().Be(MigrationTier.Package);
    }
}
