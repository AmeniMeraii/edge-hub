using Microsoft.Extensions.Options;

namespace EdgeHub.ClaudeSessions;

/// <summary>
/// What <c>/api/claude/current</c> answers: the session's name (the ticket), its branch, when it started, its last
/// activity and how many files it edited. Nothing else leaves the PC: no path, no line of the conversation.
/// </summary>
public sealed record ClaudeCurrentMessage(
    bool Configured,
    bool Active,
    string? Name,
    string? Branch,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastActivityAt,
    int EditedFiles)
{
    public static ClaudeCurrentMessage NotConfigured { get; } = new(false, false, null, null, null, null, 0);

    public static ClaudeCurrentMessage NoSession { get; } = new(true, false, null, null, null, null, 0);
}

public sealed class ClaudeCurrent(ClaudeSessionMonitor monitor, IOptionsMonitor<ClaudeCodeOptions> options, TimeProvider time)
{
    public ClaudeCurrentMessage Get()
    {
        if (options.CurrentValue.ResolvedFolder() is null)
        {
            return ClaudeCurrentMessage.NotConfigured;
        }

        // The last pass can be 30 seconds old: a session that went quiet in between is no longer active.
        if (monitor.Current is not { } session || time.GetUtcNow() - session.LastActivityAt >= ClaudeSessionReader.ActiveFor)
        {
            return ClaudeCurrentMessage.NoSession;
        }

        return new ClaudeCurrentMessage(true, true, session.Name, session.Branch, session.StartedAt, session.LastActivityAt, session.EditedFiles);
    }
}
