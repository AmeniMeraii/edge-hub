using EdgeHub.Spotify;

namespace EdgeHub.Tests.Spotify;

public sealed class PlaybackPositionTests
{
    private static readonly DateTimeOffset Updated = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(180);

    [Fact]
    public void Moves_on_with_the_clock_while_playing()
    {
        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(30), Updated, now: Updated.AddSeconds(5), isPlaying: true, Duration);

        Assert.Equal(TimeSpan.FromSeconds(35), position);
    }

    [Fact]
    public void Stays_put_while_paused()
    {
        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(30), Updated, now: Updated.AddSeconds(5), isPlaying: false, Duration);

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void Never_goes_past_the_end_of_the_track()
    {
        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(178), Updated, now: Updated.AddSeconds(5), isPlaying: true, Duration);

        Assert.Equal(Duration, position);
    }

    [Fact]
    public void Does_not_move_when_the_duration_is_unknown()
    {
        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(30), Updated, now: Updated.AddSeconds(5), isPlaying: true, duration: TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void Does_not_move_when_the_update_time_was_never_set()
    {
        // Windows reports an unset time as the FILETIME origin, 1 January 1601.
        var unset = new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(30), unset, now: Updated, isPlaying: true, Duration);

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void Never_goes_back_when_the_update_time_is_ahead_of_the_clock()
    {
        TimeSpan position = PlaybackPosition.Estimate(TimeSpan.FromSeconds(30), Updated, now: Updated.AddSeconds(-2), isPlaying: true, Duration);

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }
}
