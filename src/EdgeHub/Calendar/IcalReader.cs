using Ical.Net;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace EdgeHub.Calendar;

/// <summary>One day of a calendar, and how many series could not be evaluated for it.</summary>
public sealed record DayEvents(IReadOnlyList<CalendarEvent> Events, int SkippedSeries);

/// <summary>
/// Reads an iCal calendar series by series (one series = the events sharing a UID: the recurring event and its moved
/// or cancelled occurrences). A series that cannot be read or evaluated (unknown time zone, rule that never matches,
/// garbled lines) is skipped: it never hides the rest of the calendar.
/// </summary>
public static class IcalReader
{
    /// <summary>
    /// Parses a downloaded calendar once. Throws <see cref="FormatException"/> when the text is not a whole iCal
    /// calendar (an HTML page, a truncated download) or when none of its events can be read.
    /// </summary>
    public static ParsedCalendar Load(string ics)
    {
        ArgumentNullException.ThrowIfNull(ics);
        string[] lines = ics.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (!lines.Any(l => IsLine(l, "BEGIN:VCALENDAR")) || !lines.Any(l => IsLine(l, "END:VCALENDAR")))
        {
            throw new FormatException("The text is not a whole iCal calendar.");
        }

        var timeZones = new List<string>();
        var events = new List<(string Uid, string Block)>();
        for (int i = 0; i < lines.Length; i++)
        {
            bool isEvent = IsLine(lines[i], "BEGIN:VEVENT");
            if (!isEvent && !IsLine(lines[i], "BEGIN:VTIMEZONE"))
            {
                continue;
            }

            int end = EndOfBlock(lines, i);
            if (end < 0)
            {
                break;
            }

            string[] block = lines[i..(end + 1)];
            if (isEvent)
            {
                events.Add((UidOf(block) ?? $"no-uid-{events.Count}", string.Join('\n', block)));
            }
            else
            {
                timeZones.Add(string.Join('\n', block));
            }

            i = end;
        }

        var series = new List<IcalCalendar>();
        int unreadable = 0;
        foreach (var group in events.GroupBy(e => e.Uid, StringComparer.Ordinal))
        {
            string text = "BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Edge Hub//Series//EN\n"
                + string.Join('\n', timeZones.Concat(group.Select(e => e.Block)))
                + "\nEND:VCALENDAR\n";
            try
            {
                if (IcalCalendar.Load(text) is { } calendar)
                {
                    series.Add(calendar);
                }
                else
                {
                    unreadable++;
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                unreadable++;
            }
        }

        if (events.Count > 0 && series.Count == 0)
        {
            throw new FormatException("None of the calendar's events could be read.");
        }

        return new ParsedCalendar(series, unreadable);
    }

    /// <summary>Convenience for one-off reads: parses the text and returns the day's events.</summary>
    public static IReadOnlyList<CalendarEvent> EventsOn(string ics, DateOnly day, string timeZoneId) =>
        Load(ics).EventsOn(day, timeZoneId).Events;

    private static bool IsLine(string line, string expected) =>
        line.TrimEnd().Equals(expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>The index of the line that closes the block opened at <paramref name="start"/>, or -1 when it is cut off.</summary>
    private static int EndOfBlock(string[] lines, int start)
    {
        int depth = 0;
        for (int i = start; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
            else if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase) && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The block's UID, after unfolding continuation lines (a long UID is split over several lines).</summary>
    private static string? UidOf(string[] block)
    {
        var unfolded = new List<string>();
        foreach (string line in block)
        {
            if (unfolded.Count > 0 && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                unfolded[^1] += line[1..];
            }
            else
            {
                unfolded.Add(line);
            }
        }

        string? uid = unfolded.FirstOrDefault(l =>
            l.StartsWith("UID:", StringComparison.OrdinalIgnoreCase) || l.StartsWith("UID;", StringComparison.OrdinalIgnoreCase));
        return uid?[(uid.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
    }
}

/// <summary>A calendar parsed once per download, split into independent series. Not thread-safe: evaluate under a lock.</summary>
public sealed class ParsedCalendar
{
    // Stops a rule that never matches (30 February) after a bounded number of empty steps instead of searching for years.
    private static readonly EvaluationOptions Bounded = new() { MaxUnmatchedIncrementsLimit = 1000 };

    private readonly IReadOnlyList<IcalCalendar> _series;

    internal ParsedCalendar(IReadOnlyList<IcalCalendar> series, int unreadableSeries)
    {
        _series = series;
        UnreadableSeries = unreadableSeries;
    }

    /// <summary>Series that could not even be parsed, and are left out of every day.</summary>
    public int UnreadableSeries { get; }

    /// <summary>The day's events in the zone, ordered by start; a series that fails to evaluate is skipped and counted.</summary>
    public DayEvents EventsOn(DateOnly day, string timeZoneId)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = new CalDateTime(day, TimeOnly.MinValue, timeZoneId);
        var end = new CalDateTime(day.AddDays(1), TimeOnly.MinValue, timeZoneId);

        var events = new List<CalendarEvent>();
        int skipped = 0;
        foreach (IcalCalendar series in _series)
        {
            try
            {
                events.AddRange(Occurrences(series, start, end, zone));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                skipped++;
            }
        }

        return new DayEvents(events.OrderBy(e => e.Start).ToList(), skipped);
    }

    private static List<CalendarEvent> Occurrences(IcalCalendar series, CalDateTime start, CalDateTime end, TimeZoneInfo zone) =>
        series.GetOccurrences(start, Bounded)
            .TakeWhileBefore(end)
            .Select(occurrence => (Event: occurrence.Source as IcalEvent, occurrence.Period))
            .Where(x => x.Event is not null && !string.Equals(x.Event.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Select(x => new CalendarEvent(
                x.Event!.Summary ?? "",
                InZone(x.Period.StartTime, zone),
                InZone(x.Period.EffectiveEndTime ?? x.Period.StartTime, zone),
                IsAllDay: x.Event.IsAllDay || !x.Period.StartTime.HasTime))
            .ToList();

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
