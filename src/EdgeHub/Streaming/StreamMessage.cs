using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Streaming;

/// <summary>
/// The JSON each <c>/api/stream</c> event carries: only what the screen shows. The network adapter's name and the drives
/// stay on the PC. Times are in milliseconds, ready for the page's script.
/// </summary>
public sealed record StreamMessage(StreamStats? Stats, StreamTrack? Media)
{
    public static StreamMessage From(LiveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new StreamMessage(
            state.Stats is { } stats ? StreamStats.From(stats) : null,
            state.Media is { } track ? StreamTrack.From(track) : null);
    }
}

public sealed record StreamStats(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    StatsHistory History,
    DateTimeOffset TakenAt)
{
    public static StreamStats From(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new StreamStats(
            snapshot.CpuPercent,
            snapshot.MemoryUsedBytes,
            snapshot.MemoryTotalBytes,
            snapshot.DownloadBytesPerSecond,
            snapshot.UploadBytesPerSecond,
            snapshot.History,
            snapshot.TakenAt);
    }
}

public sealed record StreamTrack(
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    long PositionMs,
    long DurationMs,
    bool IsSpotify)
{
    public static StreamTrack From(MediaState track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new StreamTrack(
            track.Title,
            track.Artist,
            track.Album,
            track.IsPlaying,
            (long)track.Position.TotalMilliseconds,
            (long)track.Duration.TotalMilliseconds,
            track.AppId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
    }
}
