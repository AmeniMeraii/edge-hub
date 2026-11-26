using System.Text.RegularExpressions;

namespace EdgeHub.Metrics;

/// <summary>Days as the PC lives them, and tickets as people write them.</summary>
internal static partial class LocalDays
{
    /// <summary>Midnight at the start of the day, in the zone: a day lasts 23 or 25 hours when the clocks change.</summary>
    public static DateTimeOffset StartOf(DateOnly day, TimeZoneInfo zone)
    {
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }

    /// <summary>
    /// The ticket of a task or a report: the first word shaped like a ticket (« DEMO-101 », « phase-0 »), wherever it
    /// is (« [DEMO-101] Export », « DEMO-101: export »); else the first word without its punctuation.
    /// </summary>
    public static string TicketOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        if (TicketShaped().Match(text) is { Success: true } ticket)
        {
            return ticket.Value;
        }

        string first = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return first.Trim('[', ']', '(', ')', ':', ',', ';', '.', '!', '?', '«', '»', '"', '\'', '*');
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}_-])\p{L}[\p{L}\p{N}_]*-\d+(?![\p{L}\p{N}_-])")]
    private static partial Regex TicketShaped();
}
