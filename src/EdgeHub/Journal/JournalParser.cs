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

        string[] lines = text.Split('\n');
        bool[] code = CodeLines(lines);
        var items = new List<Item>();
        var reports = new List<Report>();
        var cursor = new Cursor();

        // The item a more indented bullet belongs to; a heading starts afresh.
        Item? parent = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (code[i])
            {
                continue;
            }

            if (Heading().Match(lines[i]) is { Success: true } heading)
            {
                string title = heading.Groups["title"].Value.Trim();
                if (ReportHeading().Match(WithoutBold(title)) is { Success: true } report)
                {
                    reports.Add(new Report(report.Groups["time"].Value, report.Groups["ticket"].Value.Trim(), report.Groups["title"].Value.Trim()));
                }

                cursor.Enter(heading.Groups["marks"].Length, Plain(title));
                parent = null;
                if (cursor.HeadingIsItem)
                {
                    // A ticket written as a heading, its fields as the bullets below it.
                    parent = new Item(cursor.Zone, cursor.Zone == Zone.TechnicalPoints ? $"**{WithoutBold(title)}**" : WithoutBold(title), indent: -1, fromHeading: true);
                    items.Add(parent);
                }

                continue;
            }

            if (Bullet().Match(lines[i]) is not { Success: true } bullet || Separator().IsMatch(bullet.Groups["item"].Value))
            {
                continue;
            }

            string entry = bullet.Groups["item"].Value.Trim();
            int indent = bullet.Groups["indent"].Value.Replace("\t", "    ", StringComparison.Ordinal).Length;
            // Under a ticket written as a heading, only its fields join it: another ticket's line stays a line.
            bool joins = !(parent is { FromHeading: true, Zone: Zone.Daily } && !DailyField().IsMatch(entry));
            if (parent is not null && parent.Zone == cursor.Zone && indent > parent.Indent && joins)
            {
                if (cursor.Zone == Zone.ToRaise)
                {
                    // Each point to raise stays its own line; a line that only introduces them (« Questions : ») goes.
                    parent.HasChildren = true;
                    items.Add(new Item(cursor.Zone, entry, indent, fromHeading: false));
                }
                else
                {
                    // A field written as a sub-bullet (« - **DEMO-101** » then «   - Fait : … ») belongs to its ticket.
                    string separator = parent.Zone == Zone.TechnicalPoints && parent.FromHeading && !parent.HasChildren ? " : " : " · ";
                    parent.Text = $"{parent.Text}{separator}{entry}";
                    parent.HasChildren = true;
                }

                continue;
            }

            parent = new Item(cursor.Zone, entry, indent, fromHeading: false);
            items.Add(parent);
        }

        IEnumerable<string> In(Zone zone) => items
            .Where(i => i.Zone == zone && !(zone == Zone.ToRaise && i.HasChildren && i.Text.TrimEnd().EndsWith(':')))
            .Select(i => i.Text);
        Preparation? preparation = cursor.PreparationFound
            ? new Preparation(
                In(Zone.Daily).Select(ReadDailyLine).ToList(),
                In(Zone.FollowUps).Select(ReadFollowUp).ToList(),
                In(Zone.TechnicalPoints).Select(ReadTechnicalPoint).ToList(),
                In(Zone.ToRaise).Select(WithoutBold).ToList())
            : null;
        return new JournalDay(preparation, In(Zone.Decisions).Select(ReadDecision).ToList(), reports);
    }

    private sealed class Item(Zone zone, string text, int indent, bool fromHeading)
    {
        public Zone Zone { get; } = zone;

        public string Text { get; set; } = text;

        public int Indent { get; } = indent;

        public bool FromHeading { get; } = fromHeading;

        public bool HasChildren { get; set; }
    }

    /// <summary>
    /// The lines inside a code block, fences included. A fence never closed hides nothing: a model may forget to close
    /// one, and the decisions appended after it would be lost.
    /// </summary>
    private static bool[] CodeLines(string[] lines)
    {
        var code = new bool[lines.Length];
        int? open = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal) && !trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                continue;
            }

            if (open is { } first)
            {
                Array.Fill(code, true, first, i - first + 1);
                open = null;
            }
            else
            {
                open = i;
            }
        }

        return code;
    }

    /// <summary>
    /// Follows the headings. <c>/prep-reunions</c> writes its block at the top of the day, so the first preparation is
    /// the latest; an older one below it is skipped. The preparation may be a <c>#</c> or a <c>##</c> heading, and its
    /// sections any level from its own. Inside it, a heading deeper than its section stays in that section, an unknown
    /// heading opens no section, and the preparation ends at a heading of its level or above that is none of its
    /// sections, at a report (« ## 09:10 · … ») or at the day's decisions written at its sections' level.
    /// </summary>
    private sealed partial class Cursor
    {
        private int _preparationLevel;
        private int _sectionsLevel;
        private int _sectionLevel;

        public Zone Zone { get; private set; }

        public bool PreparationFound { get; private set; }

        /// <summary>The heading just read is a ticket of the daily or of the technical choices.</summary>
        public bool HeadingIsItem { get; private set; }

        public void Enter(int level, string title)
        {
            HeadingIsItem = false;
            if (_preparationLevel > 0)
            {
                bool decisionsBlock = title.StartsWith("decisions de la reunion technique", StringComparison.Ordinal)
                    && level <= (_sectionsLevel > 0 ? _sectionsLevel : _preparationLevel + 1);
                bool sameLevelSection = level == _preparationLevel && Section(title) is not null && !decisionsBlock;
                if (decisionsBlock || Report().IsMatch(title) || (level <= _preparationLevel && !sameLevelSection))
                {
                    _preparationLevel = 0;
                }
                else
                {
                    InPreparation(level, title);
                    return;
                }
            }

            if (Zone == Zone.Decisions && level > _sectionLevel && !title.StartsWith("preparation", StringComparison.Ordinal) && !Report().IsMatch(title))
            {
                // A heading inside the day's decisions groups some of them.
                return;
            }

            _sectionLevel = level;
            if (title.StartsWith("preparation", StringComparison.Ordinal) && level <= 2 && !PreparationFound)
            {
                PreparationFound = true;
                _preparationLevel = level;
                _sectionsLevel = 0;
                Zone = Zone.Preparation;
            }
            else
            {
                Zone = title.StartsWith("decisions de la reunion technique", StringComparison.Ordinal) ? Zone.Decisions : Zone.None;
            }
        }

        private void InPreparation(int level, string title)
        {
            bool leaf = Zone is Zone.Daily or Zone.FollowUps or Zone.TechnicalPoints or Zone.ToRaise;
            if (leaf && level > _sectionLevel && Section(title) is null)
            {
                HeadingIsItem = Zone is Zone.Daily or Zone.TechnicalPoints;
                return;
            }

            if (Section(title) is { } section)
            {
                Zone = section;
                _sectionLevel = level;
                _sectionsLevel = _sectionsLevel == 0 ? level : Math.Min(_sectionsLevel, level);
                return;
            }

            // An unknown heading inside the preparation: no section until the next known one.
            Zone = Zone.Preparation;
            _sectionLevel = level;
        }

        private static Zone? Section(string title) =>
            title.StartsWith("daily", StringComparison.Ordinal) ? Zone.Daily
            : title.StartsWith("reunion technique", StringComparison.Ordinal) ? Zone.Meeting
            : title.StartsWith("suivi", StringComparison.Ordinal) || title.StartsWith("decisions", StringComparison.Ordinal) ? Zone.FollowUps
            : title.Contains("soulever", StringComparison.Ordinal) ? Zone.ToRaise
            : title.StartsWith("mes tickets", StringComparison.Ordinal) || title.StartsWith("choix", StringComparison.Ordinal) || title.StartsWith("a expliquer", StringComparison.Ordinal) ? Zone.TechnicalPoints
            : null;

        /// <summary>A report block of <c>/rapport</c>: <c>## HH:mm · TICKET · intitulé</c>.</summary>
        [GeneratedRegex(@"^\d{1,2}\s*[:h]\s*\d{2}\b")]
        private static partial Regex Report();
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

    /// <summary>A report heading: <c>09:10 · DEMO-101 · Export CSV</c>; the title may be missing.</summary>
    [GeneratedRegex(@"^(?<time>\d{1,2}\s*[:h]\s*\d{2})\s*[·•]\s*(?<ticket>[^·•]*?)\s*(?:[·•]\s*(?<title>.*))?$")]
    private static partial Regex ReportHeading();

    /// <summary>A field of a daily line written on its own: « Fait : … », « **Reste** : … ».</summary>
    [GeneratedRegex(@"^\**\s*(?:fait|reste|testable)\s*\**\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex DailyField();
}
