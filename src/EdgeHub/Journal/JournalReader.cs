using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace EdgeHub.Journal;

/// <summary>
/// One day of the journal: whether its file exists, whether it could be read, when it was written, what it holds, and
/// whether the journal folder itself exists.
/// </summary>
public sealed record JournalFile(bool Found, bool Available, DateTimeOffset? WrittenAt, JournalDay Day, bool FolderExists = true)
{
    public static JournalFile Missing { get; } = new(false, true, null, JournalDay.Empty);

    public static JournalFile NoFolder { get; } = new(false, true, null, JournalDay.Empty, FolderExists: false);
}

/// <summary>
/// Reads a day of the journal, <c>AAAA-MM-JJ.md</c> as the Claude Code commands name it. A file is read again only when
/// its write time or its size changes; the last two weeks of files are remembered.
/// </summary>
public sealed class JournalReader(IOptionsMonitor<JournalOptions> options)
{
    /// <summary>A day of the journal is a few kilobytes: far more is not a journal.</summary>
    public const int MaxBytes = 1024 * 1024;

    private const int Remembered = 14;

    private readonly ConcurrentDictionary<string, ReadFile> _read = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => options.CurrentValue.ResolvedFolder() is not null;

    /// <summary>The day's file, or null when no journal folder is set.</summary>
    public async Task<JournalFile?> ReadAsync(DateOnly day, CancellationToken cancellationToken)
    {
        if (options.CurrentValue.ResolvedFolder() is not { } folder)
        {
            return null;
        }

        var file = new FileInfo(Path.Combine(folder, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md"));
        if (!file.Exists)
        {
            return Directory.Exists(folder) ? JournalFile.Missing : JournalFile.NoFolder;
        }

        var writtenAt = new DateTimeOffset(file.LastWriteTimeUtc);
        if (file.Length > MaxBytes)
        {
            return new JournalFile(true, false, writtenAt, JournalDay.Empty);
        }

        if (_read.TryGetValue(file.FullName, out ReadFile? last) && last.WrittenAt == writtenAt && last.Length == file.Length)
        {
            return new JournalFile(true, true, writtenAt, last.Day);
        }

        JournalDay parsed;
        try
        {
            parsed = JournalParser.Parse(await ReadTextAsync(file.FullName, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Being written by a command, or locked: the next request reads it.
            return new JournalFile(true, false, writtenAt, JournalDay.Empty);
        }

        _read[file.FullName] = new ReadFile(day, writtenAt, file.Length, parsed);
        foreach (string old in _read.OrderByDescending(r => r.Value.Date).Skip(Remembered).Select(r => r.Key).ToList())
        {
            _read.TryRemove(old, out _);
        }

        return new JournalFile(true, true, writtenAt, parsed);
    }

    private static async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        // Shared with the command that may be writing it.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record ReadFile(DateOnly Date, DateTimeOffset WrittenAt, long Length, JournalDay Day);
}
