using EdgeHub.Journal;
using EdgeHub.Metrics;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdgeHub.Tests.Metrics;

public sealed class PreparationWatchTests : IDisposable
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private readonly DirectoryInfo _journal = Directory.CreateTempSubdirectory("edgehub-journal-");
    private readonly DirectoryInfo _metrics = Directory.CreateTempSubdirectory("edgehub-metrics-");
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 9, 12, 0, TimeSpan.FromHours(1)), Paris);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Today => Path.Combine(_journal.FullName, "2026-11-26.md");

    private EventLog Events(bool enabled = true) =>
        new(new FixedOptionsMonitor<MetricsOptions>(new MetricsOptions { Enabled = enabled, Folder = _metrics.FullName }));

    private PreparationWatch Watch(bool enabled = true) =>
        new(
            new JournalReader(new FixedOptionsMonitor<JournalOptions>(new JournalOptions { Folder = _journal.FullName })),
            Events(enabled),
            _time,
            NullLogger<PreparationWatch>.Instance);

    private List<MetricEvent> Seen() =>
        Events().Read(_time.GetUtcNow().AddDays(-1), _time.GetUtcNow().AddDays(1)).Where(e => e.Type == MetricEventType.PreparationSeen).ToList();

    public void Dispose()
    {
        _journal.Delete(recursive: true);
        _metrics.Delete(recursive: true);
    }

    [Fact]
    public async Task Notes_when_it_first_sees_the_days_preparation()
    {
        PreparationWatch watch = Watch();
        await watch.RecordOnceAsync(Ct);
        Assert.Empty(Seen());

        await File.WriteAllTextAsync(Today, "# Préparation du 26/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);
        await watch.RecordOnceAsync(Ct);
        _time.Advance(TimeSpan.FromMinutes(1));
        await watch.RecordOnceAsync(Ct);

        Assert.Equal(new DateTimeOffset(2026, 11, 26, 9, 12, 0, TimeSpan.FromHours(1)), Assert.Single(Seen()).At);
    }

    [Fact]
    public async Task Does_not_note_it_again_after_a_restart()
    {
        PreparationWatch watch = Watch();
        await watch.RecordOnceAsync(Ct);
        await File.WriteAllTextAsync(Today, "# Préparation du 26/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);
        await watch.RecordOnceAsync(Ct);

        _time.Advance(TimeSpan.FromHours(1));
        await Watch().RecordOnceAsync(Ct);

        Assert.Single(Seen());
    }

    [Fact]
    public async Task Notes_nothing_for_a_preparation_already_there_when_it_starts()
    {
        // Written while the service was stopped: when it came is unknown, not late.
        await File.WriteAllTextAsync(Today, "# Préparation du 26/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);
        PreparationWatch watch = Watch();
        await watch.RecordOnceAsync(Ct);
        _time.Advance(TimeSpan.FromMinutes(1));
        await watch.RecordOnceAsync(Ct);

        Assert.Empty(Seen());
    }

    [Fact]
    public async Task Notes_the_preparation_of_each_day()
    {
        PreparationWatch watch = Watch();
        await watch.RecordOnceAsync(Ct);
        await File.WriteAllTextAsync(Today, "# Préparation du 26/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);
        await watch.RecordOnceAsync(Ct);

        _time.Advance(TimeSpan.FromDays(1));
        await watch.RecordOnceAsync(Ct);
        await File.WriteAllTextAsync(Path.Combine(_journal.FullName, "2026-11-27.md"), "# Préparation du 27/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);
        await watch.RecordOnceAsync(Ct);

        Assert.Equal(2, Events().Read(_time.GetUtcNow().AddDays(-3), _time.GetUtcNow().AddDays(1)).Count(e => e.Type == MetricEventType.PreparationSeen));
    }

    [Fact]
    public async Task Notes_nothing_when_the_measure_is_off()
    {
        await File.WriteAllTextAsync(Today, "# Préparation du 26/11/2026\n## Daily\n- DEMO-101 Export · Fait : a\n", Ct);

        await Watch(enabled: false).RecordOnceAsync(Ct);

        Assert.Empty(Seen());
    }
}
