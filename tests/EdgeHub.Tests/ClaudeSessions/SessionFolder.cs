using System.Text.Json;

namespace EdgeHub.Tests.ClaudeSessions;

/// <summary>
/// An invented <c>.claude\projects</c> folder in the temp directory, laid out as observed in
/// <c>docs/formats/claude-code-sessions.md</c>. Never copy a real session line here.
/// </summary>
internal sealed class SessionFolder : IDisposable
{
    public const string Cwd = @"C:\Users\demo\repo\bookshelf";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("edgehub-claude-");

    public string Root => _root.FullName;

    public void Dispose() => _root.Delete(recursive: true);

    /// <summary>Writes lines to a session file (or a subagent's) and sets its write time.</summary>
    public string Write(string sessionId, DateTimeOffset writtenAt, IEnumerable<string> lines, string? subagent = null, bool append = false)
    {
        string project = Directory.CreateDirectory(Path.Combine(Root, "C--Users-demo-repo-bookshelf")).FullName;
        string path = subagent is null
            ? Path.Combine(project, sessionId + ".jsonl")
            : Path.Combine(Directory.CreateDirectory(Path.Combine(project, sessionId, "subagents")).FullName, $"agent-{subagent}.jsonl");
        string text = string.Concat(lines.Select(line => line + "\n"));
        if (append)
        {
            File.AppendAllText(path, text);
        }
        else
        {
            File.WriteAllText(path, text);
        }

        File.SetLastWriteTimeUtc(path, writtenAt.UtcDateTime);
        return path;
    }

    public void WriteCustomTitleFile(string sessionId, string title)
    {
        string folder = Directory.CreateDirectory(Path.Combine(Root, "C--Users-demo-repo-bookshelf", sessionId)).FullName;
        File.WriteAllText(Path.Combine(folder, "custom-title.json"), JsonSerializer.Serialize(new { customTitle = title }));
    }

    public static string User(DateTimeOffset at, string branch = "feature/DEMO-101", string cwd = Cwd, string text = "Peux-tu relire l'export ?") =>
        JsonSerializer.Serialize(new { type = "user", timestamp = Stamp(at), cwd, gitBranch = branch, sessionId = "s", message = new { role = "user", content = text } });

    public static string Assistant(DateTimeOffset at, string branch = "feature/DEMO-101", string cwd = Cwd) =>
        JsonSerializer.Serialize(new { type = "assistant", timestamp = Stamp(at), cwd, gitBranch = branch, sessionId = "s", message = new { role = "assistant", content = new[] { new { type = "text", text = "Je regarde." } } } });

    /// <summary>The result of an Edit or Write, as a <c>user</c> line carrying <c>toolUseResult.filePath</c>.</summary>
    public static string Edited(DateTimeOffset at, string filePath, bool error = false) =>
        JsonSerializer.Serialize(new
        {
            type = "user",
            timestamp = Stamp(at),
            cwd = Cwd,
            gitBranch = "feature/DEMO-101",
            sessionId = "s",
            message = new { role = "user", content = new object[] { new { type = "tool_result", tool_use_id = "toolu_demo", content = "ok", is_error = error } } },
            toolUseResult = new { filePath, oldString = "a", newString = "b" },
        });

    public static string CustomTitle(string title) => JsonSerializer.Serialize(new { type = "custom-title", customTitle = title, sessionId = "s" });

    public static string AiTitle(string title) => JsonSerializer.Serialize(new { type = "ai-title", aiTitle = title, sessionId = "s" });

    /// <summary>A line Claude Code keeps appending after the last message, without a timestamp.</summary>
    public static string LastPrompt() => JsonSerializer.Serialize(new { type = "last-prompt", lastPrompt = "relis l'export", sessionId = "s" });

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
