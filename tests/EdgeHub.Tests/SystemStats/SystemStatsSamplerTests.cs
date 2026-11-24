using EdgeHub.SystemStats;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.SystemStats;

public sealed class SystemStatsSamplerTests
{
    private readonly FakeReadings _readings = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    private SystemStatsSampler CreateSampler() => new(_readings, _clock);

    [Fact]
    public void First_sample_has_no_cpu_load_and_no_traffic_yet()
    {
        _readings.Cpu = new CpuTimes(Idle: 1000, Kernel: 3000, User: 1000);
        _readings.Network = new NetworkReading("Wi-Fi", BytesReceived: 1_000_000, BytesSent: 200_000);

        SystemSnapshot snapshot = CreateSampler().Sample();

        Assert.Equal(0, snapshot.CpuPercent);
        Assert.Equal(0, snapshot.DownloadBytesPerSecond);
        Assert.Equal(0, snapshot.UploadBytesPerSecond);
    }

    [Fact]
    public void Cpu_load_comes_from_the_change_since_the_previous_sample()
    {
        var sampler = CreateSampler();
        _readings.Cpu = new CpuTimes(Idle: 1000, Kernel: 3000, User: 1000);
        sampler.Sample();

        _readings.Cpu = new CpuTimes(Idle: 1600, Kernel: 4000, User: 1400);
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(57.143, sampler.Sample().CpuPercent, precision: 3);
    }

    [Fact]
    public void Rates_come_from_the_change_on_the_active_interface()
    {
        var sampler = CreateSampler();
        _readings.Network = new NetworkReading("Wi-Fi", BytesReceived: 1_000_000, BytesSent: 200_000);
        sampler.Sample();

        _readings.Network = new NetworkReading("Wi-Fi", BytesReceived: 3_000_000, BytesSent: 300_000);
        _clock.Advance(TimeSpan.FromSeconds(2));
        SystemSnapshot snapshot = sampler.Sample();

        Assert.Equal(1_000_000, snapshot.DownloadBytesPerSecond);
        Assert.Equal(50_000, snapshot.UploadBytesPerSecond);
        Assert.Equal("Wi-Fi", snapshot.NetworkInterface);
    }

    [Fact]
    public void No_rate_when_the_active_interface_changed_between_samples()
    {
        var sampler = CreateSampler();
        _readings.Network = new NetworkReading("Wi-Fi", BytesReceived: 1_000_000, BytesSent: 200_000);
        sampler.Sample();

        _readings.Network = new NetworkReading("Ethernet", BytesReceived: 9_000_000, BytesSent: 900_000);
        _clock.Advance(TimeSpan.FromSeconds(1));
        SystemSnapshot snapshot = sampler.Sample();

        Assert.Equal(0, snapshot.DownloadBytesPerSecond);
        Assert.Equal(0, snapshot.UploadBytesPerSecond);
    }

    [Fact]
    public void No_interface_and_no_traffic_without_network()
    {
        _readings.Network = null;

        SystemSnapshot snapshot = CreateSampler().Sample();

        Assert.Null(snapshot.NetworkInterface);
        Assert.Equal(0, snapshot.DownloadBytesPerSecond);
    }

    [Fact]
    public void Memory_used_is_total_minus_available_and_its_share_goes_to_the_history()
    {
        _readings.Memory = new MemoryReading(TotalBytes: 16L << 30, AvailableBytes: 6L << 30);
        var sampler = CreateSampler();

        SystemSnapshot snapshot = sampler.Sample();

        Assert.Equal(10L << 30, snapshot.MemoryUsedBytes);
        Assert.Equal(16L << 30, snapshot.MemoryTotalBytes);
        Assert.Equal(62.5, sampler.MemoryHistory.Latest);
    }

    [Fact]
    public void Each_history_keeps_the_last_sixty_samples()
    {
        var sampler = CreateSampler();
        for (int i = 0; i < 61; i++)
        {
            _readings.Cpu = new CpuTimes(Idle: i * 50, Kernel: i * 100, User: 0);
            _clock.Advance(TimeSpan.FromSeconds(1));
            sampler.Sample();
        }

        Assert.Equal(60, sampler.CpuHistory.Values.Count);
        Assert.Equal(60, sampler.MemoryHistory.Values.Count);
        Assert.Equal(60, sampler.DownloadHistory.Values.Count);
        Assert.Equal(60, sampler.UploadHistory.Values.Count);
        Assert.Equal(50, sampler.CpuHistory.Latest);
    }

    [Fact]
    public void Disks_are_passed_through()
    {
        _readings.Disks = [new DiskUsage("C:", TotalBytes: 500L << 30, FreeBytes: 120L << 30)];

        Assert.Equal(_readings.Disks, CreateSampler().Sample().Disks);
    }
}
