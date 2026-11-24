using System.Text;
using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.ConsoleMode;

/// <summary>
/// <c>EdgeHub.exe --console</c>: shows the PC stats and the current track every second, and drives the media session
/// from the keyboard. Keys can also be piped in as characters, so a script can test the commands; piped input does not
/// end the console, so a script must send <c>q</c>.
/// </summary>
public static class ConsoleProbe
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(1);

    /// <summary>Runs the console on this PC's real stats and media session.</summary>
    public static async Task<int> RunOnWindowsAsync(CancellationToken cancellationToken)
    {
        var stats = new SystemStatsSampler(new WindowsSystemReadings(), TimeProvider.System);
        IMediaController media;
        try
        {
            media = await WindowsMediaController.CreateAsync(TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            media = new NoMediaController();
        }

        return await RunAsync(stats, media, TimeProvider.System, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(ISystemStats stats, IMediaController media, TimeProvider time, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(time);

        Encoding previousEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;
        ChannelReader<ConsoleCommand>? pipedKeys = Console.IsInputRedirected ? ReadPipedKeys(cancellationToken) : null;
        using var screen = new Screen(redraw: !Console.IsOutputRedirected);
        string? status = null;

        try
        {
            // Stats are sampled on a fixed one-second schedule; a key press only redraws, so it never adds a sample.
            SystemSnapshot snapshot = stats.Sample();
            MediaState? state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
            DateTimeOffset nextSample = time.GetUtcNow() + Refresh;

            while (true)
            {
                screen.Show(ConsoleView.Render(snapshot, state, status));

                ConsoleCommand command = pipedKeys is null
                    ? await WaitForKeyAsync(time, nextSample, cancellationToken).ConfigureAwait(false)
                    : await WaitForPipedKeyAsync(pipedKeys, time, nextSample, cancellationToken).ConfigureAwait(false);

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

                snapshot = stats.Sample();
                state = await media.GetStateAsync(cancellationToken).ConfigureAwait(false);
                DateTimeOffset now = time.GetUtcNow();
                nextSample = nextSample + Refresh > now ? nextSample + Refresh : now + Refresh;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        finally
        {
            Console.OutputEncoding = previousEncoding;
        }
    }

    /// <summary>Waits for a command key until <paramref name="until"/>, then returns <see cref="ConsoleCommand.None"/>.</summary>
    private static async Task<ConsoleCommand> WaitForKeyAsync(TimeProvider time, DateTimeOffset until, CancellationToken cancellationToken)
    {
        while (time.GetUtcNow() < until)
        {
            if (Console.KeyAvailable)
            {
                ConsoleCommand command = ConsoleCommands.FromKey(Console.ReadKey(intercept: true).Key);
                if (command != ConsoleCommand.None)
                {
                    return command;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), time, cancellationToken).ConfigureAwait(false);
        }

        return ConsoleCommand.None;
    }

    private static async Task<ConsoleCommand> WaitForPipedKeyAsync(
        ChannelReader<ConsoleCommand> keys, TimeProvider time, DateTimeOffset until, CancellationToken cancellationToken)
    {
        TimeSpan remaining = until - time.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return ConsoleCommand.None;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(remaining);
        try
        {
            if (await keys.WaitToReadAsync(timeout.Token).ConfigureAwait(false) && keys.TryRead(out ConsoleCommand command))
            {
                return command;
            }

            // The input has ended: keep refreshing until Ctrl+C.
            await Task.Delay(remaining, time, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // No key before the next sample.
        }

        return ConsoleCommand.None;
    }

    private static ChannelReader<ConsoleCommand> ReadPipedKeys(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ConsoleCommand>();
        _ = Task.Run(
            async () =>
            {
                Exception? failure = null;
                try
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
                }
                catch (Exception e)
                {
                    failure = e;
                }
                finally
                {
                    channel.Writer.TryComplete(failure is OperationCanceledException ? null : failure);
                }
            },
            cancellationToken);
        return channel.Reader;
    }

    /// <summary>Redraws in place in a terminal; prints one frame after another when the output is redirected.</summary>
    private sealed class Screen : IDisposable
    {
        private readonly bool _redraw;
        private readonly bool _cursorWasVisible;
        private int _width = -1;
        private int _previousLineCount;

        public Screen(bool redraw)
        {
            _redraw = redraw;
            if (redraw && OperatingSystem.IsWindows())
            {
                _cursorWasVisible = Console.CursorVisible;
                Console.CursorVisible = false;
            }
        }

        public void Show(IReadOnlyList<string> lines)
        {
            if (!_redraw)
            {
                foreach (string line in lines)
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine(new string('-', 40));
                return;
            }

            int width = Math.Max(Console.WindowWidth - 1, 1);
            if (width != _width)
            {
                // First frame, or the window was resized: start from a clean window.
                Console.Clear();
                _width = width;
                _previousLineCount = 0;
            }

            Console.SetCursorPosition(0, Console.WindowTop);
            foreach (string line in lines)
            {
                Console.WriteLine(Fit(line, width));
            }

            for (int i = lines.Count; i < _previousLineCount; i++)
            {
                Console.WriteLine(new string(' ', width));
            }

            _previousLineCount = lines.Count;
        }

        public void Dispose()
        {
            if (_redraw && OperatingSystem.IsWindows())
            {
                Console.CursorVisible = _cursorWasVisible;
            }
        }

        /// <summary>Pads or cuts a line to the window width, without splitting a character such as an emoji.</summary>
        private static string Fit(string line, int width)
        {
            var info = new System.Globalization.StringInfo(line);
            return info.LengthInTextElements <= width
                ? line + new string(' ', width - info.LengthInTextElements)
                : info.SubstringByTextElements(0, width);
        }
    }
}
