namespace EdgeHub.Metrics;

/// <summary>An event sent by the screen to <c>POST /api/events</c>. <see cref="At"/> is when it happened on the screen.</summary>
public sealed record EventRequest(string? Type, DateTimeOffset? At, string? Task);

/// <summary>
/// Checks and keeps the screen's events. The screen keeps the events it could not send and sends them later, so an
/// event may be a few days old; it may not come from much further back, nor from the future.
/// </summary>
public sealed class EventRecorder(EventLog log, TimeProvider time)
{
    private const int MaxTaskLength = 200;
    private static readonly TimeSpan OldestAccepted = TimeSpan.FromDays(8);
    private static readonly TimeSpan ClockDrift = TimeSpan.FromMinutes(5);

    public IResult Record(EventRequest? request)
    {
        if (request?.Type is not { } type || !MetricEventType.FromScreen.Contains(type))
        {
            return TypedResults.BadRequest("Type d'événement inconnu.");
        }

        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset at = request.At ?? now;
        if (at < now - OldestAccepted || at > now + ClockDrift)
        {
            return TypedResults.BadRequest("Date de l'événement trop éloignée de maintenant.");
        }

        string? task = type == MetricEventType.TaskDone ? Clean(request.Task) : null;
        log.Append(new MetricEvent(at, type, task));
        return TypedResults.NoContent();
    }

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
