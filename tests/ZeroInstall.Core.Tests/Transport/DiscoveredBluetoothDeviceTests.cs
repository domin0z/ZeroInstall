using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class DiscoveredBluetoothDeviceTests
{
    [Fact]
    public void DeviceName_DefaultsToEmpty()
    {
        var device = new DiscoveredBluetoothDevice();
        device.DeviceName.Should().BeEmpty();
    }

    [Fact]
    public void Address_DefaultsToZero()
    {
        var device = new DiscoveredBluetoothDevice();
        device.Address.Should().Be(0UL);
    }

    [Fact]
    public void AddressString_DefaultsToEmpty()
    {
        var device = new DiscoveredBluetoothDevice();
        device.AddressString.Should().BeEmpty();
    }

    [Fact]
    public void IsPaired_DefaultsToFalse()
    {
        var device = new DiscoveredBluetoothDevice();
        device.IsPaired.Should().BeFalse();
    }

    [Fact]
    public void IsZimService_DefaultsToFalse()
    {
        var device = new DiscoveredBluetoothDevice();
        device.IsZimService.Should().BeFalse();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var device = new DiscoveredBluetoothDevice
        {
            DeviceName = "TestDevice",
            Address = 0xAABBCCDDEEFF,
            AddressString = "AA:BB:CC:DD:EE:FF",
            IsPaired = true,
            IsZimService = true
        };

        device.DeviceName.Should().Be("TestDevice");
        device.Address.Should().Be(0xAABBCCDDEEFF);
        device.AddressString.Should().Be("AA:BB:CC:DD:EE:FF");
        device.IsPaired.Should().BeTrue();
        device.IsZimService.Should().BeTrue();
    }
}
