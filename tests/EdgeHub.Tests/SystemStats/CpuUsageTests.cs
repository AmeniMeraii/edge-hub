using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

public sealed class CpuUsageTests
{
    [Fact]
    public void Busy_share_of_the_elapsed_processor_time()
    {
        // Kernel time includes idle time: 1400 ticks elapsed, 600 of them idle, 800 busy.
        var previous = new CpuTimes(Idle: 1000, Kernel: 3000, User: 1000);
        var current = new CpuTimes(Idle: 1600, Kernel: 4000, User: 1400);

        Assert.Equal(57.143, CpuUsage.Between(previous, current), precision: 3);
    }

    [Fact]
    public void Zero_when_no_processor_time_elapsed()
    {
        var times = new CpuTimes(Idle: 10, Kernel: 30, User: 10);

        Assert.Equal(0, CpuUsage.Between(times, times));
    }

    [Fact]
    public void Never_below_zero_when_idle_grows_more_than_the_total()
    {
        var previous = new CpuTimes(Idle: 0, Kernel: 0, User: 0);
        var current = new CpuTimes(Idle: 500, Kernel: 400, User: 0);

        Assert.Equal(0, CpuUsage.Between(previous, current));
    }
}
