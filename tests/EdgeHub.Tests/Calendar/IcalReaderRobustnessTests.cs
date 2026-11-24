using System.Diagnostics;
using EdgeHub.Calendar;

namespace EdgeHub.Tests.Calendar;

/// <summary>One broken event must never hide the rest of the calendar, nor take long to fail.</summary>
public sealed class IcalReaderRobustnessTests
{
    private const string Paris = "Europe/Paris";
    private static readonly DateOnly Thursday = new(2026, 11, 19);

    private const string GoodEvent = """
        BEGIN:VEVENT
        UID:good@edgehub.test
        SUMMARY:🏃 Footing du jeudi
        DTSTART;TZID=Europe/Paris:20261119T121500
        DTEND;TZID=Europe/Paris:20261119T130000
        END:VEVENT
        """;

    private static string Calendar(params string[] events) =>
        "BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Edge Hub//Invented//FR\n" + string.Join("\n", events) + "\nEND:VCALENDAR\n";

    private static List<string> Titles(string ics) => IcalReader.EventsOn(ics, Thursday, Paris).Select(e => e.Title).ToList();

    [Fact]
    public void An_event_in_an_unknown_time_zone_is_skipped_and_the_others_stay()
    {
        const string unknownZone = """
            BEGIN:VEVENT
            UID:unknown-zone@edgehub.test
            SUMMARY:Réunion importée
            DTSTART;TZID=Customized Time Zone:20150105T090000
            DTEND;TZID=Customized Time Zone:20150105T100000
            RRULE:FREQ=WEEKLY
            END:VEVENT
            """;

        Assert.Equal(["🏃 Footing du jeudi"], Titles(Calendar(unknownZone, GoodEvent)));
    }

    [Fact]
    public void A_rule_that_never_matches_is_skipped_quickly()
    {
        const string impossible = """
            BEGIN:VEVENT
            UID:impossible@edgehub.test
            SUMMARY:Le 30 février
            DTSTART;TZID=Europe/Paris:20260101T090000
            DTEND;TZID=Europe/Paris:20260101T100000
            RRULE:FREQ=DAILY;BYMONTH=2;BYMONTHDAY=30
            END:VEVENT
            """;
        var watch = Stopwatch.StartNew();

        IReadOnlyList<string> titles = Titles(Calendar(impossible, GoodEvent));

        Assert.Equal(["🏃 Footing du jeudi"], titles);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
    }

    [Fact]
    public void A_garbled_event_is_skipped_and_the_others_stay()
    {
        const string garbled = """
            BEGIN:VEVENT
            UID:garbled@edgehub.test
            SUMMARY:Événement abîmé
            DTSTART;TZID=Europe/Paris:20261119T080000
            RRULE:FREQ=FORTNIGHTLY
            END:VEVENT
            """;

        Assert.Equal(["🏃 Footing du jeudi"], Titles(Calendar(garbled, GoodEvent)));
    }

    [Fact]
    public void An_event_never_closed_does_not_hide_the_events_after_it()
    {
        const string unclosed = """
            BEGIN:VEVENT
            UID:unclosed@edgehub.test
            SUMMARY:Jamais fermé
            DTSTART;TZID=Europe/Paris:20261119T080000
            """;

        Assert.Equal(["🏃 Footing du jeudi"], Titles(Calendar(unclosed, GoodEvent)));
    }

    [Fact]
    public void The_uid_of_a_reminder_inside_an_event_is_not_the_events_uid()
    {
        const string master = """
            BEGIN:VEVENT
            BEGIN:VALARM
            UID:alarm@edgehub.test
            ACTION:DISPLAY
            TRIGGER:-PT10M
            END:VALARM
            UID:weekly@edgehub.test
            SUMMARY:💪 Salle du jeudi
            DTSTART;TZID=Europe/Paris:20261105T170000
            DTEND;TZID=Europe/Paris:20261105T180000
            RRULE:FREQ=WEEKLY;BYDAY=TH
            END:VEVENT
            """;
        const string moved = """
            BEGIN:VEVENT
            UID:weekly@edgehub.test
            RECURRENCE-ID;TZID=Europe/Paris:20261119T170000
            SUMMARY:💪 Salle du jeudi
            DTSTART;TZID=Europe/Paris:20261119T190000
            DTEND;TZID=Europe/Paris:20261119T200000
            END:VEVENT
            """;

        CalendarEvent session = Assert.Single(IcalReader.EventsOn(Calendar(master, moved), Thursday, Paris));
        Assert.Equal(19, session.Start.Hour);
    }

    [Fact]
    public void A_series_repeating_every_hour_or_faster_is_skipped()
    {
        const string hourly = """
            BEGIN:VEVENT
            UID:hourly@edgehub.test
            SUMMARY:Rappel toutes les heures
            DTSTART;TZID=Europe/Paris:20200101T090000
            RRULE:FREQ=HOURLY;BYHOUR=9
            END:VEVENT
            """;
        var watch = Stopwatch.StartNew();

        Assert.Equal(["🏃 Footing du jeudi"], Titles(Calendar(hourly, GoodEvent)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
    }

    [Theory]
    [InlineData("<!DOCTYPE html><html><body>Connexion au Wi-Fi de l'hôtel</body></html>")]
    [InlineData("")]
    [InlineData("BEGIN:VCALENDAR\nVERSION:2.0\nBEGIN:VEVENT\nUID:truncated")]
    public void Text_that_is_not_a_calendar_is_refused(string text)
    {
        Assert.Throws<FormatException>(() => IcalReader.Load(text));
    }

    [Fact]
    public void A_calendar_with_no_event_is_a_valid_empty_calendar()
    {
        Assert.Empty(IcalReader.Load(Calendar()).EventsOn(Thursday, Paris).Events);
    }

    [Fact]
    public void Says_how_many_series_could_not_be_read_on_a_day()
    {
        const string unknownZone = """
            BEGIN:VEVENT
            UID:unknown-zone@edgehub.test
            SUMMARY:Réunion importée
            DTSTART;TZID=Customized Time Zone:20150105T090000
            RRULE:FREQ=WEEKLY
            END:VEVENT
            """;

        DayEvents day = IcalReader.Load(Calendar(unknownZone, GoodEvent)).EventsOn(Thursday, Paris);

        Assert.Single(day.Events);
        Assert.Equal(1, day.SkippedSeries);
    }
}
