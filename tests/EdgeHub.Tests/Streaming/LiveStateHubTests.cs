using EdgeHub.Streaming;
using EdgeHub.SystemStats;

namespace EdgeHub.Tests.Streaming;

public sealed class LiveStateHubTests
{
    private static LiveState State(double cpu) => new(new SystemSnapshot(cpu, 0, 0, 0, 0, null, []), Media: null);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A state that never comes fails the test instead of hanging it.</summary>
    private static Task<bool> Soon(ValueTask<bool> next) => next.AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);

    [Fact]
    public async Task A_new_subscriber_first_receives_the_latest_state()
    {
        var hub = new LiveStateHub();
        LiveState latest = State(cpu: 42);
        hub.Publish(State(cpu: 1));
        hub.Publish(latest);

        await using var subscription = hub.Subscribe(Ct).GetAsyncEnumerator(Ct);

        Assert.True(await subscription.MoveNextAsync());
        Assert.Same(latest, subscription.Current);
    }

    [Fact]
    public async Task Every_subscriber_receives_each_new_state()
    {
        var hub = new LiveStateHub();
        await using var first = hub.Subscribe(Ct).GetAsyncEnumerator(Ct);
        await using var second = hub.Subscribe(Ct).GetAsyncEnumerator(Ct);
        ValueTask<bool> firstNext = first.MoveNextAsync();
        ValueTask<bool> secondNext = second.MoveNextAsync();

        LiveState published = State(cpu: 7);
        hub.Publish(published);

        Assert.True(await firstNext);
        Assert.True(await secondNext);
        Assert.Same(published, first.Current);
        Assert.Same(published, second.Current);
    }

    [Fact]
    public async Task A_slow_subscriber_skips_straight_to_the_newest_state()
    {
        var hub = new LiveStateHub();
        await using var slow = hub.Subscribe(Ct).GetAsyncEnumerator(Ct);
        ValueTask<bool> pending = slow.MoveNextAsync();
        hub.Publish(State(cpu: 1));
        Assert.True(await pending);

        LiveState newest = State(cpu: 3);
        hub.Publish(State(cpu: 2));
        hub.Publish(newest);

        Assert.True(await slow.MoveNextAsync());
        Assert.Same(newest, slow.Current);
    }

    [Fact]
    public async Task A_subscription_ends_and_is_removed_when_cancelled()
    {
        var hub = new LiveStateHub();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        hub.Publish(State(cpu: 1));

        await using (var subscription = hub.Subscribe(stop.Token).GetAsyncEnumerator(stop.Token))
        {
            Assert.True(await subscription.MoveNextAsync());
            Assert.Equal(1, hub.SubscriberCount);
            await stop.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await subscription.MoveNextAsync());
        }

        Assert.Equal(0, hub.SubscriberCount);
    }

    [Fact]
    public async Task Counts_the_screens_on_another_device()
    {
        var hub = new LiveStateHub();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        hub.Publish(State(cpu: 1));

        await using var onThePc = hub.Subscribe(fromAnotherDevice: false, stop.Token).GetAsyncEnumerator(stop.Token);
        Assert.True(await Soon(onThePc.MoveNextAsync()));
        Assert.Equal(0, hub.ScreensElsewhere);

        await using (var tablet = hub.Subscribe(fromAnotherDevice: true, stop.Token).GetAsyncEnumerator(stop.Token))
        {
            Assert.True(await Soon(tablet.MoveNextAsync()));
            Assert.Equal(1, hub.ScreensElsewhere);
        }

        Assert.Equal(0, hub.ScreensElsewhere);
        Assert.Equal(1, hub.SubscriberCount);
    }

    [Fact]
    public async Task A_nudge_sends_the_latest_state_again_so_that_screens_read_what_changed()
    {
        var hub = new LiveStateHub();
        await using var screen = hub.Subscribe(Ct).GetAsyncEnumerator(Ct);
        ValueTask<bool> next = screen.MoveNextAsync();

        hub.Nudge();
        Assert.True(await Soon(next));
        Assert.Equal(new LiveState(null, null), screen.Current);
        Assert.Null(hub.Latest);

        LiveState latest = State(cpu: 5);
        hub.Publish(latest);
        Assert.True(await Soon(screen.MoveNextAsync()));
        hub.Nudge();
        Assert.True(await Soon(screen.MoveNextAsync()));
        Assert.Same(latest, screen.Current);
    }
}
