using EdgeHub.ClaudeHooks;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.ClaudeHooks;

public sealed class ClaudeDeskTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.FromHours(1));
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(60);

    private readonly ManualTimeProvider _time = new(Now);
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("edgehub-decisions-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ClaudeDesk Desk() => new(_time, new DecisionLog(_folder.FullName, _time));

    private static PermissionRequestInfo Request(string summary = "git push origin main", bool sensitive = false) =>
        new("DEMO-101", "PowerShell", PermissionKind.Command, summary, "Push the branch", "bookshelf", sensitive, sensitive ? "push forcé" : null);

    private string DecisionsLog => Path.Combine(_folder.FullName, "decisions.log");

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task Gives_the_tablets_answer_to_the_waiting_request()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);

        PermissionCard card = Assert.Single(desk.Snapshot().Pending);
        Assert.Equal(("DEMO-101", "git push origin main", "bookshelf"), (card.Session, card.Summary, card.Folder));
        Assert.Equal(Now + Wait, card.ExpiresAt);
        Assert.Equal(AnswerResult.Done, desk.Answer(card.Id, Verdict.Allow));

        Assert.Equal(Verdict.Allow, await asked);
        Assert.Empty(desk.Snapshot().Pending);
    }

    [Theory]
    [InlineData(Verdict.Deny)]
    [InlineData(Verdict.OnPc)]
    public async Task Passes_on_a_refusal_or_hands_the_question_back_to_the_pc(Verdict verdict)
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);

        desk.Answer(Assert.Single(desk.Snapshot().Pending).Id, verdict);

        Assert.Equal(verdict, await asked);
    }

    [Fact]
    public async Task Never_lets_the_tablet_allow_a_sensitive_request()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request("git push --force", sensitive: true), Wait, Ct);
        string id = Assert.Single(desk.Snapshot().Pending).Id;

        Assert.Equal(AnswerResult.NotAllowed, desk.Answer(id, Verdict.Allow));
        Assert.False(asked.IsCompleted);

        Assert.Equal(AnswerResult.Done, desk.Answer(id, Verdict.OnPc));
        Assert.Equal(Verdict.OnPc, await asked);
    }

    [Fact]
    public async Task Gives_no_decision_when_nobody_answers_in_time()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);

        _time.Advance(Wait);

        Assert.Null(await asked);
        Assert.Empty(desk.Snapshot().Pending);
    }

    [Fact]
    public async Task Takes_the_card_away_when_the_question_is_answered_on_the_pc()
    {
        // Claude Code drops the hook's request: the card must leave the tablet.
        ClaudeDesk desk = Desk();
        using var aborted = new CancellationTokenSource();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, aborted.Token);

        await aborted.CancelAsync();

        Assert.Null(await asked);
        Assert.Empty(desk.Snapshot().Pending);
    }

    [Fact]
    public async Task Lists_the_waiting_requests_oldest_first_and_answers_each_once()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> first = desk.AskAsync(Request("mkdir exports"), Wait, Ct);
        _time.Advance(TimeSpan.FromSeconds(2));
        Task<Verdict?> second = desk.AskAsync(Request("git push origin main"), Wait, Ct);

        IReadOnlyList<PermissionCard> pending = desk.Snapshot().Pending;
        Assert.Equal(["mkdir exports", "git push origin main"], pending.Select(c => c.Summary));
        Assert.NotEqual(pending[0].Id, pending[1].Id);

        Assert.Equal(AnswerResult.Done, desk.Answer(pending[0].Id, Verdict.Deny));
        Assert.Equal(AnswerResult.Unknown, desk.Answer(pending[0].Id, Verdict.Allow));
        Assert.Equal(AnswerResult.Unknown, desk.Answer("no-such-card", Verdict.Allow));
        Assert.Equal(Verdict.Deny, await first);
        Assert.False(second.IsCompleted);
    }

    [Fact]
    public async Task Logs_each_decision_taken_on_the_tablet()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);
        desk.Answer(Assert.Single(desk.Snapshot().Pending).Id, Verdict.Allow);
        await asked;

        string line = Assert.Single(await File.ReadAllLinesAsync(DecisionsLog, Ct));
        Assert.Contains("autorisé depuis la tablette", line, StringComparison.Ordinal);
        Assert.Contains("git push origin main", line, StringComparison.Ordinal);
        Assert.Contains("DEMO-101", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_a_refusal_and_keeps_a_command_of_several_lines_on_one_line()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request("git add .\ngit commit -m wip"), Wait, Ct);
        desk.Answer(Assert.Single(desk.Snapshot().Pending).Id, Verdict.Deny);
        await asked;

        string line = Assert.Single(await File.ReadAllLinesAsync(DecisionsLog, Ct));
        Assert.Contains("refusé depuis la tablette", line, StringComparison.Ordinal);
        Assert.Contains("git add . ⏎ git commit -m wip", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_nothing_without_a_folder()
    {
        var desk = new ClaudeDesk(_time, new DecisionLog(null, _time));
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);
        desk.Answer(Assert.Single(desk.Snapshot().Pending).Id, Verdict.Allow);

        Assert.Equal(Verdict.Allow, await asked);
        Assert.Empty(_folder.EnumerateFiles());
    }

    [Fact]
    public async Task Knows_which_session_waits_for_the_tablet()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request() with { SessionId = "session-a" }, Wait, Ct);

        Assert.True(desk.IsAsking("session-a"));
        Assert.False(desk.IsAsking("session-b"));
        Assert.False(desk.IsAsking(null));

        _time.Advance(Wait);
        await asked;
        Assert.False(desk.IsAsking("session-a"));
    }

    [Fact]
    public async Task Says_when_a_card_comes_and_goes()
    {
        ClaudeDesk desk = Desk();
        int changes = 0;
        desk.Changed += (_, _) => changes++;

        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);
        Assert.Equal(1, changes);
        desk.Answer(Assert.Single(desk.Snapshot().Pending).Id, Verdict.Allow);
        await asked;
        Assert.Equal(2, changes);

        desk.Notify(NoticeKind.Done, "DEMO-101");
        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task Logs_nothing_when_the_tablet_did_not_decide()
    {
        ClaudeDesk desk = Desk();
        Task<Verdict?> asked = desk.AskAsync(Request(), Wait, Ct);
        _time.Advance(Wait);
        await asked;

        Assert.False(File.Exists(DecisionsLog));
    }

    [Fact]
    public void Keeps_the_latest_notices_for_the_screen()
    {
        ClaudeDesk desk = Desk();
        desk.Notify(NoticeKind.NeedsYou, "DEMO-101");
        _time.Advance(TimeSpan.FromSeconds(30));
        desk.Notify(NoticeKind.Done, "DEMO-102");

        Assert.Equal([(NoticeKind.NeedsYou, "DEMO-101"), (NoticeKind.Done, "DEMO-102")], desk.Snapshot().Notices.Select(n => (n.Kind, n.Session)));
        Assert.Equal(2, desk.Snapshot().Notices.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void Forgets_a_notice_after_an_hour()
    {
        ClaudeDesk desk = Desk();
        desk.Notify(NoticeKind.Done, "DEMO-099");
        _time.Advance(TimeSpan.FromMinutes(59));
        Assert.Single(desk.Snapshot().Notices);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(desk.Snapshot().Notices);
    }

    [Fact]
    public void Keeps_twenty_notices_at_most()
    {
        ClaudeDesk desk = Desk();
        for (int i = 0; i < 25; i++)
        {
            desk.Notify(NoticeKind.NeedsYou, $"DEMO-{i}");
        }

        IReadOnlyList<Notice> notices = desk.Snapshot().Notices;
        Assert.Equal(20, notices.Count);
        Assert.Equal(("DEMO-5", "DEMO-24"), (notices[0].Session, notices[^1].Session));
    }
}
