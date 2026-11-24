namespace EdgeHub.SystemStats;

/// <summary>The PC's light stats at one moment.</summary>
public sealed record SystemSnapshot(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    string? NetworkInterface,
    IReadOnlyList<DiskUsage> Disks);

public interface ISystemStats
{
    MetricHistory CpuHistory { get; }

    MetricHistory MemoryHistory { get; }

    MetricHistory DownloadHistory { get; }

    MetricHistory UploadHistory { get; }

    /// <summary>Takes a reading. CPU load and network rates are the change since the previous call.</summary>
    SystemSnapshot Sample();
}

/// <summary>Turns raw readings into loads and rates, and keeps the last 60 samples of each measure.</summary>
public sealed class SystemStatsSampler(ISystemReadings readings, TimeProvider time) : ISystemStats
{
    private const int HistoryLength = 60;

    private CpuTimes? _previousCpu;
    private NetworkReading? _previousNetwork;
    private long _previousTimestamp;

    public MetricHistory CpuHistory { get; } = new(HistoryLength);

    public MetricHistory MemoryHistory { get; } = new(HistoryLength);

    public MetricHistory DownloadHistory { get; } = new(HistoryLength);

    public MetricHistory UploadHistory { get; } = new(HistoryLength);

    public SystemSnapshot Sample()
    {
        long now = time.GetTimestamp();

        CpuTimes cpu = readings.ReadCpuTimes();
        double cpuPercent = _previousCpu is { } previousCpu ? CpuUsage.Between(previousCpu, cpu) : 0;

        MemoryReading memory = readings.ReadMemory();
        long memoryUsed = memory.TotalBytes - memory.AvailableBytes;

        NetworkReading? network = readings.ReadNetwork();
        double download = 0;
        double upload = 0;
        if (network is not null && _previousNetwork is { } previousNetwork && previousNetwork.InterfaceName == network.InterfaceName)
        {
            TimeSpan elapsed = time.GetElapsedTime(_previousTimestamp, now);
            download = ByteRate.PerSecond(previousNetwork.BytesReceived, network.BytesReceived, elapsed);
            upload = ByteRate.PerSecond(previousNetwork.BytesSent, network.BytesSent, elapsed);
        }

        _previousCpu = cpu;
        _previousNetwork = network;
        _previousTimestamp = now;

        CpuHistory.Add(cpuPercent);
        MemoryHistory.Add(memory.TotalBytes > 0 ? memoryUsed * 100.0 / memory.TotalBytes : 0);
        DownloadHistory.Add(download);
        UploadHistory.Add(upload);

        return new SystemSnapshot(cpuPercent, memoryUsed, memory.TotalBytes, download, upload, network?.InterfaceName, readings.ReadDisks());
    }
}
