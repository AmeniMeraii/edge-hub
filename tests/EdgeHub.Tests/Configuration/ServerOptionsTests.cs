using System.Net;
using EdgeHub.Configuration;

namespace EdgeHub.Tests.Configuration;

public sealed class ServerOptionsTests
{
    [Fact]
    public void By_default_the_service_only_listens_on_this_pc()
    {
        IReadOnlyList<IPAddress> addresses = new ServerOptions().ListenAddresses();

        Assert.NotEmpty(addresses);
        Assert.All(addresses, address => Assert.True(IPAddress.IsLoopback(address), address.ToString()));
    }

    [Fact]
    public void Opened_to_the_network_it_listens_on_every_interface()
    {
        IReadOnlyList<IPAddress> addresses = new ServerOptions { ListenOnNetwork = true }.ListenAddresses();

        Assert.Contains(addresses, address => address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
    }
}
