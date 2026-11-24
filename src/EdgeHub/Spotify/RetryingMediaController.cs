namespace EdgeHub.Spotify;

/// <summary>
/// Creates the real media controller on first use. If Windows' media controls cannot be reached (the service starts at
/// logon, sometimes before them), it answers "no session" and tries again 30 seconds later, instead of giving up.
/// Creation has a time limit (5 s by default): a creation that hangs counts as a failure.
/// </summary>
public sealed class RetryingMediaController(
    Func<CancellationToken, Task<IMediaController>> create,
    TimeProvider time,
    TimeSpan? creationLimit = null) : IMediaController, IDisposable
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _creationLimit = creationLimit ?? TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IMediaController? _inner;
    private long? _lastFailure;

    public async Task<MediaState?> GetStateAsync(CancellationToken cancellationToken) =>
        await InnerAsync(cancellationToken).ConfigureAwait(false) is { } inner
            ? await inner.GetStateAsync(cancellationToken).ConfigureAwait(false)
            : null;

    public async Task<bool> PlayPauseAsync(CancellationToken cancellationToken) =>
        await InnerAsync(cancellationToken).ConfigureAwait(false) is { } inner && await inner.PlayPauseAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> NextAsync(CancellationToken cancellationToken) =>
        await InnerAsync(cancellationToken).ConfigureAwait(false) is { } inner && await inner.NextAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> PreviousAsync(CancellationToken cancellationToken) =>
        await InnerAsync(cancellationToken).ConfigureAwait(false) is { } inner && await inner.PreviousAsync(cancellationToken).ConfigureAwait(false);

    public void Dispose()
    {
        _gate.Dispose();
        (_inner as IDisposable)?.Dispose();
    }

    private async Task<IMediaController?> InnerAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _inner) is { } ready)
        {
            return ready;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inner is not null)
            {
                return _inner;
            }

            if (_lastFailure is { } failedAt && time.GetElapsedTime(failedAt) < RetryAfter)
            {
                return null;
            }

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(_creationLimit);
            try
            {
                Volatile.Write(ref _inner, await create(limit.Token).ConfigureAwait(false));
                return _inner;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                _lastFailure = time.GetTimestamp();
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
