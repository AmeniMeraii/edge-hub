using System.Text.RegularExpressions;

namespace EdgeHub.Calendar;

/// <summary>One occurrence on the calendar, in the calendar's time zone.</summary>
public sealed record CalendarEvent(string Title, DateTimeOffset Start, DateTimeOffset End, bool IsAllDay);

/// <summary>The session the screen shows: its kind (Course, Muscu…), its start and its calendar title.</summary>
public sealed record SportSession(string Kind, DateTimeOffset Start, string Title);

/// <summary>
/// The day's sport: the retained session, or none. With none, <see cref="CancelledTitle"/> is the title of a cancelled
/// event (its leading marker removed), for « Pas de séance aujourd'hui : … ».
/// </summary>
public sealed record SportDay(SportSession? Session, string? CancelledTitle);

/// <summary>
/// Reads sport sessions from calendar titles, as the original screen did (<c>kindOf</c>, <c>isCancelled</c>,
/// <c>calSport</c>), with the keywords taken from the settings.
/// </summary>
public sealed class SportDetector
{
    private readonly IReadOnlyList<(string Kind, IReadOnlyList<Func<string, bool>> Matches)> _kinds;
    private readonly IReadOnlyList<string> _cancelledMarkers;
    private readonly IReadOnlyList<string> _cancelledWords;

    public SportDetector(SportRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _kinds = rules.Kinds
            .Select(kind => (kind.Name, (IReadOnlyList<Func<string, bool>>)kind.Keywords.Select(Matcher).ToList()))
            .ToList();
        _cancelledMarkers = rules.CancelledMarkers;
        _cancelledWords = rules.CancelledWords;
    }

    /// <summary>The kind of session a title names (the first kind whose keyword it contains), or null.</summary>
    public string? KindOf(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return _kinds.FirstOrDefault(kind => kind.Matches.Any(matches => matches(title))).Kind;
    }

    /// <summary>A title that starts with a cancel marker (❌) or contains a cancel word (« annul »).</summary>
    public bool IsCancelled(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        string trimmed = title.TrimStart();
        return _cancelledMarkers.Any(marker => trimmed.StartsWith(marker, StringComparison.Ordinal))
            || _cancelledWords.Any(word => title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The session to show among the day's timed events: the first one not finished at <paramref name="now"/>,
    /// otherwise the last one of the day. Cancelled events are never retained.
    /// </summary>
    public SportDay Pick(IReadOnlyList<CalendarEvent> events, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        var timed = events.Where(e => !e.IsAllDay).OrderBy(e => e.Start).ToList();
        var sessions = timed
            .Where(e => !IsCancelled(e.Title))
            .Select(e => (Event: e, Kind: KindOf(e.Title)))
            .Where(x => x.Kind is not null)
            .ToList();

        if (sessions.Count > 0)
        {
            var (chosen, kind) = sessions.FirstOrDefault(x => x.Event.End > now) is { Event: not null } running
                ? running
                : sessions[^1];
            return new SportDay(new SportSession(kind!, chosen.Start, chosen.Title), CancelledTitle: null);
        }

        CalendarEvent? cancelled = timed.FirstOrDefault(e => IsCancelled(e.Title));
        return new SportDay(Session: null, cancelled is null ? null : WithoutMarker(cancelled.Title));
    }

    private string WithoutMarker(string title)
    {
        string trimmed = title.TrimStart();
        foreach (string marker in _cancelledMarkers)
        {
            if (trimmed.StartsWith(marker, StringComparison.Ordinal))
            {
                return trimmed[marker.Length..].TrimStart();
            }
        }

        return trimmed;
    }

    /// <summary>
    /// A keyword with letters matches a whole word, whatever the case (« run » does not match « Prune »); spaces and
    /// hyphens inside it are optional (« full body » matches « Full-body »). A keyword without letters, such as an
    /// emoji, matches anywhere.
    /// </summary>
    private static Func<string, bool> Matcher(string keyword)
    {
        if (!keyword.Any(char.IsLetterOrDigit))
        {
            return title => title.Contains(keyword, StringComparison.Ordinal);
        }

        string body = string.Join("[- ]?", keyword.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
        var pattern = new Regex($@"(?<!\w){body}(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return pattern.IsMatch;
    }
}
