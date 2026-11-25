namespace EdgeHub.Journal;

/// <summary>What Edge Hub reads in one day of the journal: the meeting preparation and the meeting's decisions.</summary>
public sealed record JournalDay(Preparation? Preparation, IReadOnlyList<MeetingDecision> Decisions)
{
    public static JournalDay Empty { get; } = new(null, []);
}

/// <summary>The block written by <c>/prep-reunions</c> (<c># Préparation du …</c>).</summary>
public sealed record Preparation(
    IReadOnlyList<DailyLine> Daily,
    IReadOnlyList<DecisionFollowUp> FollowUps,
    IReadOnlyList<TechnicalPoint> TechnicalPoints,
    IReadOnlyList<string> ToRaise);

/// <summary>One line of the daily: <c>- {ticket} {title} · Fait : … · Reste : … · Testable : …</c>.</summary>
public sealed record DailyLine(string Ticket, string Title, string? Done, string? Remaining, string? Testable);

/// <summary>A previous decision and its state: <c>- {decision} → appliquée / en cours / pas commencée</c>.</summary>
public sealed record DecisionFollowUp(string Decision, string Status, string? StateText);

/// <summary>A technical choice to explain: <c>- **{ticket} {title}** : {choice}</c>.</summary>
public sealed record TechnicalPoint(string? Ticket, string Text);

/// <summary>A decision of the meeting (<c>/decisions</c>): <c>- {decision} · Qui : … · Pour quand : …</c>.</summary>
public sealed record MeetingDecision(string Decision, string? Who, string? When);

/// <summary>The states <c>/prep-reunions</c> gives a previous decision.</summary>
public static class DecisionStatus
{
    public const string Applied = "applied";
    public const string InProgress = "inProgress";
    public const string NotStarted = "notStarted";

    /// <summary>Any other wording: the screen shows it as it is written.</summary>
    public const string Unknown = "unknown";
}
