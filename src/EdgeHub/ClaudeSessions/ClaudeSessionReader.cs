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

    private readonly Dictionary<string, SessionFile> _files = new(StringComparer.OrdinalIgnoreCase);

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
            foreach (string main in Directory.EnumerateFiles(project, "*.jsonl"))
            {
                if (Read(main, now) is { } session && (best is null || session.LastActivityAt > best.LastActivityAt))
                {
                    best = session;
                }
            }
        }

        foreach (string forgotten in _files.Where(f => now - f.Value.LastNeeded > ForgetAfter).Select(f => f.Key).ToList())
        {
            _files.Remove(forgotten);
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
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Being written or moved: the next pass reads it. The path is not logged.
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
            name.Length > MaxNameLength ? name[..MaxNameLength] : name,
            session.LastBranch is null or "HEAD" ? null : session.LastBranch,
            session.FirstTimestamp,
            lastActivity,
            files.SelectMany(f => f.EditedFiles).Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The last folder name of the project, never the full path: it can name a client.</summary>
    private static string? FolderName(string? cwd) =>
        string.IsNullOrWhiteSpace(cwd) ? null : Path.GetFileName(cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fichier de session illisible pour le moment")]
    private static partial void LogUnreadable(ILogger logger);
}
