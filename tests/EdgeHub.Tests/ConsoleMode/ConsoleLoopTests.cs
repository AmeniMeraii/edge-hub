using System.Threading.Channels;
using EdgeHub.ConsoleMode;
using EdgeHub.SystemStats;
using EdgeHub.Tests.Spotify;

namespace EdgeHub.Tests.ConsoleMode;

public sealed class ConsoleLoopTests
{
    private static ChannelReader<ConsoleCommand> Keys(params ConsoleCommand[] commands)
    {
        var channel = Channel.CreateUnbounded<ConsoleCommand>();
        foreach (ConsoleCommand command in commands)
        {
            channel.Writer.TryWrite(command);
        }

        return channel.Reader;
    }

    [Fact]
    public async Task Sends_each_command_without_taking_an_extra_sample()
    {
        var stats = new CountingSystemStats();
        var media = new FakeMediaController(accept: true);
        var frames = new List<IReadOnlyList<string>>();

        int exitCode = await ConsoleLoop.RunAsync(
            stats, media, TimeProvider.System,
            Keys(ConsoleCommand.Next, ConsoleCommand.Next, ConsoleCommand.Next, ConsoleCommand.Previous, ConsoleCommand.Quit),
            frames.Add, initialStatus: null, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(["next", "next", "next", "previous"], media.Commands);
        Assert.Equal(1, stats.SampleCount);
        Assert.Contains("Précédent envoyé", frames[^1]);
    }

    [Fact]
    public async Task Stops_on_quit_after_showing_one_frame()
    {
        var frames = new List<IReadOnlyList<string>>();

        await ConsoleLoop.RunAsync(
            new CountingSystemStats(), new FakeMediaController(accept: true), TimeProvider.System,
            Keys(ConsoleCommand.Quit), frames.Add, initialStatus: null, TestContext.Current.CancellationToken);

        Assert.Single(frames);
    }

    [Fact]
    public async Task Shows_the_initial_status_on_the_first_frame()
    {
        var frames = new List<IReadOnlyList<string>>();

        await ConsoleLoop.RunAsync(
            new CountingSystemStats(), new FakeMediaController(accept: true), TimeProvider.System,
            Keys(ConsoleCommand.Quit), frames.Add, initialStatus: "Contrôles média indisponibles", TestContext.Current.CancellationToken);

        Assert.Contains("Contrôles média indisponibles", frames[0]);
    }

    private sealed class CountingSystemStats : ISystemStats
    {
        private static readonly SystemSnapshot Snapshot = new(0, 0, 0, 0, 0, null, []);

        public int SampleCount { get; private set; }

        public SystemSnapshot? Latest => SampleCount == 0 ? null : Snapshot;

        public SystemSnapshot Sample()
        {
            SampleCount++;
            return Snapshot;
        }
    }
}
