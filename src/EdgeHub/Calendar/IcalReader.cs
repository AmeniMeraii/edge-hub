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

        // Time zones are read by name (Ical.Net does not use the VTIMEZONE blocks), so only the events are kept.
        var events = new List<(string Uid, string Block)>();
        int unreadable = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!IsLine(lines[i], "BEGIN:VEVENT"))
            {
                continue;
            }

            int end = EndOfEvent(lines, i);
            if (end < 0)
            {
                // Never closed: drop it and carry on from the line that interrupted it.
                unreadable++;
                i = Interruption(lines, i) - 1;
                continue;
            }

            string[] block = lines[i..(end + 1)];
            if (RepeatsTooOften(block))
            {
                unreadable++;
            }
            else
            {
                events.Add((UidOf(block) ?? $"no-uid-{events.Count}", string.Join('\n', block)));
            }

            i = end;
        }

        var series = new List<IcalCalendar>();
        foreach (var group in events.GroupBy(e => e.Uid, StringComparer.Ordinal))
        {
            string text = "BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Edge Hub//Series//EN\n"
                + string.Join('\n', group.Select(e => e.Block))
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

    /// <summary>
    /// The index of the line that closes the event opened at <paramref name="start"/>, or -1 when it is never closed:
    /// cut off, or interrupted by another event or by the end of the calendar.
    /// </summary>
    private static int EndOfEvent(string[] lines, int start)
    {
        int depth = 0;
        for (int i = start; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (i > start && (IsLine(line, "BEGIN:VEVENT") || IsLine(line, "END:VCALENDAR")))
            {
                return -1;
            }

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

    /// <summary>The line after <paramref name="start"/> where an unclosed event is interrupted (or the end of the text).</summary>
    private static int Interruption(string[] lines, int start)
    {
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (IsLine(lines[i], "BEGIN:VEVENT") || IsLine(lines[i], "END:VCALENDAR"))
            {
                return i;
            }
        }

        return lines.Length;
    }

    /// <summary>
    /// Rules repeating every hour or faster cannot be made in Google Calendar, and unfolding one started years ago takes
    /// minutes: such series are left out.
    /// </summary>
    private static bool RepeatsTooOften(string[] block) =>
        Unfolded(block).Any(line => line.StartsWith("RRULE", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("FREQ=HOURLY", StringComparison.OrdinalIgnoreCase)
                || line.Contains("FREQ=MINUTELY", StringComparison.OrdinalIgnoreCase)
                || line.Contains("FREQ=SECONDLY", StringComparison.OrdinalIgnoreCase)));

    /// <summary>The event's own UID: a reminder (VALARM) nested inside it can carry another one.</summary>
    private static string? UidOf(string[] block)
    {
        int depth = 0;
        foreach (string line in Unfolded(block))
        {
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
            else if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                depth--;
            }
            else if (depth == 1
                && (line.StartsWith("UID:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("UID;", StringComparison.OrdinalIgnoreCase)))
            {
                return line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
            }
        }

        return null;
    }

    /// <summary>The block's lines with continuation lines (starting with a space or a tab) joined back.</summary>
    private static List<string> Unfolded(string[] block)
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

        return unfolded;
    }
}

/// <summary>A calendar parsed once per download, split into independent series. Not thread-safe: evaluate under a lock.</summary>
public sealed class ParsedCalendar
{
    // Stops a rule that never matches (30 February) after a bounded number of empty steps instead of searching for years.
    // A contrived rule whose matches are very far apart (DAILY on 29 February) is skipped too; Google does not make those.
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
