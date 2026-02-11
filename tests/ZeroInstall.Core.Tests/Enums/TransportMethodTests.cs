using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Tests.Enums;

public class TransportMethodTests
{
    [Fact]
    public void Bluetooth_ExistsInEnum()
    {
        var method = TransportMethod.Bluetooth;
        method.Should().BeDefined();
    }

    [Fact]
    public void Bluetooth_ToStringReturnsBluetooth()
    {
        TransportMethod.Bluetooth.ToString().Should().Be("Bluetooth");
    }
}
