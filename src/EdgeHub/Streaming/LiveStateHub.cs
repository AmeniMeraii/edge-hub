using System.Runtime.CompilerServices;
using System.Threading.Channels;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Streaming;

/// <summary>What the tablets see live: the PC's stats and the current track. Either can be missing.</summary>
public sealed record LiveState(SystemSnapshot? Stats, MediaState? Media);

/// <summary>
/// Hands every published state to every connected screen. Each screen has its own one-slot queue: a slow screen never
/// holds up the others, it simply skips to the newest state.
/// </summary>
public sealed class LiveStateHub
{
    private readonly Lock _gate = new();
    private readonly List<Channel<LiveState>> _subscribers = [];
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

    public void Publish(LiveState state)
    {
        lock (_gate)
        {
            _latest = state;
            foreach (Channel<LiveState> subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(state);
            }
        }
    }

    /// <summary>The latest state first, then each new one, until the token is cancelled.</summary>
    public async IAsyncEnumerable<LiveState> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<LiveState>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        lock (_gate)
        {
            _subscribers.Add(channel);
            if (_latest is not null)
            {
                channel.Writer.TryWrite(_latest);
            }
        }

        try
        {
            await foreach (LiveState state in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return state;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
