using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

public sealed class MetricHistoryTests
{
    [Fact]
    public void Keeps_values_oldest_first()
    {
        var history = new MetricHistory(capacity: 3);

        history.Add(1);
        history.Add(2);

        Assert.Equal([1.0, 2.0], history.Values);
    }

    [Fact]
    public void Drops_the_oldest_value_once_full()
    {
        var history = new MetricHistory(capacity: 3);

        foreach (double value in new[] { 1.0, 2, 3, 4, 5 })
        {
            history.Add(value);
        }

        Assert.Equal([3.0, 4.0, 5.0], history.Values);
    }

    [Fact]
    public void Latest_is_the_last_value_added_or_null_when_empty()
    {
        var history = new MetricHistory(capacity: 3);
        Assert.Null(history.Latest);

        history.Add(7);
        history.Add(9);

        Assert.Equal(9.0, history.Latest);
    }

    [Fact]
    public void Latest_follows_the_newest_value_once_the_buffer_wraps()
    {
        var history = new MetricHistory(capacity: 3);

        foreach (double value in new[] { 1.0, 2, 3, 4, 5 })
        {
            history.Add(value);
        }

        Assert.Equal(5.0, history.Latest);
    }

    [Fact]
    public void Refuses_a_capacity_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricHistory(capacity: 0));
    }
}
