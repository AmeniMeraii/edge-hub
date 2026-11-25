namespace EdgeHub.ClaudeSessions;

/// <summary>
/// Looks for the active session every 30 seconds, away from the requests: the first read of a session can go through
/// tens of megabytes. A failed pass is logged and keeps the last session found.
/// </summary>
public sealed partial class ClaudeSessionMonitor(IClaudeSessionReader reader, TimeProvider time, ILogger<ClaudeSessionMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    private volatile ActiveSession? _current;

    /// <summary>The session found by the last pass that worked.</summary>
    public ActiveSession? Current => _current;

    public void RefreshOnce()
    {
        try
        {
            _current = reader.FindActive();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // An error that escaped would stop the whole service, not just this screen line.
            LogPassFailed(logger, e.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Leaves the startup path before the first pass, which reads files.
        await Task.Yield();
        using var timer = new PeriodicTimer(Period, time);
        do
        {
            RefreshOnce();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recherche de la session Claude Code impossible ({ErrorType}), session précédente gardée")]
    private static partial void LogPassFailed(ILogger logger, string errorType);
}
