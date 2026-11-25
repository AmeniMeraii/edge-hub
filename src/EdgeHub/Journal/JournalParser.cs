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

        var daily = new List<DailyLine>();
        var followUps = new List<DecisionFollowUp>();
        var points = new List<TechnicalPoint>();
        var toRaise = new List<string>();
        var decisions = new List<MeetingDecision>();
        bool preparationFound = false;
        var zone = Zone.None;

        foreach (string line in text.Split('\n'))
        {
            if (Heading().Match(line) is { Success: true } heading)
            {
                zone = Next(zone, heading.Groups["marks"].Length, Normalize(heading.Groups["title"].Value), ref preparationFound);
                continue;
            }

            if (Bullet().Match(line) is not { Success: true } bullet)
            {
                continue;
            }

            string item = bullet.Groups["item"].Value.Trim();
            switch (zone)
            {
                case Zone.Daily:
                    daily.Add(ReadDailyLine(item));
                    break;
                case Zone.FollowUps:
                    followUps.Add(ReadFollowUp(item));
                    break;
                case Zone.TechnicalPoints:
                    points.Add(ReadTechnicalPoint(item));
                    break;
                case Zone.ToRaise:
                    toRaise.Add(WithoutBold(item));
                    break;
                case Zone.Decisions:
                    decisions.Add(ReadDecision(item));
                    break;
                default:
                    break;
            }
        }

        Preparation? preparation = preparationFound ? new Preparation(daily, followUps, points, toRaise) : null;
        return new JournalDay(preparation, decisions);
    }

    /// <summary>
    /// Where a heading leads. <c>/prep-reunions</c> writes its block at the top of the day, so the first preparation is
    /// the latest; an older one below it is skipped. The preparation ends at the first heading that is not one of its own.
    /// </summary>
    private static Zone Next(Zone zone, int level, string title, ref bool preparationFound)
    {
        bool inPreparation = zone is Zone.Preparation or Zone.Daily or Zone.Meeting or Zone.FollowUps or Zone.TechnicalPoints or Zone.ToRaise;
        bool inMeeting = zone is Zone.Meeting or Zone.FollowUps or Zone.TechnicalPoints or Zone.ToRaise;

        if (level == 1)
        {
            if (title.StartsWith("preparation", StringComparison.Ordinal) && !preparationFound)
            {
                preparationFound = true;
                return Zone.Preparation;
            }

            return Zone.None;
        }

        if (level == 2)
        {
            if (title.StartsWith("decisions de la reunion technique", StringComparison.Ordinal))
            {
                return Zone.Decisions;
            }

            if (inPreparation && title.StartsWith("daily", StringComparison.Ordinal))
            {
                return Zone.Daily;
            }

            return inPreparation && title.StartsWith("reunion technique", StringComparison.Ordinal) ? Zone.Meeting : Zone.None;
        }

        if (level == 3 && inMeeting)
        {
            if (title.StartsWith("suivi", StringComparison.Ordinal))
            {
                return Zone.FollowUps;
            }

            if (title.StartsWith("mes tickets", StringComparison.Ordinal))
            {
                return Zone.TechnicalPoints;
            }

            return title.StartsWith("a soulever", StringComparison.Ordinal) ? Zone.ToRaise : Zone.Meeting;
        }

        // A deeper heading stays in its section.
        return zone;
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

        string state = item[(arrow.Index + arrow.Length)..].Trim();
        string normalized = Normalize(state);
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

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s+(?<item>.*?)\s*$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(?<key>[^:]+?)\s*:\s*(?<value>.*)$")]
    private static partial Regex Field();

    [GeneratedRegex(@"(?:→|->|=>)(?!.*(?:→|->|=>))")]
    private static partial Regex LastArrow();

    [GeneratedRegex(@"^\*\*(?<ticket>.+?)\*\*\s*:?\s*(?<text>.*)$")]
    private static partial Regex LeadingBold();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
