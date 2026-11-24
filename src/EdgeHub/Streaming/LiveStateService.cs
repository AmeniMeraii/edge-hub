using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Streaming;

/// <summary>
/// Samples the stats and reads the current track once a second, and publishes them to the connected screens.
/// It is the only caller of <see cref="ISystemStats.Sample"/>. A failure is logged and skipped: the service keeps going.
/// </summary>
public sealed partial class LiveStateService(
    ISystemStats stats,
    IMediaController media,
    LiveStateHub hub,
    TimeProvider time,
    ILogger<LiveStateService> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

    /// <summary>One tick: read both sources independently, then publish what could be read.</summary>
    public async Task PublishOnceAsync(CancellationToken cancellationToken)
    {
        SystemSnapshot? snapshot = null;
        try
        {
            snapshot = stats.Sample();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStatsFailed(logger, e);
        }

        MediaState? track = null;
        try
        {
            track = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogMediaFailed(logger, e);
        }

        hub.Publish(new LiveState(snapshot, track));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, time);
        do
        {
            await PublishOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lecture des stats système impossible, tick ignoré")]
    private static partial void LogStatsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lecture du morceau en cours impossible, tick ignoré")]
    private static partial void LogMediaFailed(ILogger logger, Exception exception);
}
