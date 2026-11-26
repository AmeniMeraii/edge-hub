using System.Net;
using EdgeHub.ClaudeHooks;

namespace EdgeHub.Tests.ClaudeHooks;

public sealed class ThisPcTests
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress[] Own = [Lan, IPAddress.Parse("fe80::1%12")];

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("192.168.1.20")]
    [InlineData("::ffff:192.168.1.20")]
    [InlineData("fe80::1%12")]
    public void A_request_from_one_of_the_pcs_addresses_comes_from_the_pc(string remote)
    {
        Assert.True(ThisPc.IsTheCaller(IPAddress.Parse(remote), local: null, Own));
    }

    [Theory]
    [InlineData("192.168.1.42")]
    [InlineData("::ffff:192.168.1.42")]
    [InlineData("fe80::2%12")]
    public void A_request_from_another_device_does_not(string remote)
    {
        Assert.False(ThisPc.IsTheCaller(IPAddress.Parse(remote), local: Lan, Own));
    }

    [Fact]
    public void A_request_to_the_address_it_comes_from_comes_from_the_pc()
    {
        IPAddress address = IPAddress.Parse("10.0.0.5");

        Assert.True(ThisPc.IsTheCaller(address, local: IPAddress.Parse("::ffff:10.0.0.5"), own: []));
    }

    [Fact]
    public void A_request_without_an_address_was_made_inside_the_service()
    {
        Assert.True(ThisPc.IsTheCaller(remote: null, local: null, own: []));
    }

    [Fact]
    public void A_request_is_taken_as_the_pcs_when_its_addresses_cannot_be_read()
    {
        Assert.True(ThisPc.IsTheCaller(IPAddress.Parse("192.168.1.42"), local: Lan, own: null));
    }

    [Fact]
    public void Knows_the_pcs_own_addresses()
    {
        IReadOnlyList<IPAddress>? own = ThisPc.OwnAddresses();

        Assert.NotNull(own);
        Assert.Contains(own, IPAddress.IsLoopback);
    }
}
