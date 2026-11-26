using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EdgeHub.Metrics;

/// <summary>Something the screen (or the PC) noticed, for the weekly indicators.</summary>
public sealed record MetricEvent(DateTimeOffset At, string Type, string? Task = null);

public static class MetricEventType
{
    /// <summary>A focus session ran to its end (not a pause).</summary>
    public const string FocusCompleted = "focus-completed";

    /// <summary>A pause of the focus timer started.</summary>
    public const string BreakStarted = "break-started";

    /// <summary>The screen reminded me to take a pause after too long on the same task.</summary>
    public const string PauseReminder = "pause-reminder";

    /// <summary>« Terminé » in « Une seule chose », with the task's text.</summary>
    public const string TaskDone = "task-done";

    /// <summary>« Changer de tâche » in « Une seule chose ».</summary>
    public const string TaskSwitched = "task-switched";

    /// <summary>« Séance faite ».</summary>
    public const string SportDone = "sport-done";

    /// <summary>Written by the PC itself: the first time it saw the day's preparation in the journal.</summary>
    public const string PreparationSeen = "preparation-seen";

    /// <summary>The events the screen may send.</summary>
    public static IReadOnlySet<string> FromScreen { get; } =
        new HashSet<string>([FocusCompleted, BreakStarted, PauseReminder, TaskDone, TaskSwitched, SportDone], StringComparer.Ordinal);
}

/// <summary>
/// <c>events.jsonl</c>: one event per line, appended, kept on this PC. A line that cannot be read is skipped. The file
/// grows by a few dozen short lines a day.
/// </summary>
public sealed class EventLog(IOptionsMonitor<MetricsOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly Lock _gate = new();

    /// <summary>The measure is on and has a folder.</summary>
    public bool IsEnabled => options.CurrentValue.ResolvedFolder() is not null;

    public bool Append(MetricEvent metricEvent)
    {
        ArgumentNullException.ThrowIfNull(metricEvent);
        if (options.CurrentValue.ResolvedFolder() is not { } folder)
        {
            return false;
        }

        string line = JsonSerializer.Serialize(metricEvent, Json) + "\n";
        lock (_gate)
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "events.jsonl"), line);
        }

        return true;
    }

    /// <summary>The events from <paramref name="from"/> included to <paramref name="to"/> excluded, in the file's order.</summary>
    public IReadOnlyList<MetricEvent> Read(DateTimeOffset from, DateTimeOffset to)
    {
        if (options.CurrentValue.ResolvedFolder() is not { } folder)
        {
            return [];
        }

        string path = Path.Combine(folder, "events.jsonl");
        string[] lines;
        lock (_gate)
        {
            if (!File.Exists(path))
            {
                return [];
            }

            lines = File.ReadAllLines(path);
        }

        var events = new List<MetricEvent>();
        foreach (string line in lines)
        {
            MetricEvent? read;
            try
            {
                read = JsonSerializer.Deserialize<MetricEvent>(line, Json);
            }
            catch (JsonException)
            {
                continue;
            }

            if (read is { Type: not null } && read.At != default && read.At >= from && read.At < to)
            {
                events.Add(read);
            }
        }

        return events;
    }
}
