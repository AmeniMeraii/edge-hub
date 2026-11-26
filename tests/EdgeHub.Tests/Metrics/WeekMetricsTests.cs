using EdgeHub.Calendar;
using EdgeHub.Journal;
using EdgeHub.Metrics;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Metrics;

/// <summary>
/// An invented week, Monday 23 to Sunday 29 November 2026, read on Thursday 26 at 15:00 in Paris. The daily is at 09:30.
/// </summary>
public sealed class WeekMetricsTests : IDisposable
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static readonly TimeSpan Winter = TimeSpan.FromHours(1);

    private readonly DirectoryInfo _journal = Directory.CreateTempSubdirectory("edgehub-journal-");
    private readonly DirectoryInfo _metrics = Directory.CreateTempSubdirectory("edgehub-metrics-");
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 15, 0, 0, Winter), Paris);
    private readonly FakeCalendar _calendar = new();

    public WeekMetricsTests()
    {
        Journal(23, prepared: true, "## 10:30 · DEMO-101 · Export CSV");
        Journal(24, prepared: true);
        Journal(26, prepared: true, "## 14:00 · DEMO-102 · Recherche par auteur");

        EventLog log = Events();
        log.Append(new MetricEvent(At(22, 18), MetricEventType.TaskDone, "DEMO-099 La semaine d'avant"));
        log.Append(new MetricEvent(At(23, 9), MetricEventType.PreparationSeen));
        log.Append(new MetricEvent(At(23, 10), MetricEventType.TaskDone, "DEMO-101 Export CSV"));
        log.Append(new MetricEvent(At(23, 11), MetricEventType.FocusCompleted));
        log.Append(new MetricEvent(At(23, 11, 30), MetricEventType.PauseReminder));
        log.Append(new MetricEvent(At(23, 11, 35), MetricEventType.BreakStarted));
        log.Append(new MetricEvent(At(23, 12), MetricEventType.FocusCompleted));
        log.Append(new MetricEvent(At(23, 13), MetricEventType.SportDone));
        log.Append(new MetricEvent(At(24, 10), MetricEventType.PreparationSeen));
        log.Append(new MetricEvent(At(24, 11), MetricEventType.TaskDone, "DEMO-103 Tri par date"));
        log.Append(new MetricEvent(At(24, 14), MetricEventType.PauseReminder));
        log.Append(new MetricEvent(At(24, 14, 20), MetricEventType.TaskSwitched));
        log.Append(new MetricEvent(At(25, 10), MetricEventType.FocusCompleted));
        log.Append(new MetricEvent(At(26, 14, 30), MetricEventType.TaskDone, "demo-102 recherche"));

        _calendar.Sessions[new DateOnly(2026, 11, 23)] = Session(23, "🏃 Footing", 12, 15);
        _calendar.Sessions[new DateOnly(2026, 11, 25)] = Session(25, "💪 Full body", 12);
        _calendar.Sessions[new DateOnly(2026, 11, 26)] = Session(26, "🏃 Footing du soir", 18);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Winter);

    private static CalendarEvent Session(int day, string title, int hour, int minute = 0) => new(title, At(day, hour, minute), At(day, hour + 1, minute), IsAllDay: false);

    private void Journal(int day, bool prepared, string report = "")
    {
        string preparation = prepared ? "# Préparation du jour\n## Daily (PO et testeurs)\n- DEMO-101 Export · Fait : a · Reste : b · Testable : oui\n" : "";
        File.WriteAllText(Path.Combine(_journal.FullName, $"2026-11-{day}.md"), $"# Journal\n\n{preparation}\n{report}\n");
    }

    private EventLog Events(MetricsOptions? options = null) => new(new FixedOptionsMonitor<MetricsOptions>(options ?? Options()));

    private MetricsOptions Options(string? dailyTime = "09:30") => new() { Folder = _metrics.FullName, DailyTime = dailyTime };

    private WeekMetrics Week(MetricsOptions? options = null, bool calendar = true)
    {
        options ??= Options();
        _calendar.IsConfigured = calendar;
        return new WeekMetrics(
            new JournalReader(new FixedOptionsMonitor<JournalOptions>(new JournalOptions { Folder = _journal.FullName })),
            Events(options),
            _calendar,
            new FixedOptionsMonitor<CalendarOptions>(new CalendarOptions { IcalUrl = "https://calendar.example.test/demo.ics" }),
            new FixedOptionsMonitor<MetricsOptions>(options),
            _time);
    }

    public void Dispose()
    {
        _journal.Delete(recursive: true);
        _metrics.Delete(recursive: true);
    }

    [Fact]
    public async Task Counts_the_days_held_preparation_done_and_each_finished_task_reported()
    {
        WeekMetricsMessage week = await Week().GetAsync(Ct);

        // Monday: prepared, its task has a report. Tuesday: DEMO-103 has none. Thursday: same ticket, other case.
        Assert.Equal([true, false, false, true], week.Days.Take(4).Select(d => d.Held));
        Assert.Equal((2, 4), (week.Totals!.HeldDays, week.Totals.ElapsedWorkDays));
        Assert.Equal(new DateOnly(2026, 11, 23), week.From);
        Assert.Equal(4, week.Days.Count);
    }

    [Fact]
    public async Task Counts_a_preparation_seen_before_the_daily()
    {
        WeekMetricsMessage week = await Week().GetAsync(Ct);

        // Seen at 09:00 on Monday, at 10:00 on Tuesday; on Thursday the service did not see it come.
        Assert.Equal([true, false, false, null], week.Days.Take(4).Select(d => d.PreparedBeforeDaily));
        Assert.Equal((3, 1, true), (week.Totals!.PreparedDays, week.Totals.PreparedBeforeDaily, week.Totals.DailyTimeSet));
    }

    [Fact]
    public async Task Cannot_tell_before_the_daily_without_its_time()
    {
        WeekMetricsMessage week = await Week(Options(dailyTime: null)).GetAsync(Ct);

        Assert.All(week.Days, d => Assert.Null(d.PreparedBeforeDaily));
        Assert.False(week.Totals!.DailyTimeSet);
    }

    [Fact]
    public async Task Counts_reports_and_the_days_worked()
    {
        WeekMetricsTotals totals = (await Week().GetAsync(Ct)).Totals!;

        // Wednesday has no journal but a focus session: it was worked.
        Assert.Equal((2, 2, 4), (totals.Reports, totals.DaysWithReport, totals.WorkedDays));
    }

    [Fact]
    public async Task Counts_focus_sessions_and_pause_reminders_followed_within_ten_minutes()
    {
        WeekMetricsTotals totals = (await Week().GetAsync(Ct)).Totals!;

        // Monday's pause started 5 minutes after the reminder; Tuesday's task changed 20 minutes after.
        Assert.Equal(3, totals.FocusCompleted);
        Assert.Equal((2, 1), (totals.PauseReminders, totals.PausesTaken));
    }

    [Fact]
    public async Task Counts_sport_sessions_done_out_of_those_planned_so_far()
    {
        WeekMetricsMessage week = await Week().GetAsync(Ct);

        // Thursday's session is at 18:00: not due yet at 15:00.
        Assert.Equal([true, false, true, false], week.Days.Take(4).Select(d => d.SportPlanned));
        Assert.Equal((1, 2), (week.Totals!.SportDone, week.Totals.SportPlanned));
    }

    [Fact]
    public async Task Cannot_count_planned_sessions_without_a_calendar()
    {
        WeekMetricsMessage week = await Week(calendar: false).GetAsync(Ct);

        Assert.Null(week.Totals!.SportPlanned);
        Assert.Equal(1, week.Totals.SportDone);
    }

    [Fact]
    public async Task Counts_an_event_sent_in_utc_on_its_local_day()
    {
        // 23:30 UTC on Tuesday is 00:30 on Wednesday in Paris: the screen sends UTC times.
        Events().Append(new MetricEvent(new DateTimeOffset(2026, 11, 24, 23, 30, 0, TimeSpan.Zero), MetricEventType.FocusCompleted));

        WeekMetricsMessage week = await Week().GetAsync(Ct);

        Assert.Equal([2, 0, 2], week.Days.Take(3).Select(d => d.FocusCompleted));
    }

    [Fact]
    public async Task Keeps_the_earliest_time_the_preparation_was_seen()
    {
        Events().Append(new MetricEvent(At(24, 9), MetricEventType.PreparationSeen));

        WeekMetricsMessage week = await Week().GetAsync(Ct);

        Assert.True(week.Days[1].PreparedBeforeDaily);
    }

    [Fact]
    public async Task Finds_a_tasks_report_however_the_ticket_is_written()
    {
        // A report heading with one separator, tasks written « [DEMO-104] … » and « DEMO-105: … », and a task without a
        // ticket whose report has the same title.
        Journal(24, prepared: true, """
            ## 11:30 · DEMO-103 Tri par date
            ## 12:00 · DEMO-104 · Couvertures
            ## 13:00 · demo-105 · Export Excel
            ## 14:00 · phase-0 · Bookshelf phase 0
            """);
        EventLog log = Events();
        log.Append(new MetricEvent(At(24, 12, 10), MetricEventType.TaskDone, "[DEMO-104] Couvertures"));
        log.Append(new MetricEvent(At(24, 13, 10), MetricEventType.TaskDone, "DEMO-105: export Excel"));
        log.Append(new MetricEvent(At(24, 14, 10), MetricEventType.TaskDone, "Bookshelf phase 0"));

        DayMetrics tuesday = (await Week().GetAsync(Ct)).Days[1];

        Assert.Equal((4, 4, true), (tuesday.TasksDone, tuesday.TasksReported, tuesday.Held));
    }

    [Fact]
    public async Task Leaves_today_out_until_it_can_be_told()
    {
        // 08:00 on Thursday: no preparation yet, the daily is at 09:30. Today is neither held nor missed so far.
        File.Delete(Path.Combine(_journal.FullName, "2026-11-26.md"));
        _time.Advance(TimeSpan.FromHours(-7));

        WeekMetricsMessage week = await Week().GetAsync(Ct);

        Assert.Null(week.Days[3].PreparedBeforeDaily);
        Assert.Equal((1, 3, 1, 3), (week.Totals!.HeldDays, week.Totals.ElapsedWorkDays, week.Totals.PreparedBeforeDaily, week.Totals.PreparedBeforeDailyKnown));
    }

    [Fact]
    public async Task Counts_sessions_done_among_those_planned_and_skips_a_day_the_calendar_cannot_give()
    {
        Events().Append(new MetricEvent(At(24, 18), MetricEventType.SportDone));
        _calendar.Unavailable.Add(new DateOnly(2026, 11, 25));

        WeekMetricsTotals totals = (await Week().GetAsync(Ct)).Totals!;

        // Tuesday's session was not planned; Wednesday is unknown.
        Assert.Equal((1, 1), (totals.SportDone, totals.SportPlanned));
    }

    [Fact]
    public async Task Says_when_the_measure_is_off()
    {
        WeekMetricsMessage week = await Week(new MetricsOptions { Enabled = false, Folder = _metrics.FullName }).GetAsync(Ct);

        Assert.False(week.Enabled);
        Assert.Null(week.Totals);
    }

    private sealed class FakeCalendar : ICalendarSource
    {
        public Dictionary<DateOnly, CalendarEvent> Sessions { get; } = [];

        public HashSet<DateOnly> Unavailable { get; } = [];

        public bool IsConfigured { get; set; } = true;

        public DateTimeOffset? LastReadAt => null;

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly day, CancellationToken cancellationToken) =>
            Unavailable.Contains(day)
                ? Task.FromException<IReadOnlyList<CalendarEvent>>(new CalendarUnavailableException())
                : Task.FromResult<IReadOnlyList<CalendarEvent>>(Sessions.TryGetValue(day, out CalendarEvent? session) ? [session] : []);
    }
}
