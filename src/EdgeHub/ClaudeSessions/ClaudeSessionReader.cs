using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EdgeHub.ClaudeSessions;

/// <summary>
/// The active Claude Code session, as the screen shows it: never a path, never a line of the conversation. The name
/// is the <c>/rename</c> (the ticket), else the generated title, else the project folder's name.
/// </summary>
public sealed record ActiveSession(string Name, string? Branch, DateTimeOffset? StartedAt, DateTimeOffset LastActivityAt, int EditedFiles);

public interface IClaudeSessionReader
{
    /// <summary>The active session, or null when there is none (or no projects folder).</summary>
    ActiveSession? FindActive();

    /// <summary>
    /// The name of the session a hook comes from, at once: its <c>/rename</c> file, else the titles already read by
    /// <see cref="FindActive"/>, else its folder's name. <paramref name="transcriptPath"/> is used only inside the
    /// projects folder.
    /// </summary>
    string NameOf(string? transcriptPath, string? cwd);
}

/// <summary>
/// Finds the active session with the rule of decision D10 (<c>docs/formats/claude-code-sessions.md</c>): among the
/// sessions whose file, or one of their subagents' files, was written in the last 15 minutes, the one whose last
/// message, subagents included, is the most recent and less than 15 minutes old. Files are read incrementally and
/// forgotten an hour after their session stops being a candidate.
/// </summary>
public sealed partial class ClaudeSessionReader(IOptionsMonitor<ClaudeCodeOptions> options, TimeProvider time, ILogger<ClaudeSessionReader> logger)
    : IClaudeSessionReader
{
    public static readonly TimeSpan ActiveFor = TimeSpan.FromMinutes(15);

    private const int MaxNameLength = 120;
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(1);

    // Concurrent: the hooks look up names while the monitor reads the sessions.
    private readonly ConcurrentDictionary<string, SessionFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public string NameOf(string? transcriptPath, string? cwd)
    {
        if (SessionFileInProjects(transcriptPath) is { } main)
        {
            _files.TryGetValue(main, out SessionFile? read);
            string sessionFolder = Path.Combine(Path.GetDirectoryName(main)!, Path.GetFileNameWithoutExtension(main));
            if ((CustomTitleFile(sessionFolder) ?? read?.CustomTitle ?? read?.AiTitle) is { } title)
            {
                return Shorten(title);
            }
        }

        return Shorten(FolderName(cwd) ?? "Claude Code");
    }

    public ActiveSession? FindActive()
    {
        string? root = options.CurrentValue.ResolvedFolder();
        if (root is null || !Directory.Exists(root))
        {
            return null;
        }

        DateTimeOffset now = time.GetUtcNow();
        ActiveSession? best = null;
        foreach (string project in Directory.EnumerateDirectories(root))
        {
            IEnumerable<string> mains;
            try
            {
                mains = Directory.EnumerateFiles(project, "*.jsonl").ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A project folder that cannot be listed: the others are still read.
                LogUnreadable(logger);
                continue;
            }

            foreach (string main in mains)
            {
                ActiveSession? session;
                try
                {
                    session = Read(main, now);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    // Whatever goes wrong with one session (its folder, its name file…), the others are still read.
                    LogUnreadable(logger);
                    continue;
                }

                if (session is not null && (best is null || session.LastActivityAt > best.LastActivityAt))
                {
                    best = session;
                }
            }
        }

        foreach (string forgotten in _files.Where(f => now - f.Value.LastNeeded > ForgetAfter).Select(f => f.Key).ToList())
        {
            _files.TryRemove(forgotten, out _);
        }

        return best;
    }

    private ActiveSession? Read(string main, DateTimeOffset now)
    {
        string sessionFolder = Path.Combine(Path.GetDirectoryName(main)!, Path.GetFileNameWithoutExtension(main));
        string subagentsFolder = Path.Combine(sessionFolder, "subagents");
        List<string> paths = [main, .. Directory.Exists(subagentsFolder) ? Directory.EnumerateFiles(subagentsFolder, "agent-*.jsonl") : []];

        // Cheap first filter: the write time of a file says nothing of the last message, but an old one rules it out.
        if (!paths.Any(path => now - File.GetLastWriteTimeUtc(path) < ActiveFor))
        {
            return null;
        }

        var files = new List<SessionFile>();
        foreach (string path in paths)
        {
            if (!_files.TryGetValue(path, out SessionFile? file))
            {
                file = new SessionFile(path);
                _files[path] = file;
            }

            file.LastNeeded = now;
            try
            {
                file.ReadNewLines();
                files.Add(file);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Being written or moved, or anything unexpected: this file waits for the next pass, the others go on.
                // The path is not logged.
                LogUnreadable(logger);
            }
        }

        SessionFile? session = files.FirstOrDefault(f => f.Path == main);
        DateTimeOffset? last = files.Max(f => f.LastActivity);
        if (session is null || last is not { } lastActivity || now - lastActivity >= ActiveFor)
        {
            return null;
        }

        string name = CustomTitleFile(sessionFolder) ?? session.CustomTitle ?? session.AiTitle ?? FolderName(session.FirstCwd) ?? "Claude Code";
        return new ActiveSession(
            Shorten(name),
            session.LastBranch is null or "HEAD" ? null : session.LastBranch,
            session.FirstTimestamp,
            lastActivity,
            files.SelectMany(f => f.EditedFiles).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>At most <see cref="MaxNameLength"/> characters, without cutting an emoji in half.</summary>
    private static string Shorten(string name)
    {
        if (name.Length <= MaxNameLength)
        {
            return name;
        }

        int cut = char.IsHighSurrogate(name[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
        return name[..cut].TrimEnd();
    }

    /// <summary>The name given with <c>/rename</c>, kept next to the session file: far cheaper than the session itself.</summary>
    private static string? CustomTitleFile(string sessionFolder)
    {
        string path = Path.Combine(sessionFolder, "custom-title.json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("customTitle", out JsonElement title)
                && title.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(title.GetString())
                ? title.GetString()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // Half an emoji in the name is enough to make it unreadable: the session keeps its other names.
            return null;
        }
    }

    /// <summary>The last folder name of the project, never the full path: it can name a client.</summary>
    private static string? FolderName(string? cwd) =>
        string.IsNullOrWhiteSpace(cwd) ? null : Path.GetFileName(cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : null;

    /// <summary>The session file a hook names, as a full path, or null when it is not a session file of the projects folder.</summary>
    private string? SessionFileInProjects(string? transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || options.CurrentValue.ResolvedFolder() is not { } root)
        {
            return null;
        }

        try
        {
            string full = Path.GetFullPath(transcriptPath);
            return full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && full.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                    ? full
                    : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fichier de session illisible pour le moment")]
    private static partial void LogUnreadable(ILogger logger);
}
