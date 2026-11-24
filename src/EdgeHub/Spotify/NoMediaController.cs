namespace EdgeHub.Spotify;

/// <summary>Stands in when Windows' media controls cannot be reached: no session, every command refused.</summary>
public sealed class NoMediaController : IMediaController
{
    public Task<MediaState?> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult<MediaState?>(null);

    public Task<bool> PlayPauseAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> NextAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> PreviousAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
