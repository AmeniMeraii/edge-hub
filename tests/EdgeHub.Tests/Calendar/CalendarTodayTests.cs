using EdgeHub.Calendar;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Calendar;

public sealed class CalendarTodayTests
{
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);
    private static readonly DateOnly Thursday = new(2026, 11, 19);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CalendarEvent At(string title, int hour, int minute = 0) =>
        new(title, new DateTimeOffset(2026, 11, 19, hour, minute, 0, Paris), new DateTimeOffset(2026, 11, 19, hour + 1, minute, 0, Paris), IsAllDay: false);

    private static CalendarToday Today(FakeCalendarSource source, DateTimeOffset now) =>
        new(source, new FixedOptionsMonitor<CalendarOptions>(new CalendarOptions { IcalUrl = "https://calendar.example.test/demo.ics" }), new ManualTimeProvider(now));

    [Fact]
    public async Task Says_when_no_calendar_is_configured()
    {
        CalendarTodayMessage today = await Today(new FakeCalendarSource { IsConfigured = false }, DateTimeOffset.UtcNow).GetAsync(Ct);

        Assert.False(today.Configured);
    }

    [Fact]
    public async Task Says_when_the_calendar_cannot_be_read()
    {
        CalendarTodayMessage today = await Today(new FakeCalendarSource { Unavailable = true }, DateTimeOffset.UtcNow).GetAsync(Ct);

        Assert.True(today.Configured);
        Assert.False(today.Available);
    }

    [Fact]
    public async Task Gives_the_days_events_and_the_retained_session()
    {
        var source = new FakeCalendarSource
        {
            Events = [At("☀️ Compléments matin — vit D + oméga 3", 8), At("🏃 Jeudi — course COURTE sous le seuil (en attendant le kiné)", 12, 15)],
        };

        CalendarTodayMessage today = await Today(source, new DateTimeOffset(2026, 11, 19, 9, 0, 0, Paris)).GetAsync(Ct);

        Assert.True(today.Available);
        Assert.Equal(Thursday, today.Day);
        Assert.Equal(source.LastReadAt, today.ReadAt);
        Assert.Equal(2, today.Events.Count);
        Assert.Equal("Course", today.Sport?.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 12, 15, 0, Paris), today.Sport?.Start);
        Assert.Null(today.CancelledTitle);
    }

    [Fact]
    public async Task Explains_a_day_whose_only_session_is_cancelled()
    {
        var source = new FakeCalendarSource { Events = [At("❌ Full B annulée cette semaine (sur site)", 12)] };

        CalendarTodayMessage today = await Today(source, new DateTimeOffset(2026, 11, 19, 9, 0, 0, Paris)).GetAsync(Ct);

        Assert.Null(today.Sport);
        Assert.Equal("Full B annulée cette semaine (sur site)", today.CancelledTitle);
    }

    [Fact]
    public async Task Today_is_the_day_in_the_calendar_time_zone()
    {
        // 23:30 UTC on the 19th is already 00:30 on the 20th in Paris.
        var source = new FakeCalendarSource();

        await Today(source, new DateTimeOffset(2026, 11, 19, 23, 30, 0, TimeSpan.Zero)).GetAsync(Ct);

        Assert.Equal(Thursday.AddDays(1), source.RequestedDay);
    }

    private sealed class FakeCalendarSource : ICalendarSource
    {
        public bool IsConfigured { get; init; } = true;

        public bool Unavailable { get; init; }

        public IReadOnlyList<CalendarEvent> Events { get; init; } = [];

        public DateTimeOffset? LastReadAt { get; init; } = new DateTimeOffset(2026, 11, 19, 8, 55, 0, TimeSpan.FromHours(1));

        public DateOnly? RequestedDay { get; private set; }

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly day, CancellationToken cancellationToken)
        {
            RequestedDay = day;
            return Unavailable ? Task.FromException<IReadOnlyList<CalendarEvent>>(new CalendarUnavailableException()) : Task.FromResult(Events);
        }
    }
}
