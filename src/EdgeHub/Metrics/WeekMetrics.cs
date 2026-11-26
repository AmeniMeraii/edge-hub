using EdgeHub.Calendar;
using EdgeHub.Journal;
using Microsoft.Extensions.Options;

namespace EdgeHub.Metrics;

/// <summary>
/// What <c>/api/metrics/week</c> answers: the current week, Monday to Sunday, day by day up to today, and its totals.
/// </summary>
public sealed record WeekMetricsMessage(bool Enabled, DateOnly? From, DateOnly? To, IReadOnlyList<DayMetrics> Days, WeekMetricsTotals? Totals)
{
    public static WeekMetricsMessage Disabled { get; } = new(false, null, null, [], null);
}

/// <summary>
/// One day. <c>PreparedBeforeDaily</c> is null when the daily's time is not set, or when the PC did not see the
/// preparation come; <c>SportPlanned</c> is null without a calendar, and today's session counts once its time has
/// come, or once done.
/// </summary>
public sealed record DayMetrics(
    DateOnly Day,
    bool WorkDay,
    bool Prepared,
    bool? PreparedBeforeDaily,
    int Reports,
    int TasksDone,
    int TasksReported,
    bool Held,
    int FocusCompleted,
    bool? SportPlanned,
    bool SportDone,
    int PauseReminders,
    int PausesTaken);

/// <summary>
/// The week so far. <c>HeldDays</c>, the main indicator, counts the work days with the preparation done and every
/// finished task reported; <c>WorkedDays</c>, the work days with a journal file or an event from the screen.
/// </summary>
public sealed record WeekMetricsTotals(
    int HeldDays,
    int ElapsedWorkDays,
    int PreparedDays,
    int PreparedBeforeDaily,
    bool DailyTimeSet,
    int Reports,
    int DaysWithReport,
    int WorkedDays,
    int FocusCompleted,
    int SportDone,
    int? SportPlanned,
    int PauseReminders,
    int PausesTaken);

