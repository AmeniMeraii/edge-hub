using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EdgeHub.ClaudeHooks;

/// <summary>
/// Tells a request made on this PC from one made by another device. The hooks are taken from the PC only; Allow and
/// Refuse never are, since anything running on the PC (an agent, a script) could press them.
/// </summary>
public static class ThisPc
{
    public static bool IsTheCaller(ConnectionInfo connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return IsTheCaller(connection.RemoteIpAddress, connection.LocalIpAddress, OwnAddresses());
    }

    /// <param name="remote">Null for a request made inside the service, as in the tests.</param>
    /// <param name="local">The address the request reached: the same as <paramref name="remote"/> means the same machine.</param>
    /// <param name="own">The PC's addresses, on every network adapter; null when they cannot be read.</param>
    public static bool IsTheCaller(IPAddress? remote, IPAddress? local, IEnumerable<IPAddress>? own)
    {
        // Unknown addresses: taken as the PC, so that the tablet's answers are refused rather than wrongly accepted.
        if (remote is null || own is null)
        {
            return true;
        }

        IPAddress caller = Plain(remote);
        return IPAddress.IsLoopback(caller)
            || (local is not null && Plain(local).Equals(caller))
            || own.Select(Plain).Contains(caller);
    }

    /// <summary>The addresses of every network adapter of the PC, read again at each call (the Wi-Fi can change), or null.</summary>
    public static IReadOnlyList<IPAddress>? OwnAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static IPAddress Plain(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
