using EdgeHub.Metrics;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Metrics;

public sealed class EventLogTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(1));

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("edgehub-metrics-");

    private string File => Path.Combine(_folder.FullName, "events.jsonl");

    private EventLog Log(bool enabled = true, string? folder = null) =>
        new(new FixedOptionsMonitor<MetricsOptions>(new MetricsOptions { Enabled = enabled, Folder = folder ?? _folder.FullName }));

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void Keeps_each_event_on_its_own_line_and_reads_them_back()
    {
        EventLog log = Log();

        Assert.True(log.Append(new MetricEvent(Monday, MetricEventType.TaskDone, "DEMO-101 Export CSV")));
        Assert.True(log.Append(new MetricEvent(Monday.AddMinutes(5), MetricEventType.FocusCompleted)));

        Assert.Equal(2, System.IO.File.ReadAllLines(File).Length);
        Assert.Equal(
            [new MetricEvent(Monday, MetricEventType.TaskDone, "DEMO-101 Export CSV"), new MetricEvent(Monday.AddMinutes(5), MetricEventType.FocusCompleted)],
            log.Read(Monday.AddDays(-1), Monday.AddDays(1)));
    }

    [Fact]
    public void Reads_only_the_events_of_the_period()
    {
        EventLog log = Log();
        log.Append(new MetricEvent(Monday.AddDays(-1), MetricEventType.FocusCompleted));
        log.Append(new MetricEvent(Monday, MetricEventType.SportDone));

        Assert.Equal([MetricEventType.SportDone], log.Read(Monday, Monday.AddDays(7)).Select(e => e.Type));
    }

    [Fact]
    public void Skips_lines_it_cannot_read()
    {
        System.IO.File.WriteAllText(File, "{broken\n[1]\n{\"type\":\"focus-completed\"}\n");
        EventLog log = Log();
        log.Append(new MetricEvent(Monday, MetricEventType.FocusCompleted));

        Assert.Single(log.Read(Monday.AddDays(-1), Monday.AddDays(1)));
    }

    [Fact]
    public void An_interrupted_line_does_not_swallow_the_next_event()
    {
        System.IO.File.WriteAllText(File, "{\"at\":\"2026-11-23T09:00:00+01:00\",\"type\":\"focus-comp");
        EventLog log = Log();

        log.Append(new MetricEvent(Monday, MetricEventType.SportDone));

        Assert.Equal([MetricEventType.SportDone], log.Read(Monday.AddDays(-1), Monday.AddDays(1)).Select(e => e.Type));
    }

    [Fact]
    public void Reads_only_the_end_of_a_file_far_too_large()
    {
        // A file grown out of all proportion: only its last megabytes are read, whole lines only.
        using (var writer = new StreamWriter(File))
        {
            string old = "{\"at\":\"2026-11-01T09:00:00+01:00\",\"type\":\"focus-completed\",\"task\":\"" + new string('x', 1000) + "\"}";
            for (int i = 0; i < (EventLog.MaxBytes / old.Length) + 10; i++)
            {
                writer.WriteLine(old);
            }
        }

        EventLog log = Log();
        log.Append(new MetricEvent(Monday, MetricEventType.SportDone));

        Assert.Equal([MetricEventType.SportDone], log.Read(Monday.AddDays(-1), Monday.AddDays(1)).Select(e => e.Type));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "")]
    public void Writes_nothing_when_the_measure_is_off(bool enabled, string? folder)
    {
        EventLog log = Log(enabled, folder);

        Assert.False(log.Append(new MetricEvent(Monday, MetricEventType.FocusCompleted)));
        Assert.Empty(log.Read(Monday.AddDays(-1), Monday.AddDays(1)));
        Assert.False(System.IO.File.Exists(File));
    }
}
