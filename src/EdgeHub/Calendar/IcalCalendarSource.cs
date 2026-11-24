using Microsoft.Extensions.Options;

namespace EdgeHub.Calendar;

/// <summary>Where the day's events come from.</summary>
public interface ICalendarSource
{
    bool IsConfigured { get; }

    /// <summary>
    /// The day's events, ordered by start. Throws <see cref="CalendarUnavailableException"/> when the calendar cannot be
    /// read and was never read before.
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
/// Downloads the iCal calendar at most once every 5 minutes. When a download fails, the last calendar read is kept.
/// The link is a secret: it is never logged, and the HTTP client used for it logs nothing either.
/// </summary>
public sealed partial class IcalCalendarSource(
    IHttpClientFactory clients,
    IOptionsMonitor<CalendarOptions> options,
    TimeProvider time,
    ILogger<IcalCalendarSource> logger) : ICalendarSource, IDisposable
{
    public const string ClientName = "calendar";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _text;
    private string? _readFrom;
    private long _readAt;

    public bool IsConfigured => options.CurrentValue.IsConfigured;

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly day, CancellationToken cancellationToken)
    {
        CalendarOptions current = options.CurrentValue;
        if (!current.IsConfigured)
        {
            return [];
        }

        string ics = await TextAsync(current.IcalUrl!, cancellationToken).ConfigureAwait(false);
        try
        {
            return IcalReader.EventsOn(ics, day, current.TimeZone);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogUnreadable(logger, e.GetType().Name);
            throw new CalendarUnavailableException();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<string> TextAsync(string link, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool fresh = _text is not null && _readFrom == link && time.GetElapsedTime(_readAt) < CacheFor;
            if (fresh)
            {
                return _text!;
            }

            try
            {
                using HttpClient client = clients.CreateClient(ClientName);
                using HttpResponseMessage response = await client.GetAsync(link, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    LogDownloadFailed(logger, (int)response.StatusCode);
                    return KeptOrUnavailable(link);
                }

                _text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _readFrom = link;
                _readAt = time.GetTimestamp();
                return _text;
            }
            catch (Exception e) when (e is HttpRequestException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // Only the kind of failure is logged: exception messages can carry parts of the address.
                LogUnreachable(logger, e.GetType().Name);
                return KeptOrUnavailable(link);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private string KeptOrUnavailable(string link) =>
        _text is not null && _readFrom == link ? _text : throw new CalendarUnavailableException();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Téléchargement de l'agenda refusé (statut HTTP {StatusCode}) : dernière version gardée s'il y en a une")]
    private static partial void LogDownloadFailed(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agenda injoignable ({Failure}) : dernière version gardée s'il y en a une")]
    private static partial void LogUnreachable(ILogger logger, string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agenda illisible ({Failure})")]
    private static partial void LogUnreadable(ILogger logger, string failure);
}
