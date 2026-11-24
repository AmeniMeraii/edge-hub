using Microsoft.Extensions.Options;

namespace EdgeHub.Calendar;

/// <summary>Where the day's events come from.</summary>
public interface ICalendarSource
{
    bool IsConfigured { get; }

    /// <summary>
    /// The day's events, ordered by start. Throws <see cref="CalendarUnavailableException"/> when no readable copy of the
    /// calendar has ever been downloaded from the configured link.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly day, CancellationToken cancellationToken);
}

public sealed class CalendarUnavailableException : Exception
{
    public CalendarUnavailableException()
        : base("The calendar could not be read.")
    {
    }

    public CalendarUnavailableException(string message)
        : base(message)
    {
    }

    public CalendarUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Downloads the iCal calendar at most once every 5 minutes and keeps the last readable copy: a failed download, or one
/// that is not a calendar (an HTML page from a proxy), never replaces it, and after a failure the next attempt waits a
/// minute. The calendar is parsed once per download, and each day is computed once per download.
/// The link is a secret: it is never logged (only the kind of failure is), and the HTTP client used for it logs nothing.
/// </summary>
public sealed partial class IcalCalendarSource(
    IHttpClientFactory clients,
    IOptionsMonitor<CalendarOptions> options,
    TimeProvider time,
    ILogger<IcalCalendarSource> logger) : ICalendarSource, IDisposable
{
    public const string ClientName = "calendar";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(DateOnly Day, string Zone), IReadOnlyList<CalendarEvent>> _days = [];
    private ParsedCalendar? _calendar;
    private string? _readFrom;
    private long _readAt;
    private string? _failedFor;
    private long _failedAt;

    public bool IsConfigured => options.CurrentValue.IsConfigured;

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly day, CancellationToken cancellationToken)
    {
        CalendarOptions current = options.CurrentValue;
        if (!current.IsConfigured)
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ParsedCalendar calendar = await CalendarAsync(current.IcalUrl!, cancellationToken).ConfigureAwait(false);
            if (_days.TryGetValue((day, current.TimeZone), out IReadOnlyList<CalendarEvent>? known))
            {
                return known;
            }

            DayEvents result = calendar.EventsOn(day, current.TimeZone);
            if (result.SkippedSeries > 0)
            {
                LogSkippedSeries(logger, result.SkippedSeries);
            }

            _days[(day, current.TimeZone)] = result.Events;
            return result.Events;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    /// <summary>The calendar to read: the kept copy while fresh, otherwise a new download if it is readable.</summary>
    private async Task<ParsedCalendar> CalendarAsync(string link, CancellationToken cancellationToken)
    {
        bool sameLink = _readFrom == link;
        if (sameLink && _calendar is not null && time.GetElapsedTime(_readAt) < CacheFor)
        {
            return _calendar;
        }

        if (_failedFor == link && time.GetElapsedTime(_failedAt) < RetryAfterFailure)
        {
            return KeptOrUnavailable(link);
        }

        try
        {
            using HttpClient client = clients.CreateClient(ClientName);
            using HttpResponseMessage response = await client.GetAsync(link, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogDownloadFailed(logger, (int)response.StatusCode);
                return Failed(link);
            }

            ParsedCalendar parsed = IcalReader.Load(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (parsed.UnreadableSeries > 0)
            {
                LogSkippedSeries(logger, parsed.UnreadableSeries);
            }

            (_calendar, _readFrom, _readAt, _failedFor) = (parsed, link, time.GetTimestamp(), null);
            _days.Clear();
            return parsed;
        }
        catch (FormatException)
        {
            LogNotACalendar(logger);
            return Failed(link);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the kind of failure is logged: exception messages can carry parts of the address.
            LogUnreachable(logger, e.GetType().Name);
            return Failed(link);
        }
    }

    private ParsedCalendar Failed(string link)
    {
        (_failedFor, _failedAt) = (link, time.GetTimestamp());
        return KeptOrUnavailable(link);
    }

    private ParsedCalendar KeptOrUnavailable(string link) =>
        _calendar is not null && _readFrom == link ? _calendar : throw new CalendarUnavailableException();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Téléchargement de l'agenda refusé (statut HTTP {StatusCode}) : dernière version gardée s'il y en a une")]
    private static partial void LogDownloadFailed(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agenda injoignable ({Failure}) : dernière version gardée s'il y en a une")]
    private static partial void LogUnreachable(ILogger logger, string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Le téléchargement de l'agenda n'est pas un agenda iCal : dernière version gardée s'il y en a une")]
    private static partial void LogNotACalendar(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agenda : {Count} série(s) d'événements illisible(s), ignorée(s)")]
    private static partial void LogSkippedSeries(ILogger logger, int count);
}
