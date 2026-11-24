namespace EdgeHub.SystemStats;

/// <summary>The last samples of each measure, oldest first, copied when the snapshot was taken.</summary>
public sealed record StatsHistory(
    IReadOnlyList<double> Cpu,
    IReadOnlyList<double> MemoryPercent,
    IReadOnlyList<double> Download,
    IReadOnlyList<double> Upload)
{
    public static StatsHistory Empty { get; } = new([], [], [], []);
}

/// <summary>The PC's light stats at one moment. Immutable, so it can be shared between readers.</summary>
public sealed record SystemSnapshot(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    string? NetworkInterface,
    IReadOnlyList<DiskUsage> Disks)
{
    public StatsHistory History { get; init; } = StatsHistory.Empty;
}

public interface ISystemStats
{
    /// <summary>The last snapshot taken, or null before the first one. Safe to read from any thread.</summary>
    SystemSnapshot? Latest { get; }

    /// <summary>
    /// Takes a reading: CPU load and network rates are the change since the previous reading. Meant to be called by one
    /// periodic owner; a call less than half a second after the previous reading returns that reading unchanged.
    /// </summary>
    SystemSnapshot Sample();
}

/// <summary>Turns raw readings into loads and rates, and keeps the last 60 samples of each measure.</summary>
public sealed class SystemStatsSampler(ISystemReadings readings, TimeProvider time) : ISystemStats
{
    private const int HistoryLength = 60;
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private readonly MetricHistory _cpu = new(HistoryLength);
    private readonly MetricHistory _memory = new(HistoryLength);
    private readonly MetricHistory _download = new(HistoryLength);
    private readonly MetricHistory _upload = new(HistoryLength);

    private CpuTimes? _previousCpu;
    private NetworkReading? _previousNetwork;
    private long _previousNetworkTimestamp;
    private long _previousSampleTimestamp;
    private SystemSnapshot? _latest;

    public SystemSnapshot? Latest => Volatile.Read(ref _latest);

    public SystemSnapshot Sample()
    {
        lock (_gate)
        {
            if (_latest is not null && time.GetElapsedTime(_previousSampleTimestamp) < MinimumInterval)
            {
                return _latest;
            }

            _previousSampleTimestamp = time.GetTimestamp();
            bool isFirst = _latest is null;

            CpuTimes cpu = readings.ReadCpuTimes();
            double cpuPercent = _previousCpu is { } previousCpu ? CpuUsage.Between(previousCpu, cpu) : 0;
            _previousCpu = cpu;

            MemoryReading memory = readings.ReadMemory();
            long memoryUsed = memory.TotalBytes - memory.AvailableBytes;

            NetworkReading? network = readings.ReadNetwork();
            long networkTimestamp = time.GetTimestamp();
            double download = 0;
            double upload = 0;
            if (network is not null && _previousNetwork is { } previousNetwork && previousNetwork.InterfaceName == network.InterfaceName)
            {
                TimeSpan elapsed = time.GetElapsedTime(_previousNetworkTimestamp, networkTimestamp);
                download = ByteRate.PerSecond(previousNetwork.BytesReceived, network.BytesReceived, elapsed);
                upload = ByteRate.PerSecond(previousNetwork.BytesSent, network.BytesSent, elapsed);
            }

            _previousNetwork = network;
            _previousNetworkTimestamp = networkTimestamp;

            // The first reading only sets the starting point: its CPU load and rates mean nothing yet.
            if (!isFirst)
            {
                _cpu.Add(cpuPercent);
                _download.Add(download);
                _upload.Add(upload);
            }

            _memory.Add(memory.TotalBytes > 0 ? memoryUsed * 100.0 / memory.TotalBytes : 0);

            var snapshot = new SystemSnapshot(cpuPercent, memoryUsed, memory.TotalBytes, download, upload, network?.InterfaceName, readings.ReadDisks())
            {
                History = new StatsHistory(_cpu.Values, _memory.Values, _download.Values, _upload.Values),
            };
            Volatile.Write(ref _latest, snapshot);
            return snapshot;
        }
    }
}
