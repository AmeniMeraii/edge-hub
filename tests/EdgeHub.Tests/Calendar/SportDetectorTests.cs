using EdgeHub.Calendar;

namespace EdgeHub.Tests.Calendar;

public sealed class SportDetectorTests
{
    private static readonly SportDetector Detector = new(SportRules.Default);

    private static readonly DateTimeOffset Day = new(2026, 11, 19, 0, 0, 0, TimeSpan.FromHours(1));

    private static CalendarEvent At(string title, int hour, int minute = 0, int minutes = 60) =>
        new(title, Day.AddHours(hour).AddMinutes(minute), Day.AddHours(hour).AddMinutes(minute + minutes), IsAllDay: false);

    // Section 4.2 of the plan: its examples, used as they are.
    [Theory]
    [InlineData("🏃 Jeudi — course COURTE sous le seuil (en attendant le kiné)", "Course")]
    [InlineData("💪 Full-body A — DÉPLACÉE samedi 21/11 — reprise S2", "Muscu")]
    [InlineData("☀️ Compléments matin — vit D + oméga 3", null)]
    public void Recognises_the_kind_of_session_from_the_title(string title, string? kind)
    {
        Assert.Equal(kind, Detector.KindOf(title));
    }

    [Theory]
    [InlineData("Footing tranquille", "Course")]
    [InlineData("Footings de la semaine", "Course")]
    [InlineData("FOOTING_matin", "Course")]
    [InlineData("Running du dimanche", "Course")]
    [InlineData("Run club du midi", "Course")]
    [InlineData("Musculation dos", "Muscu")]
    [InlineData("Salle avec le coach", "Muscu")]
    [InlineData("Full body B", "Muscu")]
    [InlineData("🚶 Petite marche", "Marche")]
    [InlineData("Marche digestive", "Marche")]
    [InlineData("Prune et café", null)]
    [InlineData("Marché de Noël", null)]
    [InlineData("Parcours client", null)]
    [InlineData("Démarche administrative", null)]
    public void Matches_keywords_at_the_start_of_a_word_whatever_the_case(string title, string? kind)
    {
        Assert.Equal(kind, Detector.KindOf(title));
    }

    [Theory]
    [InlineData("❌ Full B annulée cette semaine (sur site)", true)]
    [InlineData("❌ Pas de course ce samedi — Full A à la place", true)]
    [InlineData("  ❌ espaces avant la croix", true)]
    [InlineData("Course ANNULÉE", true)]
    [InlineData("💪 Full-body A — DÉPLACÉE samedi 21/11 — reprise S2", false)]
    public void Recognises_a_cancelled_session(string title, bool cancelled)
    {
        Assert.Equal(cancelled, Detector.IsCancelled(title));
    }

    [Fact]
    public void Retains_the_running_session_at_its_time()
    {
        SportDay day = Detector.Pick([At("🏃 Jeudi — course COURTE sous le seuil (en attendant le kiné)", 12, 15)], now: Day.AddHours(9));

        Assert.Equal("Course", day.Session?.Kind);
        Assert.Equal(Day.AddHours(12).AddMinutes(15), day.Session?.Start);
    }

    [Fact]
    public void Retains_the_strength_session_at_noon()
    {
        SportDay day = Detector.Pick([At("💪 Full-body A — DÉPLACÉE samedi 21/11 — reprise S2", 12)], now: Day.AddHours(9));

        Assert.Equal("Muscu", day.Session?.Kind);
        Assert.Equal(Day.AddHours(12), day.Session?.Start);
    }

    [Fact]
    public void A_cancelled_session_is_not_retained_but_explains_the_empty_day()
    {
        SportDay day = Detector.Pick([At("❌ Full B annulée cette semaine (sur site)", 12)], now: Day.AddHours(9));

        Assert.Null(day.Session);
        Assert.Equal("Full B annulée cette semaine (sur site)", day.CancelledTitle);
    }

    [Fact]
    public void Another_session_wins_over_a_cancelled_one()
    {
        SportDay day = Detector.Pick(
            [At("❌ Pas de course ce samedi — Full A à la place", 10), At("💪 Full-body A", 12)],
            now: Day.AddHours(9));

        Assert.Equal("Muscu", day.Session?.Kind);
        Assert.Equal("💪 Full-body A", day.Session?.Title);
    }

    [Fact]
    public void Retains_the_first_session_not_finished_yet()
    {
        CalendarEvent morning = At("🏃 Footing", 7);
        CalendarEvent noon = At("💪 Salle", 12, 15);

        Assert.Same(noon.Title, Detector.Pick([morning, noon], now: Day.AddHours(10)).Session?.Title);
        Assert.Same(morning.Title, Detector.Pick([morning, noon], now: Day.AddHours(7).AddMinutes(30)).Session?.Title);
    }

    [Fact]
    public void Retains_the_last_session_once_all_are_finished()
    {
        SportDay day = Detector.Pick([At("🏃 Footing", 7), At("💪 Salle", 12, 15)], now: Day.AddHours(20));

        Assert.Equal("💪 Salle", day.Session?.Title);
    }

    [Fact]
    public void Ignores_all_day_events_and_non_sport_events()
    {
        var allDay = new CalendarEvent("🏃 Semaine du marathon", Day, Day.AddDays(1), IsAllDay: true);

        SportDay day = Detector.Pick([allDay, At("☀️ Compléments matin — vit D + oméga 3", 8)], now: Day.AddHours(9));

        Assert.Null(day.Session);
        Assert.Null(day.CancelledTitle);
    }

    [Fact]
    public void Keywords_come_from_the_settings()
    {
        var rules = new SportRules
        {
            Kinds = [new SportKind { Name = "Natation", Keywords = ["🏊", "piscine"] }],
            CancelledMarkers = ["🚫"],
            CancelledWords = ["reporté"],
        };
        var detector = new SportDetector(rules);

        Assert.Equal("Natation", detector.KindOf("Piscine municipale"));
        Assert.Null(detector.KindOf("🏃 Footing"));
        Assert.True(detector.IsCancelled("🚫 Piscine"));
        Assert.True(detector.IsCancelled("Piscine reportée"));
    }
}
