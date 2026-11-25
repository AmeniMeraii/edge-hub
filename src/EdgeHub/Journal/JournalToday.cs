using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace EdgeHub.Journal;

/// <summary>
/// What <c>/api/journal/today</c> answers. <see cref="Found"/> is false when the day has no file yet (no
/// <c>/prep-reunions</c> and no <c>/rapport</c> today); <see cref="Available"/> is false when the file exists but cannot
/// be read. <see cref="Day"/> lets the screen drop the data of another day.
/// </summary>
public sealed record JournalTodayMessage(
    bool Configured,
    bool Available,
    DateOnly? Day,
    bool Found,
    DateTimeOffset? WrittenAt,
    Preparation? Preparation,
    IReadOnlyList<MeetingDecision> Decisions)
{
    public static JournalTodayMessage NotConfigured { get; } = new(false, false, null, false, null, null, []);
}

/// <summary>
/// Reads today's journal file, <c>AAAA-MM-JJ.md</c> after the PC's local date as the Claude Code commands name it. The
/// file is read again only when its write time or its size changes.
/// </summary>
public sealed class JournalToday(IOptionsMonitor<JournalOptions> options, TimeProvider time)
{
    /// <summary>A day of the journal is a few kilobytes: far more is not a journal.</summary>
    public const int MaxBytes = 1024 * 1024;

    private readonly Lock _gate = new();
    private ReadFile? _last;

    public async Task<JournalTodayMessage> GetAsync(CancellationToken cancellationToken)
    {
        if (options.CurrentValue.ResolvedFolder() is not { } folder)
        {
            return JournalTodayMessage.NotConfigured;
        }

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), time.LocalTimeZone).DateTime);
        var file = new FileInfo(Path.Combine(folder, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md"));
        if (!file.Exists)
        {
            return new JournalTodayMessage(true, true, today, false, null, null, []);
        }

        var writtenAt = new DateTimeOffset(file.LastWriteTimeUtc);
        if (file.Length > MaxBytes)
        {
            return new JournalTodayMessage(true, false, today, true, writtenAt, null, []);
        }

        ReadFile? last;
        lock (_gate)
        {
            last = _last;
        }

        JournalDay day;
        if (last is not null && last.Path == file.FullName && last.WrittenAt == writtenAt && last.Length == file.Length)
        {
            day = last.Day;
        }
        else
        {
            try
            {
                day = JournalParser.Parse(await ReadAsync(file.FullName, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Being written by a command, or locked: the next request reads it.
                return new JournalTodayMessage(true, false, today, true, writtenAt, null, []);
            }

            lock (_gate)
            {
                _last = new ReadFile(file.FullName, writtenAt, file.Length, day);
            }
        }

        return new JournalTodayMessage(true, true, today, true, writtenAt, day.Preparation, day.Decisions);
    }

    private static async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        // Shared with the command that may be writing it.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record ReadFile(string Path, DateTimeOffset WrittenAt, long Length, JournalDay Day);
}
