using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace EdgeHub.Spotify;

/// <summary>
/// Reads and drives the media session through Windows' media controls (GSMTC). Spotify's desktop app shows up there,
/// so neither the Spotify Web API nor a Premium account is needed.
/// </summary>
public sealed class WindowsMediaController : IMediaController
{
    private readonly GlobalSystemMediaTransportControlsSessionManager _manager;
    private readonly TimeProvider _time;

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
        GlobalSystemMediaTransportControlsSession? session = ChosenSession();
        if (session is null)
        {
            return null;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            bool isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            TimeSpan duration = timeline.EndTime - timeline.StartTime;
            TimeSpan position = PlaybackPosition.Estimate(
                timeline.Position - timeline.StartTime, timeline.LastUpdatedTime, _time.GetUtcNow(), isPlaying, duration);

            return new MediaState(
                session.SourceAppUserModelId,
                properties.Title ?? "",
                properties.Artist ?? "",
                properties.AlbumTitle ?? "",
                isPlaying,
                position,
                duration);
        }
        catch (COMException)
        {
            // The app closed between listing the sessions and reading this one.
            return null;
        }
    }

    public Task<bool> PlayPauseAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TryTogglePlayPauseAsync().AsTask(cancellationToken));

    public Task<bool> NextAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TrySkipNextAsync().AsTask(cancellationToken));

    public Task<bool> PreviousAsync(CancellationToken cancellationToken) =>
        SendAsync(s => s.TrySkipPreviousAsync().AsTask(cancellationToken));

    private async Task<bool> SendAsync(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command)
    {
        GlobalSystemMediaTransportControlsSession? session = ChosenSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            return await command(session).ConfigureAwait(false);
        }
        catch (COMException)
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
