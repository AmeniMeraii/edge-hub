using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

public sealed class ActiveInterfaceTests
{
    private static InterfaceInfo Wifi(long bytes = 1_000) => new("wifi", IsUp: true, IsLoopbackOrTunnel: false, HasIPv4Gateway: true, TotalBytes: bytes);

    [Fact]
    public void Prefers_an_interface_with_a_gateway_over_a_busier_one_without()
    {
        // A virtual switch (WSL, Hyper-V) can move more bytes than the real link, but it has no gateway.
        var virtualSwitch = new InterfaceInfo("vswitch", IsUp: true, IsLoopbackOrTunnel: false, HasIPv4Gateway: false, TotalBytes: 50_000);

        Assert.Equal("wifi", ActiveInterface.Pick([virtualSwitch, Wifi()])?.Id);
    }

    [Fact]
    public void Picks_the_busiest_of_the_interfaces_with_a_gateway()
    {
        var ethernet = new InterfaceInfo("ethernet", IsUp: true, IsLoopbackOrTunnel: false, HasIPv4Gateway: true, TotalBytes: 9_000);

        Assert.Equal("ethernet", ActiveInterface.Pick([Wifi(bytes: 1_000), ethernet])?.Id);
    }

    [Fact]
    public void Ignores_interfaces_that_are_down_loopback_or_tunnels()
    {
        var down = new InterfaceInfo("down", IsUp: false, IsLoopbackOrTunnel: false, HasIPv4Gateway: true, TotalBytes: 90_000);
        var tunnel = new InterfaceInfo("vpn", IsUp: true, IsLoopbackOrTunnel: true, HasIPv4Gateway: true, TotalBytes: 90_000);

        Assert.Equal("wifi", ActiveInterface.Pick([down, tunnel, Wifi()])?.Id);
    }

    [Fact]
    public void Falls_back_to_the_busiest_interface_that_is_up_when_none_has_a_gateway()
    {
        var a = new InterfaceInfo("a", IsUp: true, IsLoopbackOrTunnel: false, HasIPv4Gateway: false, TotalBytes: 10);
        var b = new InterfaceInfo("b", IsUp: true, IsLoopbackOrTunnel: false, HasIPv4Gateway: false, TotalBytes: 20);

        Assert.Equal("b", ActiveInterface.Pick([a, b])?.Id);
    }

    [Fact]
    public void None_when_no_interface_is_up()
    {
        var down = new InterfaceInfo("down", IsUp: false, IsLoopbackOrTunnel: false, HasIPv4Gateway: true, TotalBytes: 10);

        Assert.Null(ActiveInterface.Pick([down]));
    }
}
