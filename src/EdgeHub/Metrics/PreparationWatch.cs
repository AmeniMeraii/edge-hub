using EdgeHub.Journal;

namespace EdgeHub.Metrics;

/// <summary>
/// Notes, once a day, the first time the PC sees the day's preparation in the journal: it tells whether
/// <c>/prep-reunions</c> was run before the daily. It looks every minute, whether the screen is open or not.
/// </summary>
public sealed partial class PreparationWatch(JournalReader journal, EventLog events, TimeProvider time, ILogger<PreparationWatch> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    private DateOnly? _noted;
    private bool _firstLook = true;

    public async Task RecordOnceAsync(CancellationToken cancellationToken)
    {
        if (!events.IsEnabled)
        {
            return;
        }

        TimeZoneInfo zone = time.LocalTimeZone;
        DateTimeOffset now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        bool firstLook = _firstLook;
        _firstLook = false;
        if (_noted == today || (await journal.ReadAsync(today, cancellationToken).ConfigureAwait(false))?.Day.Preparation is null)
        {
            return;
        }

        // Noted already, before a restart of the service? Already there at the first look after a start: when it came
        // is unknown, and noting it now would count it late.
        bool noted = events.Read(LocalDays.StartOf(today, zone), LocalDays.StartOf(today.AddDays(1), zone)).Any(e => e.Type == MetricEventType.PreparationSeen);
        if (!noted && !firstLook)
        {
            events.Append(new MetricEvent(now, MetricEventType.PreparationSeen));
        }

        _noted = today;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(Period, time);
        do
        {
            try
            {
                await RecordOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
            {
                // An error that escaped would stop the whole service.
                LogFailed(logger, e.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Préparation du jour non vérifiée ({ErrorType}), nouvel essai dans une minute")]
    private static partial void LogFailed(ILogger logger, string errorType);
}
