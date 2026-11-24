using Windows.Media.Control;

namespace EdgeHub.Spotify;

/// <summary>
/// Reads and drives the media session through Windows' media controls (GSMTC). Spotify's desktop app shows up there,
/// so neither the Spotify Web API nor a Premium account is needed. Calls go to another process: each one has a time
/// limit, and any failure means "no session" rather than an exception.
/// </summary>
public sealed class WindowsMediaController : IMediaController
{
    private static readonly TimeSpan ReadLimit = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CommandLimit = TimeSpan.FromSeconds(2);

    private readonly GlobalSystemMediaTransportControlsSessionManager _manager;
    private readonly TimeProvider _time;
    private MediaState? _lastState;

    private WindowsMediaController(GlobalSystemMediaTransportControlsSessionManager manager, TimeProvider time)
    {
        _manager = manager;
        _time = time;
    }

    public static async Task<WindowsMediaController> CreateAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken).ConfigureAwait(false);
        return new WindowsMediaController(manager, time);
    }

    public async Task<MediaState?> GetStateAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ReadLimit);
        try
        {
            GlobalSystemMediaTransportControlsSession? session = ChosenSession();
            if (session is null)
            {
                return _lastState = null;
            }

            var properties = await session.TryGetMediaPropertiesAsync().AsTask(limit.Token).ConfigureAwait(false);
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            bool isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            TimeSpan duration = timeline.EndTime - timeline.StartTime;
            TimeSpan position = PlaybackPosition.Estimate(
                timeline.Position - timeline.StartTime, timeline.LastUpdatedTime, _time.GetUtcNow(), isPlaying, duration);

            return _lastState = new MediaState(
                session.SourceAppUserModelId,
                properties.Title ?? "",
                properties.Artist ?? "",
                properties.AlbumTitle ?? "",
                isPlaying,
                position,
                duration);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The media app is slow to answer: show what it said last time rather than nothing.
            return _lastState;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The app closed or refused the call (the interop layer maps these errors to various exception types).
            return _lastState = null;
        }
    }

    public Task<bool> PlayPauseAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TryTogglePlayPauseAsync(), cancellationToken);

    public Task<bool> NextAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TrySkipNextAsync(), cancellationToken);

    public Task<bool> PreviousAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TrySkipPreviousAsync(), cancellationToken);

    private async Task<bool> SendAsync(
        Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> command,
        CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(CommandLimit);
        try
        {
            GlobalSystemMediaTransportControlsSession? session = ChosenSession();
            return session is not null && await command(session).AsTask(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return false;
        }
    }

    private GlobalSystemMediaTransportControlsSession? ChosenSession()
    {
        var sessions = _manager.GetSessions();
        var current = _manager.GetCurrentSession();
        string? chosen = MediaSessionPicker.Pick(sessions.Select(s => s.SourceAppUserModelId).ToList(), current?.SourceAppUserModelId);

        return sessions.FirstOrDefault(s => s.SourceAppUserModelId == chosen) ?? current;
    }
}
