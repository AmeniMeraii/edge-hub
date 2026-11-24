using EdgeHub.Spotify;
using EdgeHub.Streaming;
using EdgeHub.SystemStats;
using EdgeHub.Tests.Spotify;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdgeHub.Tests.Streaming;

public sealed class LiveStateServiceTests
{
    private static readonly MediaState Track = new("Spotify.exe", "Demo Song", "Demo Artist", "Demo Album", IsPlaying: true, TimeSpan.FromSeconds(83), TimeSpan.FromSeconds(225));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publishes_the_stats_and_the_current_track()
    {
        var stats = new FakeSystemStats();
        var media = new FakeMediaController(accept: true) { State = Track };
        var hub = new LiveStateHub();
        var service = new LiveStateService(stats, media, hub, TimeProvider.System, NullLogger<LiveStateService>.Instance);

        await service.PublishOnceAsync(Ct);

        Assert.Same(stats.Snapshot, hub.Latest?.Stats);
        Assert.Same(Track, hub.Latest?.Media);
    }

    [Fact]
    public async Task A_failing_stats_source_does_not_stop_the_track_from_being_published()
    {
        var media = new FakeMediaController(accept: true) { State = Track };
        var hub = new LiveStateHub();
        var service = new LiveStateService(new ThrowingSystemStats(), media, hub, TimeProvider.System, NullLogger<LiveStateService>.Instance);

        await service.PublishOnceAsync(Ct);

        Assert.Null(hub.Latest?.Stats);
        Assert.Same(Track, hub.Latest?.Media);
    }

    private sealed class ThrowingSystemStats : ISystemStats
    {
        public SystemSnapshot? Latest => null;

        public SystemSnapshot Sample() => throw new InvalidOperationException("Simulated reading failure.");
    }
}
