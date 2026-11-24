namespace EdgeHub.Spotify;

public static class PlaybackPosition
{
    /// <summary>
    /// Where the track is now. Windows only reports the position at its last update, so while playing the time elapsed
    /// since then is added, without going past the end of the track.
    /// </summary>
    public static TimeSpan Estimate(TimeSpan position, DateTimeOffset lastUpdated, DateTimeOffset now, bool isPlaying, TimeSpan duration)
    {
        TimeSpan elapsed = isPlaying && now > lastUpdated ? now - lastUpdated : TimeSpan.Zero;
        TimeSpan estimate = position + elapsed;

        return duration > TimeSpan.Zero && estimate > duration ? duration : estimate;
    }
}
