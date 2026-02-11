using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
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

    [Fact]
    public void DomainWarning_WhenSet_ShowsDomainWarning()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill", AccountType = UserAccountType.ActiveDirectory },
            DomainMigrationWarning = "Account is domain-joined, requires SID reassignment"
        };

        var vm = new UserMappingEntryViewModel(mapping);

        vm.DomainWarning.Should().Contain("SID reassignment");
        vm.ShowDomainWarning.Should().BeTrue();
    }

    [Fact]
    public void DomainWarning_WhenNull_HidesDomainWarning()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill", AccountType = UserAccountType.Local }
        };

        var vm = new UserMappingEntryViewModel(mapping);

        vm.DomainWarning.Should().BeNull();
        vm.ShowDomainWarning.Should().BeFalse();
    }

    [Fact]
    public void PostMigrationAction_UpdatesModel()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill" },
            PostMigrationAction = PostMigrationAccountAction.None
        };

        var vm = new UserMappingEntryViewModel(mapping);
        vm.PostMigrationAction = PostMigrationAccountAction.Disable;

        mapping.PostMigrationAction.Should().Be(PostMigrationAccountAction.Disable);
    }

    [Fact]
    public void ReassignInPlace_UpdatesModel()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill" },
            ReassignInPlace = false
        };

        var vm = new UserMappingEntryViewModel(mapping);
        vm.ReassignInPlace = true;

        mapping.ReassignInPlace.Should().BeTrue();
    }

    [Fact]
    public void ShowDomainOptions_TrueForDomainAccount()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill", AccountType = UserAccountType.ActiveDirectory }
        };

        var vm = new UserMappingEntryViewModel(mapping);

        vm.ShowDomainOptions.Should().BeTrue();
    }

    [Fact]
    public void ShowDomainOptions_FalseForLocalAccount()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Bill", AccountType = UserAccountType.Local }
        };

        var vm = new UserMappingEntryViewModel(mapping);

        vm.ShowDomainOptions.Should().BeFalse();
    }
}
