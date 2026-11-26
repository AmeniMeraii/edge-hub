using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EdgeHub.Metrics;

/// <summary>
/// Something the screen (or the PC) noticed, for the weekly indicators. <see cref="Id"/>, given by the screen, keeps an
/// event sent twice from being counted twice.
/// </summary>
public sealed record MetricEvent(DateTimeOffset At, string Type, string? Task = null, string? Id = null);

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
/// grows by a few dozen short lines a day; past <see cref="MaxBytes"/>, only its end is read.
/// </summary>
public sealed class EventLog(IOptionsMonitor<MetricsOptions> options)
{
    /// <summary>Years of events; a larger file is not read whole.</summary>
    public const int MaxBytes = 4 * 1024 * 1024;

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
            string path = Path.Combine(folder, "events.jsonl");

            // A line cut short (power cut) must not swallow this one.
            File.AppendAllText(path, EndsWithLineBreak(path) ? line : "\n" + line);
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

            lines = ReadEnd(path);
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

    /// <summary>The ids of the events of the period, to recognise an event sent twice.</summary>
    public bool Contains(string id, DateTimeOffset from, DateTimeOffset to) =>
        Read(from, to).Any(e => string.Equals(e.Id, id, StringComparison.Ordinal));

    private static bool EndsWithLineBreak(string path)
    {
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length == 0)
        {
            return true;
        }

        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() == '\n';
    }

    /// <summary>The whole lines of the last <see cref="MaxBytes"/> of the file.</summary>
    private static string[] ReadEnd(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        bool partial = stream.Length > MaxBytes;
        if (partial)
        {
            stream.Seek(-MaxBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        string[] lines = reader.ReadToEnd().Split('\n');

        // The first line of a partial read starts in the middle of an event.
        return partial ? lines[1..] : lines;
    }
}
