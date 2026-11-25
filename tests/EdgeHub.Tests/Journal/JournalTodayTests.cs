using EdgeHub.Journal;
using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Journal;

public sealed class JournalTodayTests : IDisposable
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static readonly string Demo = Path.Combine(AppContext.BaseDirectory, "Fixtures", "journal", "2026-11-25.md");

    // 00:30 in Paris on 25 November, still the 24th in UTC.
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 23, 30, 0, TimeSpan.Zero), Paris);
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("edgehub-journal-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Today => Path.Combine(_folder.FullName, "2026-11-25.md");

    private JournalToday Journal(string? folder = null) =>
        new(new FixedOptionsMonitor<JournalOptions>(new JournalOptions { Folder = folder ?? _folder.FullName }), _time);

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task Says_when_no_journal_folder_is_set()
    {
        JournalTodayMessage today = await Journal(folder: "").GetAsync(Ct);

        Assert.False(today.Configured);
    }

    [Fact]
    public async Task Says_when_the_day_has_no_file_yet()
    {
        JournalTodayMessage today = await Journal().GetAsync(Ct);

        Assert.True(today.Configured);
        Assert.True(today.Available);
        Assert.False(today.Found);
        Assert.Equal(new DateOnly(2026, 11, 25), today.Day);
    }

    [Fact]
    public async Task A_folder_that_does_not_exist_only_means_no_file()
    {
        JournalTodayMessage today = await Journal(Path.Combine(_folder.FullName, "missing")).GetAsync(Ct);

        Assert.True(today.Available);
        Assert.False(today.Found);
    }

    [Fact]
    public async Task Reads_the_file_named_after_the_local_date()
    {
        File.Copy(Demo, Today);

        JournalTodayMessage today = await Journal().GetAsync(Ct);

        Assert.True(today.Found);
        Assert.Equal(2, Assert.IsType<Preparation>(today.Preparation).Daily.Count);
        Assert.Equal(2, today.Decisions.Count);
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(Today)), today.WrittenAt);
    }

    [Fact]
    public async Task Reads_the_file_again_once_it_changes()
    {
        await File.WriteAllTextAsync(Today, "# Journal du 25/11/2026\n", Ct);
        JournalToday journal = Journal();
        Assert.Null((await journal.GetAsync(Ct)).Preparation);

        // The preparation is written at the top of the day.
        await File.WriteAllTextAsync(Today, await File.ReadAllTextAsync(Demo, Ct), Ct);
        File.SetLastWriteTimeUtc(Today, File.GetLastWriteTimeUtc(Today).AddSeconds(5));

        Assert.NotNull((await journal.GetAsync(Ct)).Preparation);
    }

    [Fact]
    public async Task Does_not_read_a_file_again_while_its_time_and_size_stay_the_same()
    {
        File.Copy(Demo, Today);
        DateTime written = File.GetLastWriteTimeUtc(Today);
        JournalToday journal = Journal();
        Assert.NotNull((await journal.GetAsync(Ct)).Preparation);

        // Same size and same write time: the file is taken as unchanged.
        string text = await File.ReadAllTextAsync(Today, Ct);
        await File.WriteAllTextAsync(Today, text.Replace("# Préparation du", "# Préparatiox du", StringComparison.Ordinal), Ct);
        File.SetLastWriteTimeUtc(Today, written);

        Assert.NotNull((await journal.GetAsync(Ct)).Preparation);
    }

    [Fact]
    public async Task Moves_to_the_next_file_at_midnight()
    {
        File.Copy(Demo, Today);
        JournalToday journal = Journal();
        Assert.True((await journal.GetAsync(Ct)).Found);

        _time.Advance(TimeSpan.FromDays(1));

        JournalTodayMessage tomorrow = await journal.GetAsync(Ct);
        Assert.False(tomorrow.Found);
        Assert.Equal(new DateOnly(2026, 11, 26), tomorrow.Day);
    }

    [Fact]
    public async Task Refuses_a_file_far_larger_than_a_journal_day()
    {
        await File.WriteAllTextAsync(Today, new string('x', JournalToday.MaxBytes + 1), Ct);

        JournalTodayMessage today = await Journal().GetAsync(Ct);

        Assert.True(today.Found);
        Assert.False(today.Available);
        Assert.Null(today.Preparation);
    }
}
