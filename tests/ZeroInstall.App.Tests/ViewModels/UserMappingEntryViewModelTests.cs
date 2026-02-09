using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.ViewModels;

public class UserMappingEntryViewModelTests
{
    [Fact]
    public void Constructor_MapsFromModel()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill", ProfilePath = @"C:\Users\Bill" },
            DestinationUsername = "William",
            CreateIfMissing = true
        };

        var vm = new UserMappingEntryViewModel(mapping);

        vm.SourceUsername.Should().Be("Bill");
        vm.SourceProfilePath.Should().Be(@"C:\Users\Bill");
        vm.DestinationUsername.Should().Be("William");
        vm.CreateIfMissing.Should().BeTrue();
    }

    [Fact]
    public void SettingDestinationUsername_UpdatesModel()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill" },
            DestinationUsername = "Bill"
        };

        var vm = new UserMappingEntryViewModel(mapping);
        vm.DestinationUsername = "NewUser";

        mapping.DestinationUsername.Should().Be("NewUser");
        mapping.DestinationProfilePath.Should().Be(@"C:\Users\NewUser");
    }

    [Fact]
    public void SettingCreateIfMissing_UpdatesModel()
    {
        var mapping = new UserMapping { CreateIfMissing = false };
        var vm = new UserMappingEntryViewModel(mapping);

        vm.CreateIfMissing = true;

        mapping.CreateIfMissing.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_Fires_WhenDestinationUsernameChanges()
    {
        var mapping = new UserMapping { DestinationUsername = "Old" };
        var vm = new UserMappingEntryViewModel(mapping);
        var propertyChanged = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UserMappingEntryViewModel.DestinationUsername))
                propertyChanged = true;
        };

        vm.DestinationUsername = "New";

        propertyChanged.Should().BeTrue();
    }
}