/// <summary>
/// The indicators of <c>docs/product/indicateurs.md</c>, from the journal, the screen's events and the calendar. A day
/// is held when its preparation is done and each task finished with « Terminé » has a report with the same ticket (the
/// task's first word). A pause reminder is followed when a pause starts, or the task is finished or changed, within 10
/// minutes. Days are the PC's local days.
/// </summary>
public sealed class WeekMetrics(
    JournalReader journal,
    EventLog events,
    ICalendarSource calendar,
    IOptionsMonitor<CalendarOptions> calendarOptions,
    IOptionsMonitor<MetricsOptions> options,
    TimeProvider time)
{
    private static readonly TimeSpan PauseWindow = TimeSpan.FromMinutes(10);

    public async Task<WeekMetricsMessage> GetAsync(CancellationToken cancellationToken)
    {
        if (!events.IsEnabled)
        {
            return WeekMetricsMessage.Disabled;
        }

        TimeZoneInfo zone = time.LocalTimeZone;
        DateTimeOffset now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        DateOnly monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        TimeOnly? daily = options.CurrentValue.DailyAt();

        ILookup<DateOnly, MetricEvent> byDay = events
            .Read(StartOf(monday, zone), StartOf(monday.AddDays(7), zone))
            .ToLookup(e => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.At, zone).DateTime));

        var days = new List<Day>();
        for (DateOnly day = monday; day <= today; day = day.AddDays(1))
        {
            days.Add(await DayAsync(day, byDay[day].ToList(), daily, now, zone, cancellationToken).ConfigureAwait(false));
        }

        List<DayMetrics> all = days.Select(d => d.Metrics).ToList();
        List<DayMetrics> work = all.Where(d => d.WorkDay).ToList();
        var totals = new WeekMetricsTotals(
            HeldDays: work.Count(d => d.Held),
            ElapsedWorkDays: work.Count,
            PreparedDays: work.Count(d => d.Prepared),
            PreparedBeforeDaily: work.Count(d => d.PreparedBeforeDaily == true),
            DailyTimeSet: daily is not null,
            Reports: all.Sum(d => d.Reports),
            DaysWithReport: work.Count(d => d.Reports > 0),
            WorkedDays: days.Count(d => d.Metrics.WorkDay && d.Worked),
            FocusCompleted: all.Sum(d => d.FocusCompleted),
            SportDone: all.Count(d => d.SportDone),
            SportPlanned: all.Any(d => d.SportPlanned is null) ? null : all.Count(d => d.SportPlanned == true),
            PauseReminders: all.Sum(d => d.PauseReminders),
            PausesTaken: all.Sum(d => d.PausesTaken));
        return new WeekMetricsMessage(true, monday, monday.AddDays(6), all, totals);
    }

    private async Task<Day> DayAsync(DateOnly day, List<MetricEvent> dayEvents, TimeOnly? daily, DateTimeOffset now, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        JournalFile file = await journal.ReadAsync(day, cancellationToken).ConfigureAwait(false) ?? JournalFile.Missing;
        bool prepared = file.Day.Preparation is not null;
        DateTimeOffset? seen = dayEvents.Where(e => e.Type == MetricEventType.PreparationSeen).Select(e => (DateTimeOffset?)e.At).Min();
        bool? beforeDaily = daily is null ? null
            : !prepared ? false
            : seen is null ? null
            : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(seen.Value, zone).DateTime) <= daily;

        HashSet<string> reported = file.Day.Reports.Select(r => r.Ticket).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> tasks = dayEvents.Where(e => e.Type == MetricEventType.TaskDone).Select(e => Ticket(e.Task)).ToList();
        int tasksReported = tasks.Count(reported.Contains);

        List<MetricEvent> reminders = dayEvents.Where(e => e.Type == MetricEventType.PauseReminder).ToList();
        int pausesTaken = reminders.Count(r => dayEvents.Any(e =>
            e.Type is MetricEventType.BreakStarted or MetricEventType.TaskDone or MetricEventType.TaskSwitched
            && e.At > r.At && e.At - r.At <= PauseWindow));

        bool sportDone = dayEvents.Any(e => e.Type == MetricEventType.SportDone);
        bool workDay = day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        var metrics = new DayMetrics(
            day,
            workDay,
            prepared,
            beforeDaily,
            file.Day.Reports.Count,
            tasks.Count,
            tasksReported,
            Held: workDay && prepared && tasksReported == tasks.Count,
            dayEvents.Count(e => e.Type == MetricEventType.FocusCompleted),
            await SportPlannedAsync(day, sportDone, now, cancellationToken).ConfigureAwait(false),
            sportDone,
            reminders.Count,
            pausesTaken);
        return new Day(metrics, file.Found || dayEvents.Count > 0);
    }

    private async Task<bool?> SportPlannedAsync(DateOnly day, bool done, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!calendar.IsConfigured)
        {
            return null;
        }

        IReadOnlyList<CalendarEvent> dayEvents;
        try
        {
            dayEvents = await calendar.GetEventsAsync(day, cancellationToken).ConfigureAwait(false);
        }
        catch (CalendarUnavailableException)
        {
            return null;
        }

        SportSession? session = new SportDetector(calendarOptions.CurrentValue.EffectiveSport).Pick(dayEvents, now).Session;
        return session is not null && (done || session.Start <= now);
    }

    /// <summary>The ticket of a task: its first word (« DEMO-101 Export CSV » → DEMO-101).</summary>
    private static string Ticket(string? task) =>
        task?.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) is [var first, ..] ? first : "";

    private static DateTimeOffset StartOf(DateOnly day, TimeZoneInfo zone)
    {
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }

    /// <summary>A day's metrics, and whether it was worked (a journal file or an event).</summary>
    private sealed record Day(DayMetrics Metrics, bool Worked);
}
