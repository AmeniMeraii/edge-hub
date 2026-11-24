using EdgeHub.Calendar;

namespace EdgeHub.Tests.Calendar;

public sealed class IcalReaderTests
{
    private const string Paris = "Europe/Paris";
    private static readonly string Demo = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "calendar", "demo.ics"));

    private static IReadOnlyList<CalendarEvent> On(int month, int day) => IcalReader.EventsOn(Demo, new DateOnly(2026, month, day), Paris);

    [Fact]
    public void Unfolds_a_weekly_event_on_its_day_at_its_local_time()
    {
        CalendarEvent footing = Assert.Single(On(11, 5), e => e.Title == "🏃 Footing du jeudi");

        Assert.Equal(new DateTimeOffset(2026, 11, 5, 12, 15, 0, TimeSpan.FromHours(1)), footing.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 13, 0, 0, TimeSpan.FromHours(1)), footing.End);
        Assert.False(footing.IsAllDay);
    }

    [Fact]
    public void Leaves_out_an_excluded_date()
    {
        Assert.DoesNotContain(On(11, 12), e => e.Title.Contains("Footing", StringComparison.Ordinal));
    }

    [Fact]
    public void Shows_a_moved_occurrence_on_its_new_day_only()
    {
        Assert.DoesNotContain(On(11, 19), e => e.Title.Contains("Footing", StringComparison.Ordinal));

        CalendarEvent moved = Assert.Single(On(11, 21), e => e.Title.Contains("Footing", StringComparison.Ordinal));
        Assert.Equal("🏃 Footing — DÉPLACÉ samedi", moved.Title);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.FromHours(1)), moved.Start);
    }

    [Fact]
    public void Leaves_out_a_cancelled_occurrence()
    {
        Assert.DoesNotContain(On(11, 26), e => e.Title.Contains("Footing", StringComparison.Ordinal));
    }

    [Fact]
    public void Marks_an_all_day_event()
    {
        CalendarEvent birthday = Assert.Single(On(11, 19), e => e.Title == "Anniversaire de Demo");

        Assert.True(birthday.IsAllDay);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.FromHours(1)), birthday.Start);
    }

    [Theory]
    [InlineData(24, 2)]
    [InlineData(26, 1)]
    public void Keeps_the_local_time_across_the_change_of_hour(int day, int utcOffsetHours)
    {
        CalendarEvent ritual = Assert.Single(On(10, day), e => e.Title == "☀️ Rituel du matin");

        Assert.Equal(new DateTimeOffset(2026, 10, day, 8, 0, 0, TimeSpan.FromHours(utcOffsetHours)), ritual.Start);
    }

    [Fact]
    public void Converts_an_event_given_in_utc_to_the_calendar_time_zone()
    {
        CalendarEvent meeting = Assert.Single(On(11, 19), e => e.Title.StartsWith("Réunion", StringComparison.Ordinal));

        Assert.Equal(new DateTimeOffset(2026, 11, 19, 16, 0, 0, TimeSpan.FromHours(1)), meeting.Start);
        Assert.Equal(TimeSpan.FromHours(1), meeting.Start.Offset);
    }

    [Fact]
    public void Lists_the_day_in_order_of_start()
    {
        IReadOnlyList<CalendarEvent> day = On(11, 19);

        Assert.Equal(day.OrderBy(e => e.Start).Select(e => e.Title), day.Select(e => e.Title));
        Assert.Equal(2, day.Count);
    }
}
