using Microsoft.Extensions.Options;

namespace EdgeHub.Calendar;

/// <summary>What <c>/api/calendar/today</c> answers: the day's events and the sport session the screen shows.</summary>
public sealed record CalendarTodayMessage(
    bool Configured,
    bool Available,
    IReadOnlyList<CalendarEventMessage> Events,
    SportMessage? Sport,
    string? CancelledTitle)
{
    public static CalendarTodayMessage NotConfigured { get; } = new(false, false, [], null, null);

    public static CalendarTodayMessage Unavailable { get; } = new(true, false, [], null, null);
}

public sealed record CalendarEventMessage(string Title, DateTimeOffset Start, DateTimeOffset End, bool AllDay);

public sealed record SportMessage(string Kind, DateTimeOffset Start, string Title);

/// <summary>Builds today's message: "today" and "now" are read in the calendar's time zone.</summary>
public sealed class CalendarToday(ICalendarSource source, IOptionsMonitor<CalendarOptions> options, TimeProvider time)
{
    public async Task<CalendarTodayMessage> GetAsync(CancellationToken cancellationToken)
    {
        if (!source.IsConfigured)
        {
            return CalendarTodayMessage.NotConfigured;
        }

        CalendarOptions current = options.CurrentValue;
        DateTimeOffset now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(current.TimeZone));

        IReadOnlyList<CalendarEvent> events;
        try
        {
            events = await source.GetEventsAsync(DateOnly.FromDateTime(now.DateTime), cancellationToken).ConfigureAwait(false);
        }
        catch (CalendarUnavailableException)
        {
            return CalendarTodayMessage.Unavailable;
        }

        SportDay sport = new SportDetector(current.EffectiveSport).Pick(events, now);
        return new CalendarTodayMessage(
            Configured: true,
            Available: true,
            events.Select(e => new CalendarEventMessage(e.Title, e.Start, e.End, e.IsAllDay)).ToList(),
            sport.Session is { } session ? new SportMessage(session.Kind, session.Start, session.Title) : null,
            sport.CancelledTitle);
    }
}
