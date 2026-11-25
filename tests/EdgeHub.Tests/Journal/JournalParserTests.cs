using EdgeHub.Journal;

namespace EdgeHub.Tests.Journal;

public sealed class JournalParserTests
{
    private const string NoBreakSpace = "\u00A0";
    private const string NarrowNoBreakSpace = "\u202F";
    private static readonly string Demo = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "journal", "2026-11-25.md"));

    private static Preparation DemoPreparation => Assert.IsType<Preparation>(JournalParser.Parse(Demo).Preparation);

    [Fact]
    public void Reads_one_daily_line_per_ticket_with_done_remaining_and_testable()
    {
        Assert.Collection(
            DemoPreparation.Daily,
            line =>
            {
                Assert.Equal("DEMO-101", line.Ticket);
                Assert.Equal("Export CSV de la bibliothèque", line.Title);
                Assert.Equal("colonnes auteur et ISBN", line.Done);
                Assert.Equal("tri par date d'ajout", line.Remaining);
                Assert.Equal("oui", line.Testable);
            },
            line =>
            {
                Assert.Equal("DEMO-102", line.Ticket);
                Assert.Equal("à compléter", line.Remaining);
                Assert.Equal("jeudi", line.Testable);
            });
    }

    [Fact]
    public void Leaves_the_daily_lines_of_the_reports_out_of_the_preparation()
    {
        Assert.Equal(2, DemoPreparation.Daily.Count);
    }

    [Fact]
    public void Reads_the_follow_up_of_previous_decisions_with_their_state()
    {
        Assert.Collection(
            DemoPreparation.FollowUps,
            decision =>
            {
                Assert.Equal("Paginer l'API des livres par curseur", decision.Decision);
                Assert.Equal(DecisionStatus.Applied, decision.Status);
            },
            decision =>
            {
                Assert.Equal(DecisionStatus.InProgress, decision.Status);
                Assert.Equal("en cours (d'après le journal)", decision.StateText);
            },
            decision => Assert.Equal(DecisionStatus.NotStarted, decision.Status));
    }

    [Fact]
    public void Reads_the_technical_choices_to_explain_and_the_points_to_raise()
    {
        TechnicalPoint first = DemoPreparation.TechnicalPoints[0];
        Assert.Equal("DEMO-101 Export CSV de la bibliothèque", first.Ticket);
        Assert.StartsWith("écriture en flux", first.Text, StringComparison.Ordinal);
        Assert.Equal(2, DemoPreparation.TechnicalPoints.Count);

        Assert.Equal(
            ["Le PO veut aussi un export Excel : à arbitrer, la PR est déjà grosse.", "Faire valider le format de date ISO dans l'export."],
            DemoPreparation.ToRaise);
    }

    [Fact]
    public void Reads_the_decisions_of_todays_meeting_with_who_and_when()
    {
        Assert.Collection(
            JournalParser.Parse(Demo).Decisions,
            decision =>
            {
                Assert.Equal("L'export Excel attendra la version 2", decision.Decision);
                Assert.Equal("PO", decision.Who);
                Assert.Equal("à préciser", decision.When);
            },
            decision => Assert.Equal("vendredi", decision.When));
    }

    [Fact]
    public void Finds_no_preparation_in_a_day_with_reports_only()
    {
        JournalDay day = JournalParser.Parse("""
            # Journal du 25/11/2026

            ## 09:10 · DEMO-101 · Export CSV

            **Pour le daily**
            - DEMO-101 Export CSV · Fait : colonnes · Reste : tri
            """);

        Assert.Null(day.Preparation);
        Assert.Empty(day.Decisions);
    }

    [Fact]
    public void Keeps_the_latest_preparation_which_is_written_at_the_top()
    {
        JournalDay day = JournalParser.Parse("""
            # Journal du 25/11/2026

            # Préparation du 25/11/2026

            ## Daily (PO et testeurs)
            - DEMO-102 Recherche · Fait : index · Reste : tests · Testable : non

            # Préparation du 25/11/2026

            ## Daily (PO et testeurs)
            - DEMO-101 Export · Fait : rien · Reste : tout · Testable : non
            """);

        Assert.Equal("DEMO-102", Assert.Single(Assert.IsType<Preparation>(day.Preparation).Daily).Ticket);
    }

    [Fact]
    public void Tolerates_small_differences_in_how_the_blocks_are_written()
    {
        // Headings in another case and without their details, "*" bullets, a no-break space before the colon, "->".
        JournalDay day = JournalParser.Parse($"""
            # PRÉPARATION DU 25/11/2026
            ## daily
            * DEMO-101 Export · Fait{NoBreakSpace}: colonnes · Reste{NarrowNoBreakSpace}: tri · Testable : oui
            ## Réunion Technique
            ### Suivi des décisions
            - Paginer l'API -> Appliquée
            - Renommer le dépôt → abandonnée
            ### À soulever
            - **Urgent** : la base de test est pleine
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        DailyLine line = Assert.Single(preparation.Daily);
        Assert.Equal(("colonnes", "tri", "oui"), (line.Done, line.Remaining, line.Testable));
        Assert.Equal([DecisionStatus.Applied, DecisionStatus.Unknown], preparation.FollowUps.Select(d => d.Status));
        Assert.Equal("abandonnée", preparation.FollowUps[1].StateText);
        Assert.Empty(preparation.TechnicalPoints);
        Assert.Equal(["Urgent : la base de test est pleine"], preparation.ToRaise);
    }

    [Fact]
    public void Keeps_a_daily_line_whose_fields_are_missing()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            - DEMO-101 Export CSV, en pause
            """);

        DailyLine line = Assert.Single(Assert.IsType<Preparation>(day.Preparation).Daily);
        Assert.Equal(("DEMO-101", "Export CSV, en pause"), (line.Ticket, line.Title));
        Assert.Equal(((string?)null, (string?)null, (string?)null), (line.Done, line.Remaining, line.Testable));
    }

    [Fact]
    public void Reads_nothing_from_an_empty_day()
    {
        JournalDay day = JournalParser.Parse("");

        Assert.Null(day.Preparation);
        Assert.Empty(day.Decisions);
    }

    [Fact]
    public void Gathers_every_block_of_meeting_decisions()
    {
        JournalDay day = JournalParser.Parse("""
            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser

            ## 14:00 · DEMO-102 · Recherche

            ## Décisions de la réunion technique
            - Revoir l'index · Qui : moi · Pour quand : lundi
            """);

        Assert.Equal(["Garder .NET 10", "Revoir l'index"], day.Decisions.Select(d => d.Decision));
    }
}
