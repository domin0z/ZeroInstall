using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class UserMappingTests
{
    [Fact]
    public void RequiresPathRemapping_WhenPathsDiffer_ReturnsTrue()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile
            {
                Username = "Bill",
                ProfilePath = @"C:\Users\Bill"
            },
            DestinationUsername = "User",
            DestinationProfilePath = @"C:\Users\User"
        };

        mapping.RequiresPathRemapping.Should().BeTrue();
    }

    [Fact]
    public void RequiresPathRemapping_WhenPathsSame_ReturnsFalse()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile
            {
                Username = "Admin",
                ProfilePath = @"C:\Users\Admin"
            },
            DestinationUsername = "Admin",
            DestinationProfilePath = @"C:\Users\Admin"
        };

        mapping.RequiresPathRemapping.Should().BeFalse();
    }

    [Fact]
    public void RequiresPathRemapping_CaseInsensitive()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile
            {
                Username = "admin",
                ProfilePath = @"C:\Users\admin"
            },
            DestinationUsername = "Admin",
            DestinationProfilePath = @"C:\Users\Admin"
        };

        mapping.RequiresPathRemapping.Should().BeFalse();
    }

    [Fact]
    public void SourcePathPrefix_ReturnsSourceProfilePath()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { ProfilePath = @"C:\Users\OldUser" }
        };

        mapping.SourcePathPrefix.Should().Be(@"C:\Users\OldUser");
    }
}
