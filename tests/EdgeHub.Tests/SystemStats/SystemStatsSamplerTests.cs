using EdgeHub.SystemStats;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.SystemStats;

public sealed class SystemStatsSamplerTests
{
    private readonly FakeReadings _readings = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    private SystemStatsSampler CreateSampler() => new(_readings, _clock);

    [Fact]
    public void First_sample_has_no_cpu_load_and_no_traffic_and_adds_no_point_for_them()
    {
        _readings.Cpu = new CpuTimes(Idle: 1000, Kernel: 3000, User: 1000);
        _readings.Network = new NetworkReading("Wi-Fi", BytesReceived: 1_000_000, BytesSent: 200_000);

        SystemSnapshot snapshot = CreateSampler().Sample();

        Assert.Equal(0, snapshot.CpuPercent);
        Assert.Equal(0, snapshot.DownloadBytesPerSecond);
        Assert.Equal(0, snapshot.UploadBytesPerSecond);
        Assert.Empty(snapshot.History.Cpu);
        Assert.Empty(snapshot.History.Download);
        Assert.Empty(snapshot.History.Upload);
        Assert.Single(snapshot.History.MemoryPercent);
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

        SystemSnapshot snapshot = CreateSampler().Sample();

        Assert.Equal(10L << 30, snapshot.MemoryUsedBytes);
        Assert.Equal(16L << 30, snapshot.MemoryTotalBytes);
        Assert.Equal([62.5], snapshot.History.MemoryPercent);
    }

    [Fact]
    public void Each_history_keeps_the_last_sixty_samples()
    {
        var sampler = CreateSampler();
        SystemSnapshot snapshot = sampler.Sample();
        for (int i = 1; i <= 61; i++)
        {
            _readings.Cpu = new CpuTimes(Idle: i * 50, Kernel: i * 100, User: 0);
            _clock.Advance(TimeSpan.FromSeconds(1));
            snapshot = sampler.Sample();
        }

        Assert.Equal(60, snapshot.History.Cpu.Count);
        Assert.Equal(60, snapshot.History.MemoryPercent.Count);
        Assert.Equal(60, snapshot.History.Download.Count);
        Assert.Equal(60, snapshot.History.Upload.Count);
        Assert.Equal(50, snapshot.History.Cpu[^1]);
    }

    [Fact]
    public void A_sample_taken_too_soon_returns_the_previous_snapshot_and_adds_no_point()
    {
        // Back-to-back readings give meaningless CPU values (Windows updates its times every ~15 ms).
        var sampler = CreateSampler();
        sampler.Sample();
        _clock.Advance(TimeSpan.FromSeconds(1));
        SystemSnapshot previous = sampler.Sample();

        _readings.Cpu = new CpuTimes(Idle: 0, Kernel: 999_999, User: 999_999);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        SystemSnapshot tooSoon = sampler.Sample();

        Assert.Same(previous, tooSoon);
        Assert.Single(tooSoon.History.Cpu);
    }

    [Fact]
    public void Latest_is_null_before_the_first_sample_then_the_last_snapshot()
    {
        var sampler = CreateSampler();
        Assert.Null(sampler.Latest);

        SystemSnapshot snapshot = sampler.Sample();

        Assert.Same(snapshot, sampler.Latest);
    }

    [Fact]
    public void A_snapshot_keeps_its_history_when_later_samples_arrive()
    {
        var sampler = CreateSampler();
        sampler.Sample();
        _clock.Advance(TimeSpan.FromSeconds(1));
        SystemSnapshot earlier = sampler.Sample();

        _clock.Advance(TimeSpan.FromSeconds(1));
        sampler.Sample();

        Assert.Single(earlier.History.Cpu);
    }

    [Fact]
    public void Disks_are_passed_through()
    {
        _readings.Disks = [new DiskUsage("C:", TotalBytes: 500L << 30, FreeBytes: 120L << 30)];

        Assert.Equal(_readings.Disks, CreateSampler().Sample().Disks);
    }
}
