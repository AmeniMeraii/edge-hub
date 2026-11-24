using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

public sealed class ByteRateTests
{
    [Fact]
    public void Bytes_per_second_over_the_elapsed_time()
    {
        Assert.Equal(500_000, ByteRate.PerSecond(previousBytes: 2_000_000, currentBytes: 3_000_000, elapsed: TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Zero_when_no_time_elapsed()
    {
        Assert.Equal(0, ByteRate.PerSecond(previousBytes: 0, currentBytes: 1_000, elapsed: TimeSpan.Zero));
    }

    [Fact]
    public void Zero_when_the_counter_was_reset()
    {
        // Counters restart from zero when the network adapter reconnects.
        Assert.Equal(0, ByteRate.PerSecond(previousBytes: 9_000_000, currentBytes: 1_000, elapsed: TimeSpan.FromSeconds(1)));
    }
}
