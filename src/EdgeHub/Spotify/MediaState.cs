namespace EdgeHub.Spotify;

/// <summary>What is playing, as Windows' media controls report it.</summary>
public sealed record MediaState(
    string AppId,
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration);

/// <summary>Reads and drives the media session shown on the screen (Spotify first).</summary>
public interface IMediaController
{
    /// <summary>
    /// The current track; null when no media app is open or the media controls fail. An implementation may return the
    /// last known state for a short while when the media app is slow to answer.
    /// </summary>
    Task<MediaState?> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Each command returns false when there is no session or the app refused it.</summary>
    Task<bool> PlayPauseAsync(CancellationToken cancellationToken);

    Task<bool> NextAsync(CancellationToken cancellationToken);

    Task<bool> PreviousAsync(CancellationToken cancellationToken);
}
