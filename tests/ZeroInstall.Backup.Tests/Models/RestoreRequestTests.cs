using System.Text.Json;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests.Models;

public class RestoreRequestTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var request = new RestoreRequest();

        request.CustomerId.Should().BeEmpty();
        request.Scope.Should().Be(RestoreScope.Full);
        request.Message.Should().BeNull();
        request.SpecificPaths.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var request = new RestoreRequest
        {
            CustomerId = "cust-001",
            MachineName = "DESKTOP-ABC",
            Scope = RestoreScope.Partial,
            Message = "I accidentally deleted my Documents folder",
            SpecificPaths = { @"C:\Users\Customer\Documents" }
        };

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<RestoreRequest>(json)!;

        deserialized.CustomerId.Should().Be("cust-001");
        deserialized.Scope.Should().Be(RestoreScope.Partial);
        deserialized.Message.Should().Contain("Documents");
        deserialized.SpecificPaths.Should().HaveCount(1);
    }
}
