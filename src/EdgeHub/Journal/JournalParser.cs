using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EdgeHub.Journal;

/// <summary>
/// Reads the blocks of a journal day written by the Claude Code commands (<c>integrations/claude-code/commands/</c>):
/// the preparation of <c>/prep-reunions</c> and the decisions of <c>/decisions</c>. The blocks are written by a model, so
/// headings are matched by their start, whatever the case and the accents, and anything unknown is skipped: a missing
/// section gives an empty list, never an error.
/// </summary>
public static partial class JournalParser
{
    private enum Zone
    {
        None,
        Preparation,
        Daily,
        Meeting,
        FollowUps,
        TechnicalPoints,
        ToRaise,
        Decisions,
    }

    public static JournalDay Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // First the items of each zone: a nested bullet belongs to the item above it (« - **DEMO-101** » then
        // «   - Fait : … »), and nothing inside a code block or a separator counts.
        var items = new List<(Zone Zone, string Text)>();
        var cursor = new Cursor();
        bool inCode = false;
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                continue;
            }

            if (Heading().Match(line) is { Success: true } heading)
            {
                cursor.Enter(heading.Groups["marks"].Length, Plain(heading.Groups["title"].Value));
                continue;
            }

            if (Bullet().Match(line) is not { Success: true } bullet || Separator().IsMatch(bullet.Groups["item"].Value))
            {
                continue;
            }

            string item = bullet.Groups["item"].Value.Trim();
            bool nested = bullet.Groups["indent"].Value.Replace("\t", "    ", StringComparison.Ordinal).Length >= 2;
            if (nested && items.Count > 0 && items[^1].Zone == cursor.Zone)
            {
                items[^1] = (cursor.Zone, $"{items[^1].Text} · {item}");
            }
            else
            {
                items.Add((cursor.Zone, item));
            }
        }

        IEnumerable<string> In(Zone zone) => items.Where(i => i.Zone == zone).Select(i => i.Text);
        Preparation? preparation = cursor.PreparationFound
            ? new Preparation(
                In(Zone.Daily).Select(ReadDailyLine).ToList(),
                In(Zone.FollowUps).Select(ReadFollowUp).ToList(),
                In(Zone.TechnicalPoints).Select(ReadTechnicalPoint).ToList(),
                In(Zone.ToRaise).Select(WithoutBold).ToList())
            : null;
        return new JournalDay(preparation, In(Zone.Decisions).Select(ReadDecision).ToList());
    }

    /// <summary>
    /// Follows the headings. <c>/prep-reunions</c> writes its block at the top of the day, so the first preparation is
    /// the latest; an older one below it is skipped. The preparation may be a <c>#</c> or a <c>##</c> heading, its
    /// sections any deeper level; it ends at the next heading of its own level, or at an unknown section.
    /// </summary>
    private sealed class Cursor
    {
        private int _preparationLevel;
        private int _meetingLevel;

        public Zone Zone { get; private set; }

        public bool PreparationFound { get; private set; }

        private bool InPreparation => Zone is Zone.Preparation or Zone.Daily or Zone.Meeting or Zone.FollowUps or Zone.TechnicalPoints or Zone.ToRaise;

        private bool InMeeting => Zone is Zone.Meeting or Zone.FollowUps or Zone.TechnicalPoints or Zone.ToRaise;

        public void Enter(int level, string title)
        {
            if (title.StartsWith("preparation", StringComparison.Ordinal))
            {
                bool first = !PreparationFound && level <= 2;
                PreparationFound |= first;
                _preparationLevel = first ? level : _preparationLevel;
                Zone = first ? Zone.Preparation : Zone.None;
            }
            else if (title.StartsWith("decisions de la reunion technique", StringComparison.Ordinal))
            {
                Zone = Zone.Decisions;
            }
            else if (!InPreparation || level <= _preparationLevel)
            {
                Zone = Zone.None;
            }
            else if (title.StartsWith("daily", StringComparison.Ordinal))
            {
                Zone = Zone.Daily;
            }
            else if (title.StartsWith("reunion technique", StringComparison.Ordinal))
            {
                Zone = Zone.Meeting;
                _meetingLevel = level;
            }
            else if (InMeeting && level > _meetingLevel)
            {
                Zone = title.StartsWith("suivi", StringComparison.Ordinal) ? Zone.FollowUps
                    : title.Contains("soulever", StringComparison.Ordinal) ? Zone.ToRaise
                    : title.StartsWith("mes tickets", StringComparison.Ordinal) || title.Contains("technique", StringComparison.Ordinal) ? Zone.TechnicalPoints
                    : Zone.Meeting;
            }
            else if (level == _preparationLevel + 1)
            {
                // Another section at the preparation's section level, such as a report: the preparation is over.
                Zone = Zone.None;
            }

            // A deeper unknown heading stays in its section.
        }
    }

    private static DailyLine ReadDailyLine(string item)
    {
        (string head, Dictionary<string, string> fields) = Fields(item);
        string[] words = head.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new DailyLine(
            words.Length > 0 ? words[0] : "",
            words.Length > 1 ? words[1] : "",
            fields.GetValueOrDefault("fait"),
            fields.GetValueOrDefault("reste"),
            fields.GetValueOrDefault("testable"));
    }

    private static DecisionFollowUp ReadFollowUp(string item)
    {
        Match arrow = LastArrow().Match(item);
        if (!arrow.Success)
        {
            return new DecisionFollowUp(WithoutBold(item), DecisionStatus.Unknown, null);
        }

        string state = WithoutBold(item[(arrow.Index + arrow.Length)..]);
        string normalized = Plain(state);
        string status =
            normalized.StartsWith("appliqu", StringComparison.Ordinal) ? DecisionStatus.Applied
            : normalized.StartsWith("en cours", StringComparison.Ordinal) ? DecisionStatus.InProgress
            : normalized.StartsWith("pas commenc", StringComparison.Ordinal) || normalized.StartsWith("non commenc", StringComparison.Ordinal) ? DecisionStatus.NotStarted
            : DecisionStatus.Unknown;
        return new DecisionFollowUp(WithoutBold(item[..arrow.Index].Trim()), status, state.Length > 0 ? state : null);
    }

    private static TechnicalPoint ReadTechnicalPoint(string item)
    {
        Match bold = LeadingBold().Match(item);
        return bold.Success
            ? new TechnicalPoint(bold.Groups["ticket"].Value.Trim().TrimEnd(':').TrimEnd(), WithoutBold(bold.Groups["text"].Value.Trim()))
            : new TechnicalPoint(null, WithoutBold(item));
    }

    private static MeetingDecision ReadDecision(string item)
    {
        (string head, Dictionary<string, string> fields) = Fields(item);
        return new MeetingDecision(head, fields.GetValueOrDefault("qui"), fields.GetValueOrDefault("pour quand"));
    }

    /// <summary>
    /// Splits <c>head · Key : value · Key : value</c>. A part without a known key belongs to what comes before it, so a
    /// « · » inside a title or a value loses nothing.
    /// </summary>
    private static (string Head, Dictionary<string, string> Fields) Fields(string item)
    {
        string[] parts = WithoutBold(item).Split(['·', '•'], StringSplitOptions.TrimEntries);
        string head = parts[0];
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? last = null;
        foreach (string part in parts.Skip(1))
        {
            if (Field().Match(part) is { Success: true } field && Normalize(field.Groups["key"].Value) is ("fait" or "reste" or "testable" or "qui" or "pour quand") and var key)
            {
                fields[key] = field.Groups["value"].Value.Trim();
                last = key;
            }
            else if (last is null)
            {
                head = $"{head} · {part}";
            }
            else
            {
                fields[last] = $"{fields[last]} · {part}";
            }
        }

        return (head, fields);
    }

    /// <summary>Normalized, without the symbols a model may put first (« 📋 Daily », « ✅ appliquée »).</summary>
    private static string Plain(string text)
    {
        string normalized = Normalize(text);
        int start = 0;
        while (start < normalized.Length && !char.IsLetterOrDigit(normalized[start]))
        {
            start++;
        }

        return normalized[start..];
    }

    private static string WithoutBold(string text) => text.Replace("**", "", StringComparison.Ordinal).Replace("__", "", StringComparison.Ordinal).Trim();

    /// <summary>Lower case, without accents or extra spaces, for comparing headings, keys and states.</summary>
    private static string Normalize(string text)
    {
        var plain = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(char.IsWhiteSpace(c) ? ' ' : char.ToLowerInvariant(c));
            }
        }

        return Spaces().Replace(plain.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"^(?<marks>#{1,6})\s+(?<title>.*?)\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:[-*+]|\d+[.)])\s+(?<item>.*?)\s*$")]
    private static partial Regex Bullet();

    /// <summary>« * * * » or « - - - »: a horizontal rule, not an item.</summary>
    [GeneratedRegex(@"^[-*_\s]*$")]
    private static partial Regex Separator();

    [GeneratedRegex(@"^(?<key>[^:]+?)\s*:\s*(?<value>.*)$")]
    private static partial Regex Field();

    [GeneratedRegex(@"(?:→|->|=>)(?!.*(?:→|->|=>))")]
    private static partial Regex LastArrow();

    [GeneratedRegex(@"^\*\*(?<ticket>.+?)\*\*\s*:?\s*(?<text>.*)$")]
    private static partial Regex LeadingBold();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
