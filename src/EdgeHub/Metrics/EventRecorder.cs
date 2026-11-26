namespace EdgeHub.Metrics;

/// <summary>
/// An event sent by the screen to <c>POST /api/events</c>. <see cref="At"/> is when it happened on the screen, and
/// <see cref="Id"/> lets an event sent twice be kept once.
/// </summary>
public sealed record EventRequest(string? Type, DateTimeOffset? At, string? Task, string? Id = null);

/// <summary>
/// Checks and keeps the screen's events. The screen keeps the events it could not send and sends them later, so an
/// event may be a few days old, but not more than 8. An event from the future comes from a tablet whose clock is ahead:
/// it is dated now rather than lost.
/// </summary>
public sealed partial class EventRecorder(EventLog log, TimeProvider time)
{
    private const int MaxTaskLength = 200;
    private static readonly TimeSpan OldestAccepted = TimeSpan.FromDays(8);

    public IResult Record(EventRequest? request)
    {
        if (request?.Type is not { } type || !MetricEventType.FromScreen.Contains(type))
        {
            return TypedResults.BadRequest("Type d'événement inconnu.");
        }

        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset at = request.At ?? now;
        if (at < now - OldestAccepted)
        {
            return TypedResults.BadRequest("Événement de plus de 8 jours.");
        }

        at = at > now ? now : at;
        string? id = request.Id is { } given && Identifier().IsMatch(given) ? given : null;
        if (id is not null && log.Contains(id, now - OldestAccepted - TimeSpan.FromDays(1), now + TimeSpan.FromDays(1)))
        {
            // Sent again after a lost answer: kept already.
            return TypedResults.NoContent();
        }

        string? task = type == MetricEventType.TaskDone ? Clean(request.Task) : null;
        log.Append(new MetricEvent(at, type, task, id));
        return TypedResults.NoContent();
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial System.Text.RegularExpressions.Regex Identifier();

    /// <summary>One line of at most 200 characters, without control characters.</summary>
    private static string? Clean(string? task)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return null;
        }

        string line = new string(task.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return line.Length > MaxTaskLength ? line[..MaxTaskLength] : line;
    }
}
