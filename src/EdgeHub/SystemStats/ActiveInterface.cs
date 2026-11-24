namespace EdgeHub.SystemStats;

/// <summary>What the picker needs to know about one network interface.</summary>
public sealed record InterfaceInfo(string Id, bool IsUp, bool IsLoopbackOrTunnel, bool HasIPv4Gateway, long TotalBytes);

public static class ActiveInterface
{
    /// <summary>
    /// The interface that carries the PC's traffic: up, not a loopback or tunnel, with a default gateway if any has one,
    /// and the busiest among those.
    /// </summary>
    public static InterfaceInfo? Pick(IReadOnlyList<InterfaceInfo> interfaces)
    {
        var usable = interfaces.Where(i => i.IsUp && !i.IsLoopbackOrTunnel).ToList();
        var withGateway = usable.Where(i => i.HasIPv4Gateway).ToList();

        return (withGateway.Count > 0 ? withGateway : usable).MaxBy(i => i.TotalBytes);
    }
}
