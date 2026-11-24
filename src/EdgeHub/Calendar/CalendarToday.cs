using Microsoft.Extensions.Options;

namespace EdgeHub.Calendar;

/// <summary>
/// What <c>/api/calendar/today</c> answers: the day it describes (in the calendar's time zone), its events and the sport
/// session the screen shows. The screen drops data whose day is not its own day, so a PC asleep at night never leaves
/// yesterday's session on the screen.
/// </summary>
public sealed record CalendarTodayMessage(
    bool Configured,
    bool Available,
    DateOnly? Day,
    IReadOnlyList<CalendarEventMessage> Events,
    SportMessage? Sport,
    string? CancelledTitle)
{
    public static CalendarTodayMessage NotConfigured { get; } = new(false, false, null, [], null, null);

    public static CalendarTodayMessage UnavailableOn(DateOnly day) => new(true, false, day, [], null, null);
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

        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        IReadOnlyList<CalendarEvent> events;
        try
        {
            events = await source.GetEventsAsync(today, cancellationToken).ConfigureAwait(false);
        }
        catch (CalendarUnavailableException)
        {
            return CalendarTodayMessage.UnavailableOn(today);
        }

        SportDay sport = new SportDetector(current.EffectiveSport).Pick(events, now);
        return new CalendarTodayMessage(
            Configured: true,
            Available: true,
            today,
            events.Select(e => new CalendarEventMessage(e.Title, e.Start, e.End, e.IsAllDay)).ToList(),
            sport.Session is { } session ? new SportMessage(session.Kind, session.Start, session.Title) : null,
            sport.CancelledTitle);
    }
}
