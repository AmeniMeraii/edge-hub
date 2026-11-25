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
    public void Recognises_a_state_written_in_bold_or_after_a_symbol()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Réunion technique
            ### Suivi des décisions précédentes
            - Paginer l'API → **appliquée**
            - Cacher les couvertures → ✅ en cours
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal([DecisionStatus.Applied, DecisionStatus.InProgress], preparation.FollowUps.Select(d => d.Status));
        Assert.Equal("appliquée", preparation.FollowUps[0].StateText);
    }

    [Fact]
    public void Reads_a_ticket_whose_fields_are_nested_bullets_as_one_line()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            - **DEMO-101 Export CSV**
              - Fait : colonnes
              - Reste : tri
              - Testable : oui
            - DEMO-102 Recherche · Fait : index · Reste : tests · Testable : non
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal(["DEMO-101", "DEMO-102"], preparation.Daily.Select(l => l.Ticket));
        Assert.Equal(("colonnes", "tri", "oui"), (preparation.Daily[0].Done, preparation.Daily[0].Remaining, preparation.Daily[0].Testable));
    }

    [Fact]
    public void Ignores_separators_and_code_blocks()
    {
        JournalDay day = JournalParser.Parse("""
            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser

            * * *
            ```
            - pas une décision
            ```
            """);

        Assert.Equal(["Garder .NET 10"], day.Decisions.Select(d => d.Decision));
    }

    [Fact]
    public void Reads_a_preparation_written_one_heading_level_lower()
    {
        // Placed "just below the title", the preparation can come as a level 2 heading with level 3 sections.
        JournalDay day = JournalParser.Parse("""
            # Journal du 25/11/2026
            ## Préparation du 25/11/2026
            ### 📋 Daily (PO et testeurs)
            - DEMO-101 Export · Fait : colonnes · Reste : tri · Testable : oui
            ### Réunion technique
            #### Points à soulever
            - La base de test est pleine.
            ## 09:10 · DEMO-101 · Export
            - DEMO-101 Export · Fait : rien · Reste : tout
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal("DEMO-101", Assert.Single(preparation.Daily).Ticket);
        Assert.Equal(["La base de test est pleine."], preparation.ToRaise);
    }

    [Fact]
    public void Keeps_a_section_whose_items_sit_under_deeper_headings()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Réunion technique
            ### Mes tickets, côté technique
            #### DEMO-101 Export CSV
            - écriture en flux.
            ### À soulever
            #### Urgent
            - La base de test est pleine.

            ## Décisions de la réunion technique
            ### API
            - Paginer par curseur · Qui : moi · Pour quand : lundi
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        TechnicalPoint point = Assert.Single(preparation.TechnicalPoints);
        Assert.Equal(("DEMO-101 Export CSV", "écriture en flux."), (point.Ticket, point.Text));
        Assert.Contains("La base de test est pleine.", preparation.ToRaise);
        Assert.Equal("Paginer par curseur", Assert.Single(day.Decisions).Decision);
    }

    [Fact]
    public void Reads_a_ticket_written_as_a_heading_with_its_fields_below()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            ### DEMO-101 Export CSV
            - Fait : colonnes
            - Reste : tri
            ### DEMO-102 Recherche
              - Fait : index
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal(["DEMO-101", "DEMO-102"], preparation.Daily.Select(l => l.Ticket));
        Assert.Equal(("colonnes", "tri"), (preparation.Daily[0].Done, preparation.Daily[0].Remaining));
        Assert.Equal("index", preparation.Daily[1].Done);
    }

    [Fact]
    public void Keeps_reading_the_preparation_past_a_heading_it_does_not_know()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Réunion technique
            ### Préparation de la mise en prod
            - Vérifier les migrations.
            ### À soulever
            - La base de test est pleine.
            """);

        Assert.Equal(["La base de test est pleine."], Assert.IsType<Preparation>(day.Preparation).ToRaise);
    }

    [Fact]
    public void Takes_previous_decisions_inside_the_preparation_as_its_follow_up()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Réunion technique
            ### Décisions de la réunion technique précédente
            - Paginer → appliquée
            ### À soulever
            - La base de test est pleine.
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal(DecisionStatus.Applied, Assert.Single(preparation.FollowUps).Status);
        Assert.Equal(["La base de test est pleine."], preparation.ToRaise);
        Assert.Empty(day.Decisions);
    }

    [Fact]
    public void Reads_a_preparation_whose_sections_share_its_heading_level()
    {
        JournalDay day = JournalParser.Parse("""
            # Journal du 25/11/2026
            ## Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            - DEMO-101 Export · Fait : colonnes · Reste : tri · Testable : oui
            ## Réunion technique
            ### À soulever
            - La base de test est pleine.
            ## 09:10 · DEMO-101 · Export
            - DEMO-101 Export · Fait : rien · Reste : tout
            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal("colonnes", Assert.Single(preparation.Daily).Done);
        Assert.Equal(["La base de test est pleine."], preparation.ToRaise);
        Assert.Equal("Garder .NET 10", Assert.Single(day.Decisions).Decision);
    }

    [Fact]
    public void Reads_the_days_decisions_written_right_after_the_preparation()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            - DEMO-101 Export · Fait : colonnes · Reste : tri · Testable : oui

            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser
            """);

        Assert.Single(Assert.IsType<Preparation>(day.Preparation).Daily);
        Assert.Equal("Garder .NET 10", Assert.Single(day.Decisions).Decision);
    }

    [Fact]
    public void Joins_a_nested_bullet_only_to_a_less_indented_one()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
              - DEMO-101 Export · Fait : colonnes · Reste : tri · Testable : oui
              - DEMO-102 Recherche · Fait : index · Reste : tests · Testable : non
            """);

        Assert.Equal(["DEMO-101", "DEMO-102"], Assert.IsType<Preparation>(day.Preparation).Daily.Select(l => l.Ticket));
    }

    [Fact]
    public void Keeps_each_point_to_raise_listed_under_a_heading_line()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Réunion technique
            ### À soulever
            - Questions et blocages :
              - La base de test est pleine.
              - Le flag des couvertures n'a pas de propriétaire.
            - Faire valider le format de date.
            """);

        Assert.Equal(
            ["La base de test est pleine.", "Le flag des couvertures n'a pas de propriétaire.", "Faire valider le format de date."],
            Assert.IsType<Preparation>(day.Preparation).ToRaise);
    }

    [Fact]
    public void A_code_block_left_open_hides_nothing()
    {
        JournalDay day = JournalParser.Parse("""
            ## 09:10 · DEMO-101 · Export
            ```csharp
            var x = 1;

            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser
            """);

        Assert.Equal("Garder .NET 10", Assert.Single(day.Decisions).Decision);
    }

    [Fact]
    public void Takes_a_known_section_written_too_deep_as_a_section_not_a_ticket()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            - DEMO-101 Export · Fait : colonnes · Reste : tri · Testable : oui
            ### Réunion technique
            #### Suivi des décisions précédentes
            - Paginer → appliquée
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal("DEMO-101", Assert.Single(preparation.Daily).Ticket);
        Assert.Equal(DecisionStatus.Applied, Assert.Single(preparation.FollowUps).Status);
    }

    [Fact]
    public void Joins_only_fields_to_a_ticket_written_as_a_heading()
    {
        JournalDay day = JournalParser.Parse("""
            # Préparation du 25/11/2026
            ## Daily (PO et testeurs)
            ### DEMO-101 Export
            - Fait : colonnes
            - DEMO-102 Recherche · Fait : index · Reste : tests · Testable : non
            """);

        Preparation preparation = Assert.IsType<Preparation>(day.Preparation);
        Assert.Equal(["DEMO-101", "DEMO-102"], preparation.Daily.Select(l => l.Ticket));
        Assert.Equal(("colonnes", (string?)null), (preparation.Daily[0].Done, preparation.Daily[0].Remaining));
    }

    [Fact]
    public void A_report_below_the_days_decisions_is_not_one_of_them()
    {
        JournalDay day = JournalParser.Parse("""
            ## Décisions de la réunion technique
            - Garder .NET 10 · Qui : équipe · Pour quand : à préciser
            ### 14:00 · DEMO-102 · Recherche
            - DEMO-102 Recherche · Fait : index · Reste : tests
            """);

        Assert.Equal("Garder .NET 10", Assert.Single(day.Decisions).Decision);
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
