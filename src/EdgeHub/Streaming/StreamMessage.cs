using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Streaming;

/// <summary>The JSON each <c>/api/stream</c> event carries. Times are in milliseconds, ready for the page's script.</summary>
public sealed record StreamMessage(SystemSnapshot? Stats, StreamTrack? Media)
{
    public static StreamMessage From(LiveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new StreamMessage(state.Stats, state.Media is { } track ? StreamTrack.From(track) : null);
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
