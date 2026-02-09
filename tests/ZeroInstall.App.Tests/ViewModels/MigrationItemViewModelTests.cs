using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.ViewModels;

public class MigrationItemViewModelTests
{
    [Fact]
    public void Constructor_MapsPropertiesFromModel()
    {
        var model = new MigrationItem
        {
            DisplayName = "Firefox",
            Description = "Web browser",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            EstimatedSizeBytes = 104_857_600, // 100 MB
            IsSelected = true
        };

        var sut = new MigrationItemViewModel(model);

        sut.DisplayName.Should().Be("Firefox");
        sut.Description.Should().Be("Web browser");
        sut.ItemType.Should().Be(MigrationItemType.Application);
        sut.RecommendedTier.Should().Be(MigrationTier.Package);
        sut.EstimatedSizeBytes.Should().Be(104_857_600);
        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_SyncsBackToModel()
    {
        var model = new MigrationItem { IsSelected = true };
        var sut = new MigrationItemViewModel(model);

        sut.IsSelected = false;

        model.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_InvokesCallback()
    {
        var callbackCalled = false;
        var model = new MigrationItem { IsSelected = false };
        var sut = new MigrationItemViewModel(model, () => callbackCalled = true);

        sut.IsSelected = true;

        callbackCalled.Should().BeTrue();
    }

    [Fact]
    public void EstimatedSizeFormatted_FormatsCorrectly()
    {
        var model = new MigrationItem { EstimatedSizeBytes = 1_073_741_824 }; // 1 GB
        var sut = new MigrationItemViewModel(model);

        sut.EstimatedSizeFormatted.Should().Be("1.0 GB");
    }

    [Fact]
    public void EstimatedSizeFormatted_ZeroBytes()
    {
        var model = new MigrationItem { EstimatedSizeBytes = 0 };
        var sut = new MigrationItemViewModel(model);

        sut.EstimatedSizeFormatted.Should().Be("0 B");
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var model = new MigrationItem { IsSelected = false };
        var sut = new MigrationItemViewModel(model);
        var propertyChanged = false;
        sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MigrationItemViewModel.IsSelected))
                propertyChanged = true;
        };

        sut.IsSelected = true;

        propertyChanged.Should().BeTrue();
    }
}
