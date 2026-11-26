using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace EdgeHub.ClaudeHooks;

[JsonConverter(typeof(JsonStringEnumConverter<PermissionKind>))]
public enum PermissionKind
{
    /// <summary>A shell command (<c>PowerShell</c>, <c>Bash</c>).</summary>
    Command,

    /// <summary>A file written or edited: the card shows its path, never its content.</summary>
    File,

    Other,
}

public enum Verdict
{
    Allow,
    Deny,

    /// <summary>« Répondre sur le PC »: no decision, Claude Code asks in VS Code.</summary>
    OnPc,
}

public enum AnswerResult
{
    Done,

    /// <summary>No such card waits any more: answered, given up, or never there.</summary>
    Unknown,

    /// <summary>The tablet may not allow a sensitive request.</summary>
    NotAllowed,
}

[JsonConverter(typeof(JsonStringEnumConverter<NoticeKind>))]
public enum NoticeKind
{
    /// <summary>« Claude Code a besoin de toi ».</summary>
    NeedsYou,

    /// <summary>« Claude Code a fini ».</summary>
    Done,
}

/// <summary>
/// A permission request as the tablet will show it. <paramref name="SessionId"/> only ties the request to its session's
/// notifications; it is never shown.
/// </summary>
public sealed record PermissionRequestInfo(
    string Session,
    string Tool,
    PermissionKind Kind,
    string Summary,
    string? Description,
    string? Folder,
    bool Sensitive,
    string? Reason,
    string? SessionId = null);

/// <summary>A request waiting for the tablet's answer, until <paramref name="ExpiresAt"/>.</summary>
public sealed record PermissionCard(
    string Id,
    string Session,
    string Tool,
    PermissionKind Kind,
    string Summary,
    string? Description,
    string? Folder,
    bool Sensitive,
    string? Reason,
    DateTimeOffset ReceivedAt,
    DateTimeOffset ExpiresAt);

public sealed record Notice(string Id, NoticeKind Kind, string Session, DateTimeOffset At);

/// <summary>The waiting requests, oldest first, and the notices of the last hour, oldest first.</summary>
public sealed record DeskState(IReadOnlyList<PermissionCard> Pending, IReadOnlyList<Notice> Notices);

/// <summary>
/// Where Claude Code's requests wait for the tablet. A request waits until the tablet answers, until its time is up, or
/// until Claude Code stops waiting; only an answer from the tablet gives a decision, and the service never decides on its
/// own. Each decision taken on the tablet goes to <c>decisions.log</c>.
/// </summary>
public sealed class ClaudeDesk(TimeProvider time, DecisionLog decisions)
{
    public const int MaxNotices = 20;
    public static readonly TimeSpan NoticesFor = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();
    private readonly List<Waiting> _pending = [];
    private readonly List<Notice> _notices = [];

    /// <summary>Raised after each change (a card came or left, a notice came), outside the desk's lock.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Shows the request on the tablet and waits for its answer: the verdict, or null when nobody answered within
    /// <paramref name="wait"/> or when <paramref name="cancellationToken"/> says Claude Code stopped waiting.
    /// </summary>
    public async Task<Verdict?> AskAsync(PermissionRequestInfo request, TimeSpan wait, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset now = time.GetUtcNow();
        var waiting = new Waiting(
            new PermissionCard(NewId(), request.Session, request.Tool, request.Kind, request.Summary, request.Description, request.Folder, request.Sensitive, request.Reason, now, now + wait),
            request.SessionId);
        lock (_gate)
        {
            _pending.Add(waiting);
        }

        OnChanged();
        try
        {
            return await waiting.Answer.Task.WaitAsync(wait, time, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
            lock (_gate)
            {
                if (waiting.Verdict is { } answered)
                {
                    // The tablet answered at the very last moment: its answer stands.
                    return answered;
                }

                waiting.Closed = true;
                _pending.Remove(waiting);
            }

            OnChanged();
            return null;
        }
    }

    public AnswerResult Answer(string id, Verdict verdict)
    {
        Waiting? answered;
        lock (_gate)
        {
            answered = _pending.Find(w => string.Equals(w.Card.Id, id, StringComparison.Ordinal));
            if (answered is null || answered.Closed)
            {
                return AnswerResult.Unknown;
            }

            if (verdict == Verdict.Allow && answered.Card.Sensitive)
            {
                return AnswerResult.NotAllowed;
            }

            answered.Closed = true;
            answered.Verdict = verdict;
            _pending.Remove(answered);
        }

        // Logged before Claude Code goes on.
        decisions.Record(answered.Card, verdict);
        answered.Answer.TrySetResult(verdict);
        OnChanged();
        return AnswerResult.Done;
    }

    /// <summary>Whether a request of this session waits for the tablet.</summary>
    public bool IsAsking(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        lock (_gate)
        {
            return _pending.Exists(w => string.Equals(w.SessionId, sessionId, StringComparison.Ordinal));
        }
    }

    public void Notify(NoticeKind kind, string session)
    {
        DateTimeOffset now = time.GetUtcNow();
        lock (_gate)
        {
            ForgetOldNotices(now);
            _notices.Add(new Notice(NewId(), kind, session, now));
            if (_notices.Count > MaxNotices)
            {
                _notices.RemoveRange(0, _notices.Count - MaxNotices);
            }
        }

        OnChanged();
    }

    public DeskState Snapshot()
    {
        DateTimeOffset now = time.GetUtcNow();
        lock (_gate)
        {
            ForgetOldNotices(now);
            return new DeskState(_pending.Select(w => w.Card).ToList(), _notices.ToList());
        }
    }

    private void ForgetOldNotices(DateTimeOffset now) => _notices.RemoveAll(n => now - n.At >= NoticesFor);

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Random, so that a card still shown from before a restart can never answer a new request.</summary>
    private static string NewId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));

    private sealed class Waiting(PermissionCard card, string? sessionId)
    {
        public PermissionCard Card { get; } = card;

        public string? SessionId { get; } = sessionId;

        public TaskCompletionSource<Verdict?> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Answered or given up; changed under the desk's lock only.</summary>
        public bool Closed { get; set; }

        public Verdict? Verdict { get; set; }
    }
}
