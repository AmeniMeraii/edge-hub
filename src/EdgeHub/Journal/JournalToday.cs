namespace EdgeHub.Journal;

/// <summary>
/// What <c>/api/journal/today</c> answers. <see cref="Found"/> is false when the day has no file yet (no
/// <c>/prep-reunions</c> and no <c>/rapport</c> today); <see cref="Available"/> is false when the file exists but cannot
/// be read. <see cref="Day"/> lets the screen drop the data of another day.
/// </summary>
public sealed record JournalTodayMessage(
    bool Configured,
    bool Available,
    DateOnly? Day,
    bool Found,
    DateTimeOffset? WrittenAt,
    Preparation? Preparation,
    IReadOnlyList<MeetingDecision> Decisions)
{
    public static JournalTodayMessage NotConfigured { get; } = new(false, false, null, false, null, null, []);
}

/// <summary>Today's journal, today being the PC's local date, as the Claude Code commands name their files.</summary>
public sealed class JournalToday(JournalReader reader, TimeProvider time)
{
    public async Task<JournalTodayMessage> GetAsync(CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), time.LocalTimeZone).DateTime);
        if (await reader.ReadAsync(today, cancellationToken).ConfigureAwait(false) is not { } file)
        {
            return JournalTodayMessage.NotConfigured;
        }

        return new JournalTodayMessage(true, file.Available, today, file.Found, file.WrittenAt, file.Day.Preparation, file.Day.Decisions);
    }
}
