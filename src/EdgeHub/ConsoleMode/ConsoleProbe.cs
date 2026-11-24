using System.Text;
using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.ConsoleMode;

/// <summary>
/// <c>EdgeHub.exe --console</c>: shows the PC stats and the current track every second, and drives the media session
/// from the keyboard. Keys can also be piped in as characters, so a script can test the commands.
/// </summary>
public static class ConsoleProbe
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(1);

    /// <summary>Runs the console on this PC's real stats and media session.</summary>
    public static async Task<int> RunOnWindowsAsync(CancellationToken cancellationToken)
    {
        var stats = new SystemStatsSampler(new WindowsSystemReadings(), TimeProvider.System);
        var media = await WindowsMediaController.CreateAsync(TimeProvider.System, cancellationToken).ConfigureAwait(false);
        return await RunAsync(stats, media, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(ISystemStats stats, IMediaController media, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(media);
        Console.OutputEncoding = Encoding.UTF8;
        ChannelReader<ConsoleCommand>? pipedKeys = Console.IsInputRedirected ? ReadPipedKeys(cancellationToken) : null;
        var screen = new Screen(redraw: !Console.IsOutputRedirected);
        string? status = null;

        try
        {
            while (true)
            {
                MediaState? state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
                screen.Show(ConsoleView.Render(stats.Sample(), state, status));

                ConsoleCommand command = pipedKeys is null
                    ? await WaitForKeyAsync(cancellationToken).ConfigureAwait(false)
                    : await WaitForPipedKeyAsync(pipedKeys, cancellationToken).ConfigureAwait(false);

                if (command == ConsoleCommand.Quit)
                {
                    return 0;
                }

                status = await SendAsync(command, media, cancellationToken).ConfigureAwait(false) ?? status;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private static async Task<string?> SendAsync(ConsoleCommand command, IMediaController media, CancellationToken cancellationToken)
    {
        (string label, Func<CancellationToken, Task<bool>>? send) = command switch
        {
            ConsoleCommand.PlayPause => ("Lecture/pause", media.PlayPauseAsync),
            ConsoleCommand.Next => ("Suivant", media.NextAsync),
            ConsoleCommand.Previous => ("Précédent", media.PreviousAsync),
            _ => ("", (Func<CancellationToken, Task<bool>>?)null),
        };
        if (send is null)
        {
            return null;
        }

        bool accepted = await send(cancellationToken).ConfigureAwait(false);
        return accepted ? $"{label} envoyé" : $"{label} refusé : aucune session média, ou l'appli a refusé";
    }

    private static async Task<ConsoleCommand> WaitForKeyAsync(CancellationToken cancellationToken)
    {
        DateTime until = DateTime.UtcNow + Refresh;
        while (DateTime.UtcNow < until)
        {
            if (Console.KeyAvailable)
            {
                ConsoleCommand command = ConsoleCommands.FromKey(Console.ReadKey(intercept: true).Key);
                if (command != ConsoleCommand.None)
                {
                    return command;
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return ConsoleCommand.None;
    }

    private static async Task<ConsoleCommand> WaitForPipedKeyAsync(ChannelReader<ConsoleCommand> keys, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Refresh);
        try
        {
            if (await keys.WaitToReadAsync(timeout.Token).ConfigureAwait(false) && keys.TryRead(out ConsoleCommand command))
            {
                return command;
            }

            // The input has ended: keep refreshing until Ctrl+C.
            await Task.Delay(Refresh, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // No key within the refresh period.
        }

        return ConsoleCommand.None;
    }

    private static ChannelReader<ConsoleCommand> ReadPipedKeys(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ConsoleCommand>();
        _ = Task.Run(
            async () =>
            {
                var buffer = new char[1];
                while (await Console.In.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0)
                {
                    ConsoleCommand command = ConsoleCommands.FromChar(buffer[0]);
                    if (command != ConsoleCommand.None)
                    {
                        channel.Writer.TryWrite(command);
                    }
                }

                channel.Writer.TryComplete();
            },
            cancellationToken);
        return channel.Reader;
    }

    /// <summary>Redraws in place in a terminal; prints one frame after another when the output is redirected.</summary>
    private sealed class Screen(bool redraw)
    {
        private int _previousLineCount;

        public void Show(IReadOnlyList<string> lines)
        {
            if (!redraw)
            {
                foreach (string line in lines)
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine(new string('-', 40));
                return;
            }

            int width = Math.Max(Console.WindowWidth - 1, 1);
            Console.SetCursorPosition(0, 0);
            foreach (string line in lines)
            {
                Console.WriteLine(line.Length > width ? line[..width] : line.PadRight(width));
            }

            for (int i = lines.Count; i < _previousLineCount; i++)
            {
                Console.WriteLine(new string(' ', width));
            }

            _previousLineCount = lines.Count;
        }
    }
}
