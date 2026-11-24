using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

/// <summary>Readings set by the test, returned as they are.</summary>
internal sealed class FakeReadings : ISystemReadings
{
    public CpuTimes Cpu { get; set; }

    public MemoryReading Memory { get; set; } = new(TotalBytes: 16L << 30, AvailableBytes: 8L << 30);

    public NetworkReading? Network { get; set; }

    public IReadOnlyList<DiskUsage> Disks { get; set; } = [];

    public CpuTimes ReadCpuTimes() => Cpu;

    public MemoryReading ReadMemory() => Memory;

    public NetworkReading? ReadNetwork() => Network;

    public IReadOnlyList<DiskUsage> ReadDisks() => Disks;
}
