using EdgeHub.Spotify;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Spotify;

public sealed class RetryingMediaControllerTests
{
    private static readonly MediaState Track = new("Spotify.exe", "Demo Song", "Demo Artist", "", IsPlaying: false, TimeSpan.Zero, TimeSpan.FromMinutes(3));

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));
    private int _attempts;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Func<CancellationToken, Task<IMediaController>> Creator(int failures, IMediaController controller) => _ =>
    {
        _attempts++;
        return _attempts <= failures
            ? Task.FromException<IMediaController>(new InvalidOperationException("Media controls unavailable."))
            : Task.FromResult(controller);
    };

    [Fact]
    public async Task Creates_the_controller_once_and_forwards_every_call_to_it()
    {
        var inner = new FakeMediaController(accept: true) { State = Track };
        using var media = new RetryingMediaController(Creator(failures: 0, inner), _clock);

        Assert.Same(Track, await media.GetStateAsync(Ct));
        Assert.True(await media.NextAsync(Ct));

        Assert.Equal(1, _attempts);
        Assert.Equal(["next"], inner.Commands);
    }

    [Fact]
    public async Task A_creation_that_hangs_counts_as_a_failure_and_is_retried_later()
    {
        var inner = new FakeMediaController(accept: true) { State = Track };
        Func<CancellationToken, Task<IMediaController>> hangsThenWorks = async cancellationToken =>
        {
            if (++_attempts == 1)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return inner;
        };
        using var media = new RetryingMediaController(hangsThenWorks, _clock, creationLimit: TimeSpan.FromMilliseconds(100));

        Assert.Null(await media.GetStateAsync(Ct));
        _clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Same(Track, await media.GetStateAsync(Ct));
        Assert.Equal(2, _attempts);
    }

    [Fact]
    public async Task Without_media_controls_it_has_no_session_and_tries_again_after_thirty_seconds()
    {
        var inner = new FakeMediaController(accept: true) { State = Track };
        using var media = new RetryingMediaController(Creator(failures: 1, inner), _clock);

        Assert.Null(await media.GetStateAsync(Ct));
        Assert.False(await media.PlayPauseAsync(Ct));
        _clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Null(await media.GetStateAsync(Ct));
        Assert.Equal(1, _attempts);

        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Same(Track, await media.GetStateAsync(Ct));
        Assert.Equal(2, _attempts);
    }
}
