using EdgeHub.SystemStats;

namespace EdgeHub.Tests.TestSupport;

/// <summary>Returns the same snapshot on every sample and counts the samples taken.</summary>
internal sealed class FakeSystemStats : ISystemStats
{
    public SystemSnapshot Snapshot { get; set; } = new(
        CpuPercent: 12,
        MemoryUsedBytes: 8L << 30,
        MemoryTotalBytes: 16L << 30,
        DownloadBytesPerSecond: 2_048,
        UploadBytesPerSecond: 1_024,
        NetworkInterface: "Wi-Fi",
        Disks: [new DiskUsage("C:", TotalBytes: 500L << 30, FreeBytes: 120L << 30)]);

    public int SampleCount { get; private set; }

    public SystemSnapshot? Latest => SampleCount == 0 ? null : Snapshot;

    public SystemSnapshot Sample()
    {
        SampleCount++;
        return Snapshot;
    }
}
