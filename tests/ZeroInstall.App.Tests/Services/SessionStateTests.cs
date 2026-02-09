using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.Services;

public class SessionStateTests
{
    private readonly SessionState _sut = new();

    [Fact]
    public void DefaultValues_ShouldBeEmpty()
    {
        _sut.Role.Should().Be(MachineRole.Source);
        _sut.SelectedItems.Should().BeEmpty();
        _sut.UserMappings.Should().BeEmpty();
        _sut.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.OutputPath.Should().BeEmpty();
        _sut.InputPath.Should().BeEmpty();
        _sut.CurrentJob.Should().BeNull();
    }

    [Fact]
    public void SetProperties_ShouldRetainValues()
    {
        var item = new MigrationItem { DisplayName = "Chrome" };
        var mapping = new UserMapping { DestinationUsername = "Bill" };
        var job = new MigrationJob { SourceHostname = "PC1" };

        _sut.Role = MachineRole.Destination;
        _sut.SelectedItems = [item];
        _sut.UserMappings = [mapping];
        _sut.TransportMethod = TransportMethod.NetworkShare;
        _sut.OutputPath = @"E:\capture";
        _sut.InputPath = @"E:\restore";
        _sut.CurrentJob = job;

        _sut.Role.Should().Be(MachineRole.Destination);
        _sut.SelectedItems.Should().ContainSingle().Which.DisplayName.Should().Be("Chrome");
        _sut.UserMappings.Should().ContainSingle().Which.DestinationUsername.Should().Be("Bill");
        _sut.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        _sut.OutputPath.Should().Be(@"E:\capture");
        _sut.InputPath.Should().Be(@"E:\restore");
        _sut.CurrentJob.Should().Be(job);
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        _sut.Role = MachineRole.Destination;
        _sut.SelectedItems = [new MigrationItem()];
        _sut.UserMappings = [new UserMapping()];
        _sut.TransportMethod = TransportMethod.DirectWiFi;
        _sut.OutputPath = @"E:\capture";
        _sut.InputPath = @"E:\restore";
        _sut.CurrentJob = new MigrationJob();

        _sut.Reset();

        _sut.Role.Should().Be(MachineRole.Source);
        _sut.SelectedItems.Should().BeEmpty();
        _sut.UserMappings.Should().BeEmpty();
        _sut.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.OutputPath.Should().BeEmpty();
        _sut.InputPath.Should().BeEmpty();
        _sut.CurrentJob.Should().BeNull();
    }

    [Fact]
    public void ImplementsISessionState()
    {
        _sut.Should().BeAssignableTo<ISessionState>();
    }

    [Fact]
    public void Reset_CalledTwice_ShouldBeIdempotent()
    {
        _sut.Role = MachineRole.Destination;
        _sut.OutputPath = @"E:\test";

        _sut.Reset();
        _sut.Reset();

        _sut.Role.Should().Be(MachineRole.Source);
        _sut.OutputPath.Should().BeEmpty();
    }
}
