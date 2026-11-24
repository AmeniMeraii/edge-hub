namespace EdgeHub.SystemStats;

/// <summary>What the picker needs to know about one network interface.</summary>
public sealed record InterfaceInfo(string Id, bool IsUp, bool IsLoopbackOrTunnel, bool HasIPv4Gateway, long TotalBytes);

public static class ActiveInterface
{
    /// <summary>
    /// Fallback guess of the interface that carries the PC's traffic, used when Windows cannot tell which one its default
    /// route uses: up, not a loopback or tunnel, with a default gateway if any has one, and the one that moved the most
    /// bytes since it came up.
    /// </summary>
    public static InterfaceInfo? Pick(IReadOnlyList<InterfaceInfo> interfaces)
    {
        var usable = interfaces.Where(i => i.IsUp && !i.IsLoopbackOrTunnel).ToList();
        var withGateway = usable.Where(i => i.HasIPv4Gateway).ToList();

        return (withGateway.Count > 0 ? withGateway : usable).MaxBy(i => i.TotalBytes);
    }
}
