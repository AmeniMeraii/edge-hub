using EdgeHub.Streaming;
using EdgeHub.SystemStats;

namespace EdgeHub.Tests.Streaming;

public sealed class LiveStateHubTests
{
    private static LiveState State(double cpu) => new(new SystemSnapshot(cpu, 0, 0, 0, 0, null, []), Media: null);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
}
