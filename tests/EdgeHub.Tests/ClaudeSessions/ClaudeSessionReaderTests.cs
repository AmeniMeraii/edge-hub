using EdgeHub.ClaudeSessions;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using static EdgeHub.Tests.ClaudeSessions.SessionFolder;

namespace EdgeHub.Tests.ClaudeSessions;

public sealed class ClaudeSessionReaderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 30, 0, TimeSpan.Zero);

    private readonly SessionFolder _folder = new();
    private readonly ManualTimeProvider _time = new(Now);

    private ClaudeSessionReader Reader(string? projects = null) =>
        new(new FixedOptionsMonitor<ClaudeCodeOptions>(new ClaudeCodeOptions { ProjectsFolder = projects ?? _folder.Root }), _time, NullLogger<ClaudeSessionReader>.Instance);

    private static DateTimeOffset Ago(int minutes) => Now.AddMinutes(-minutes);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Finds_nothing_without_a_projects_folder()
    {
        Assert.Null(Reader(projects: "").FindActive());
        Assert.Null(Reader(projects: Path.Combine(_folder.Root, "missing")).FindActive());
    }

    [Fact]
    public void Finds_the_session_whose_last_message_is_recent()
    {
        _folder.Write("a1", Ago(2), [User(Ago(40)), AiTitle("Export CSV de la bibliothèque"), Assistant(Ago(2), branch: "feature/DEMO-101")]);

        ActiveSession session = Assert.IsType<ActiveSession>(Reader().FindActive());

        Assert.Equal("Export CSV de la bibliothèque", session.Name);
        Assert.Equal("feature/DEMO-101", session.Branch);
        Assert.Equal(Ago(40), session.StartedAt);
        Assert.Equal(Ago(2), session.LastActivityAt);
    }

    [Fact]
    public void A_session_silent_for_15_minutes_is_not_active_even_if_its_file_keeps_changing()
    {
        // Claude Code keeps appending lines without a timestamp after the last message.
        _folder.Write("a1", Ago(1), [User(Ago(30)), Assistant(Ago(16)), LastPrompt(), AiTitle("Export")]);

        Assert.Null(Reader().FindActive());
    }

    [Fact]
    public void A_working_subagent_keeps_its_session_active()
    {
        _folder.Write("a1", Ago(20), [User(Ago(25)), AiTitle("Export"), Assistant(Ago(20))]);
        _folder.Write("a1", Ago(1), [Assistant(Ago(1))], subagent: "x1");

        ActiveSession session = Assert.IsType<ActiveSession>(Reader().FindActive());

        Assert.Equal(Ago(1), session.LastActivityAt);
        Assert.Equal("Export", session.Name);
    }

    [Fact]
    public void Names_the_session_after_rename_first_then_the_generated_title_then_the_project_folder()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5)), Assistant(Ago(1))]);
        Assert.Equal("bookshelf", Reader().FindActive()?.Name);

        _folder.Write("a1", Ago(1), [User(Ago(5)), AiTitle("Export"), Assistant(Ago(1))]);
        Assert.Equal("Export", Reader().FindActive()?.Name);

        // /rename wins over the generated titles, even those written after it.
        _folder.Write("a1", Ago(1), [User(Ago(5)), AiTitle("Export"), CustomTitle("DEMO-101"), AiTitle("Export CSV"), Assistant(Ago(1))]);
        Assert.Equal("DEMO-101", Reader().FindActive()?.Name);

        _folder.WriteCustomTitleFile("a1", "DEMO-102");
        Assert.Equal("DEMO-102", Reader().FindActive()?.Name);
    }

    [Fact]
    public void Gives_the_branch_of_the_last_message_and_none_for_a_detached_head()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5), branch: "main"), Assistant(Ago(1), branch: "feature/DEMO-102")]);
        Assert.Equal("feature/DEMO-102", Reader().FindActive()?.Branch);

        _folder.Write("a1", Ago(1), [User(Ago(5)), Assistant(Ago(1), branch: "HEAD")]);
        Assert.Null(Reader().FindActive()?.Branch);
    }

    [Fact]
    public void Counts_each_file_edited_successfully_once_subagents_included()
    {
        _folder.Write("a1", Ago(1),
        [
            User(Ago(9)),
            Edited(Ago(8), @"C:\Users\demo\repo\bookshelf\Export.cs"),
            Edited(Ago(7), @"C:\Users\demo\repo\bookshelf\export.cs"),
            Edited(Ago(6), @"C:\Users\demo\repo\bookshelf\Broken.cs", error: true),
            Assistant(Ago(1)),
        ]);
        _folder.Write("a1", Ago(2), [Edited(Ago(2), @"C:\Users\demo\repo\bookshelf\ExportTests.cs")], subagent: "x1");

        Assert.Equal(2, Reader().FindActive()?.EditedFiles);
    }

    [Fact]
    public void Reads_only_the_lines_added_since_the_last_time_and_waits_for_an_unfinished_line()
    {
        string path = _folder.Write("a1", Ago(1), [User(Ago(5)), AiTitle("Export"), Assistant(Ago(1))]);
        ClaudeSessionReader reader = Reader();
        Assert.Equal("Export", reader.FindActive()?.Name);

        // A line still being written has no line break yet.
        string line = CustomTitle("DEMO-101");
        File.AppendAllText(path, line[..10]);
        File.SetLastWriteTimeUtc(path, Now.UtcDateTime);
        Assert.Equal("Export", reader.FindActive()?.Name);

        File.AppendAllText(path, line[10..] + "\n");
        File.SetLastWriteTimeUtc(path, Now.UtcDateTime);
        Assert.Equal("DEMO-101", reader.FindActive()?.Name);
    }

    [Fact]
    public void Starts_over_when_a_file_is_replaced_by_a_shorter_one()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5)), AiTitle("Un titre généré assez long pour la démo"), Assistant(Ago(1))]);
        ClaudeSessionReader reader = Reader();
        Assert.NotNull(reader.FindActive());

        _folder.Write("a1", Ago(1), [User(Ago(3)), Assistant(Ago(1))]);

        ActiveSession session = Assert.IsType<ActiveSession>(reader.FindActive());
        Assert.Equal("bookshelf", session.Name);
        Assert.Equal(Ago(3), session.StartedAt);
    }

    [Fact]
    public void Skips_broken_lines_and_line_types_it_does_not_know()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5)), "{not json", """{"type":"pr-link","prUrl":"https://example.test/pr/1"}""", "[1,2]", Assistant(Ago(1))]);

        Assert.Equal("bookshelf", Reader().FindActive()?.Name);
    }

    [Fact]
    public void Keeps_the_most_recent_of_two_active_sessions()
    {
        // The most recent one is found first, so keeping the last one found would fail.
        _folder.Write("a1", Ago(2), [User(Ago(9)), CustomTitle("DEMO-101"), Assistant(Ago(2))]);
        _folder.Write("a2", Ago(4), [User(Ago(9)), CustomTitle("DEMO-102"), Assistant(Ago(4))]);

        Assert.Equal("DEMO-101", Reader().FindActive()?.Name);
    }

    [Fact]
    public void Does_not_read_again_the_lines_it_has_already_read()
    {
        string path = _folder.Write("a1", Ago(1), [User(Ago(5), text: new string('x', 400)), AiTitle("Export"), Assistant(Ago(1))]);
        ClaudeSessionReader reader = Reader();
        Assert.Equal("Export", reader.FindActive()?.Name);

        // The title line changes in place, past the start of the file: only a full read would see it.
        File.WriteAllText(path, File.ReadAllText(path).Replace(AiTitle("Export"), AiTitle("Autre!"), StringComparison.Ordinal));
        File.AppendAllText(path, Assistant(Now) + "\n");
        File.SetLastWriteTimeUtc(path, Now.UtcDateTime);

        Assert.Equal("Export", reader.FindActive()?.Name);
    }

    [Fact]
    public void Starts_over_when_a_file_is_replaced_by_a_longer_one()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5)), AiTitle("Export"), Assistant(Ago(1))]);
        ClaudeSessionReader reader = Reader();
        Assert.Equal("Export", reader.FindActive()?.Name);

        _folder.Write("a1", Now, [User(Ago(3), cwd: @"C:\Users\demo\repo\library"), AiTitle("Recherche par auteur, une session bien plus longue"), Assistant(Now)]);

        ActiveSession session = Assert.IsType<ActiveSession>(reader.FindActive());
        Assert.Equal("Recherche par auteur, une session bien plus longue", session.Name);
        Assert.Equal(Ago(3), session.StartedAt);
    }

    [Fact]
    public void A_broken_line_never_stops_the_search()
    {
        // A tool result followed by more JSON on the same line, and a title ending with half an emoji.
        string trailing = Edited(Ago(4), @"C:\Users\demo\repo\bookshelf\Export.cs") + """{"type":"assistant"}""";
        string halfEmoji = """{"type":"ai-title","aiTitle":"Export \ud83d"}""";
        _folder.Write("a1", Ago(1), [User(Ago(5)), trailing, halfEmoji, AiTitle("Export CSV"), Assistant(Ago(1))]);
        _folder.Write("a2", Ago(2), [User(Ago(9)), CustomTitle("DEMO-102"), Assistant(Ago(2))]);

        ActiveSession session = Assert.IsType<ActiveSession>(Reader().FindActive());

        Assert.Equal("Export CSV", session.Name);
    }

    [Fact]
    public void Counts_a_file_edited_by_the_session_and_one_of_its_subagents_once()
    {
        _folder.Write("a1", Ago(1), [User(Ago(9)), Edited(Ago(8), @"C:\Users\demo\repo\bookshelf\Export.cs"), Assistant(Ago(1))]);
        _folder.Write("a1", Ago(2), [Edited(Ago(2), "C:/Users/demo/repo/bookshelf/Export.cs")], subagent: "x1");

        Assert.Equal(1, Reader().FindActive()?.EditedFiles);
    }

    [Fact]
    public void Skips_a_byte_order_mark_and_an_empty_branch()
    {
        string path = _folder.Write("a1", Ago(1), [User(Ago(30), branch: ""), Assistant(Ago(1), branch: "")]);
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(path)]);
        File.SetLastWriteTimeUtc(path, Ago(1).UtcDateTime);

        ActiveSession session = Assert.IsType<ActiveSession>(Reader().FindActive());

        Assert.Equal(Ago(30), session.StartedAt);
        Assert.Null(session.Branch);
    }

    [Fact]
    public void Shortens_a_long_name_without_cutting_an_emoji_in_half()
    {
        _folder.Write("a1", Ago(1), [User(Ago(5)), CustomTitle(new string('a', 119) + "🙂 la suite"), Assistant(Ago(1))]);

        string name = Assert.IsType<ActiveSession>(Reader().FindActive()).Name;

        Assert.Equal(new string('a', 119), name);
    }

    [Fact]
    public void Does_not_read_a_session_whose_files_have_not_changed_for_15_minutes()
    {
        // Its last message looks recent, but nothing was written lately: the cheap first filter leaves it out.
        _folder.Write("a1", Ago(20), [User(Ago(30)), Assistant(Ago(1))]);

        Assert.Null(Reader().FindActive());
    }
}
