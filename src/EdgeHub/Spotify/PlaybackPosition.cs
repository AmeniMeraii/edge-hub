namespace EdgeHub.Spotify;

public static class PlaybackPosition
{
    // Windows reports an unset update time as the FILETIME origin (1601); anything that old was never set.
    private static readonly DateTimeOffset NeverSet = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Where the track is now. Windows only reports the position at its last update, so while playing the time elapsed
    /// since then is added, without going past the end of the track. Without a duration or an update time, the
    /// reported position is kept as it is.
    /// </summary>
    public static TimeSpan Estimate(TimeSpan position, DateTimeOffset lastUpdated, DateTimeOffset now, bool isPlaying, TimeSpan duration)
    {
        bool canMove = isPlaying && duration > TimeSpan.Zero && lastUpdated > NeverSet && now > lastUpdated;
        if (!canMove)
        {
            return position;
        }

        TimeSpan estimate = position + (now - lastUpdated);
        return estimate > duration ? duration : estimate;
    }
}
