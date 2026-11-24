using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;

namespace EdgeHub.Configuration;

/// <summary>Section <c>Server</c>.</summary>
public sealed class ServerOptions
{
    public const string Section = "Server";

    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    /// <summary>
    /// False by default: the service only answers on this PC. The tablet needs true, plus a firewall rule for the port
    /// on the Private profile.
    /// </summary>
    public bool ListenOnNetwork { get; set; }

    /// <summary>The addresses to listen on: this PC's loopback addresses only, unless <see cref="ListenOnNetwork"/> is set.</summary>
    public IReadOnlyList<IPAddress> ListenAddresses()
    {
        if (ListenOnNetwork)
        {
            // IPv6 "any" also accepts IPv4 connections (dual mode) where IPv6 exists.
            return [Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any];
        }

        return Socket.OSSupportsIPv6 ? [IPAddress.Loopback, IPAddress.IPv6Loopback] : [IPAddress.Loopback];
    }
}
