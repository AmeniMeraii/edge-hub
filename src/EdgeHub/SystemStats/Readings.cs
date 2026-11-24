namespace EdgeHub.SystemStats;

/// <summary>Raw readings from the operating system, taken without admin rights or drivers.</summary>
public interface ISystemReadings
{
    CpuTimes ReadCpuTimes();

    MemoryReading ReadMemory();

    /// <summary>Byte counters of the interface that carries the PC's traffic, or null without network.</summary>
    NetworkReading? ReadNetwork();

    IReadOnlyList<DiskUsage> ReadDisks();
}

public readonly record struct MemoryReading(long TotalBytes, long AvailableBytes);

public sealed record NetworkReading(string InterfaceName, long BytesReceived, long BytesSent);

public sealed record DiskUsage(string Name, long TotalBytes, long FreeBytes);
