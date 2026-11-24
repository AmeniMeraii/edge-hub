using Ical.Net;
using Ical.Net.DataTypes;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace EdgeHub.Calendar;

/// <summary>
/// Reads one day of an iCal calendar: recurring events are unfolded, with their exceptions (excluded dates, moved or
/// cancelled occurrences), and every time is given in the calendar's time zone.
/// </summary>
public static class IcalReader
{
    public static IReadOnlyList<CalendarEvent> EventsOn(string ics, DateOnly day, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(ics);
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        IcalCalendar calendar = IcalCalendar.Load(ics)
            ?? throw new FormatException("The iCal text holds no calendar.");

        var start = new CalDateTime(day, TimeOnly.MinValue, timeZoneId);
        var end = new CalDateTime(day.AddDays(1), TimeOnly.MinValue, timeZoneId);

        return calendar.GetOccurrences(start, options: null)
            .TakeWhileBefore(end)
            .Select(occurrence => (Event: occurrence.Source as IcalEvent, occurrence.Period))
            .Where(x => x.Event is not null && !string.Equals(x.Event.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Select(x => new CalendarEvent(
                x.Event!.Summary ?? "",
                InZone(x.Period.StartTime, zone),
                InZone(x.Period.EffectiveEndTime ?? x.Period.StartTime, zone),
                IsAllDay: x.Event.IsAllDay || !x.Period.StartTime.HasTime))
            .OrderBy(e => e.Start)
            .ToList();
    }

    /// <summary>A date (all-day) is midnight in the zone; a floating time is read in the zone; anything else is converted.</summary>
    private static DateTimeOffset InZone(CalDateTime time, TimeZoneInfo zone)
    {
        if (!time.HasTime || time.IsFloating)
        {
            DateTime local = time.HasTime ? time.Value : time.Date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        return TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(time.AsUtc, DateTimeKind.Utc)), zone);
    }
}
