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
    /// <summary>The current track, or null when no media app is open.</summary>
    Task<MediaState?> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Each command returns false when there is no session or the app refused it.</summary>
    Task<bool> PlayPauseAsync(CancellationToken cancellationToken);

    Task<bool> NextAsync(CancellationToken cancellationToken);

    Task<bool> PreviousAsync(CancellationToken cancellationToken);
}
