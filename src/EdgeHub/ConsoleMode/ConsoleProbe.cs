using System.Globalization;
using System.Text;
using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.ConsoleMode;

/// <summary>
/// <c>EdgeHub.exe --console</c>: wires <see cref="ConsoleLoop"/> to the real console. Keys come from the keyboard, or
/// as characters when the input is piped (a script must then send <c>q</c> to quit); frames are redrawn in place, or
/// printed one after another when the output is redirected.
/// </summary>
public static class ConsoleProbe
{
    /// <summary>Runs the console on this PC's real stats and media session.</summary>
    public static async Task<int> RunOnWindowsAsync(CancellationToken cancellationToken)
    {
        var stats = new SystemStatsSampler(new WindowsSystemReadings(), TimeProvider.System);
        IMediaController media;
        string? status = null;
        try
        {
            media = await WindowsMediaController.CreateAsync(TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            media = new NoMediaController();
            status = "Contrôles média de Windows indisponibles : le morceau en cours ne peut pas être lu";
        }

        return await RunAsync(stats, media, TimeProvider.System, status, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(
        ISystemStats stats, IMediaController media, TimeProvider time, string? initialStatus, CancellationToken cancellationToken)
    {
        Encoding previousEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;
        using var stopKeys = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ChannelReader<ConsoleCommand> keys = Console.IsInputRedirected ? ReadPipedKeys(stopKeys.Token) : ReadKeyboard(stopKeys.Token);
        using var screen = new Screen(redraw: !Console.IsOutputRedirected);
        try
        {
            return await ConsoleLoop.RunAsync(stats, media, time, keys, screen.Show, initialStatus, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await stopKeys.CancelAsync().ConfigureAwait(false);
            Console.OutputEncoding = previousEncoding;
        }
    }

    private static ChannelReader<ConsoleCommand> ReadKeyboard(CancellationToken cancellationToken) =>
        ReadInBackground(
            async writer =>
            {
                while (true)
                {
                    while (Console.KeyAvailable)
                    {
                        ConsoleCommand command = ConsoleCommands.FromKey(Console.ReadKey(intercept: true).Key);
                        if (command != ConsoleCommand.None)
                        {
                            writer.TryWrite(command);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken);

    private static ChannelReader<ConsoleCommand> ReadPipedKeys(CancellationToken cancellationToken) =>
        ReadInBackground(
            async writer =>
            {
                var buffer = new char[1];
                while (await Console.In.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0)
                {
                    ConsoleCommand command = ConsoleCommands.FromChar(buffer[0]);
                    if (command != ConsoleCommand.None)
                    {
                        writer.TryWrite(command);
                    }
                }
            },
            cancellationToken);

    /// <summary>Runs a key reader on the thread pool; the channel completes when it ends, fails or is cancelled.</summary>
    private static ChannelReader<ConsoleCommand> ReadInBackground(
        Func<ChannelWriter<ConsoleCommand>, Task> read, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ConsoleCommand>();
        _ = Task.Run(
            async () =>
            {
                Exception? failure = null;
                try
                {
                    await read(channel.Writer).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    failure = e;
                }
                catch (OperationCanceledException)
                {
                    // The console is closing.
                }
                finally
                {
                    channel.Writer.TryComplete(failure);
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
        private int _height = -1;
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
            int height = Console.WindowHeight;
            if (width != _width || height != _height)
            {
                // First frame, or the window was resized: start from a clean window.
                Console.Clear();
                (_width, _height, _previousLineCount) = (width, height, 0);
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
            var info = new StringInfo(line);
            return info.LengthInTextElements <= width
                ? line + new string(' ', width - info.LengthInTextElements)
                : info.SubstringByTextElements(0, width);
        }
    }
}
