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

    /// <summary>The ticket of a report: its first word, without the punctuation around it (« DEMO-101: » → DEMO-101).</summary>
    public static string TicketOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        string first = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return first.Trim('[', ']', '(', ')', ':', ',', ';', '.', '!', '?', '«', '»', '"', '\'', '*');
    }

    /// <summary>
    /// The word appears whole in the text, whatever the case: « DEMO-101 » is in « [DEMO-101] Export » and in
    /// « feat/DEMO-101-export », not in « DEMO-1010 »; « phase-0 » is not in « phase-01 ».
    /// </summary>
    public static bool ContainsWord(string text, string word) =>
        word.Length > 0
        && Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
