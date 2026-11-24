using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.ConsoleMode;

/// <summary>
/// The console's loop, without the console itself: commands arrive on a channel and frames go to a callback.
/// Stats are sampled once a second on a monotonic clock (a change of the PC's time does not freeze it); a command
/// only redraws, so it never adds a sample.
/// </summary>
public static class ConsoleLoop
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(1);

    public static async Task<int> RunAsync(
        ISystemStats stats,
        IMediaController media,
        TimeProvider time,
        ChannelReader<ConsoleCommand> keys,
        Action<IReadOnlyList<string>> show,
        string? initialStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(show);

        string? status = initialStatus;
        try
        {
            SystemSnapshot snapshot = stats.Sample();
            long lastSample = time.GetTimestamp();
            MediaState? state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);

            while (true)
            {
                show(ConsoleView.Render(snapshot, state, status));

                ConsoleCommand command = await NextCommandAsync(keys, Refresh - time.GetElapsedTime(lastSample), time, cancellationToken)
                    .ConfigureAwait(false);

                if (command == ConsoleCommand.Quit)
                {
                    return 0;
                }

                if (command != ConsoleCommand.None)
                {
                    status = await MediaCommands.SendAsync(command, media, cancellationToken).ConfigureAwait(false) ?? status;
                    state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (time.GetElapsedTime(lastSample) >= Refresh)
                {
                    snapshot = stats.Sample();
                    lastSample = time.GetTimestamp();
                    state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    /// <summary>The next command, or <see cref="ConsoleCommand.None"/> once <paramref name="wait"/> has passed.</summary>
    private static async Task<ConsoleCommand> NextCommandAsync(
        ChannelReader<ConsoleCommand> keys, TimeSpan wait, TimeProvider time, CancellationToken cancellationToken)
    {
        if (keys.TryRead(out ConsoleCommand ready))
        {
            return ready;
        }

        if (wait <= TimeSpan.Zero)
        {
            return ConsoleCommand.None;
        }

        using var timeout = new CancellationTokenSource(wait, time);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            if (await keys.WaitToReadAsync(waiting.Token).ConfigureAwait(false))
            {
                return keys.TryRead(out ConsoleCommand command) ? command : ConsoleCommand.None;
            }

            // The input has ended: keep refreshing until Ctrl+C.
            await Task.Delay(wait, time, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // No command before the next sample.
        }

        return ConsoleCommand.None;
    }
}
