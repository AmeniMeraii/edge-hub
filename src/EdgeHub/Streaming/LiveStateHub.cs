using System.Runtime.CompilerServices;
using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Streaming;

/// <summary>What the tablets see live: the PC's stats and the current track. Either can be missing.</summary>
public sealed record LiveState(SystemSnapshot? Stats, MediaState? Media);

/// <summary>
/// Hands every published state to every connected screen. Each screen has its own one-slot queue: a slow screen never
/// holds up the others, it simply skips to the newest state. It also knows how many screens are on another device than
/// the PC: Claude Code's requests wait for a tablet only when one is there.
/// </summary>
public sealed class LiveStateHub
{
    private readonly Lock _gate = new();
    private readonly List<Subscriber> _subscribers = [];
    private LiveState? _latest;

    public LiveState? Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public int SubscriberCount
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count;
            }
        }
    }

    /// <summary>The screens connected from another device than the PC.</summary>
    public int ScreensElsewhere
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count(s => s.FromAnotherDevice);
            }
        }
    }

    public void Publish(LiveState state)
    {
        lock (_gate)
        {
            _latest = state;
            foreach (Subscriber subscriber in _subscribers)
            {
                subscriber.Channel.Writer.TryWrite(state);
            }
        }
    }

    /// <summary>
    /// Sends the latest state again (an empty one before the first), so that every screen sends a new message at once:
    /// what goes with the state, such as Claude Code's cards, is read when the message is made.
    /// </summary>
    public void Nudge()
    {
        lock (_gate)
        {
            LiveState state = _latest ?? new LiveState(null, null);
            foreach (Subscriber subscriber in _subscribers)
            {
                subscriber.Channel.Writer.TryWrite(state);
            }
        }
    }

    /// <summary>The latest state first, then each new one, until the token is cancelled.</summary>
    public IAsyncEnumerable<LiveState> Subscribe(CancellationToken cancellationToken) => Subscribe(fromAnotherDevice: false, cancellationToken);

    /// <inheritdoc cref="Subscribe(CancellationToken)"/>
    public async IAsyncEnumerable<LiveState> Subscribe(bool fromAnotherDevice, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriber = new Subscriber(
            Channel.CreateBounded<LiveState>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            }),
            fromAnotherDevice);

        lock (_gate)
        {
            _subscribers.Add(subscriber);
            if (_latest is not null)
            {
                subscriber.Channel.Writer.TryWrite(_latest);
            }
        }

        try
        {
            await foreach (LiveState state in subscriber.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return state;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(subscriber);
            }
        }
    }

    private sealed record Subscriber(Channel<LiveState> Channel, bool FromAnotherDevice);
}
