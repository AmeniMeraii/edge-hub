using System.Text.Encodings.Web;
using System.Text.Json;

namespace EdgeHub.ClaudeHooks;

/// <summary>
/// The few fields Edge Hub reads from a hook's input (<c>docs/formats/claude-code-hooks.md</c>). Any shape is taken:
/// a field of the wrong type is simply missing. Nothing else is read, <c>last_assistant_message</c> least of all.
/// </summary>
public sealed record HookInput(
    string? SessionId,
    string? TranscriptPath,
    string? Cwd,
    string? ToolName,
    JsonElement? ToolInput,
    string? NotificationType)
{
    public const string TooLong = "trop long pour être lu en entier sur la tablette";
    public const string Unreadable = "demande sans contenu lisible";

    /// <summary>Beyond this, another tool's input is cut on the card, so the tablet may not allow it.</summary>
    public const int MaxSummaryLength = 2000;

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static HookInput From(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new HookInput(null, null, null, null, null, null);
        }

        return new HookInput(
            SensitiveRules.Text(root, "session_id"),
            SensitiveRules.Text(root, "transcript_path"),
            SensitiveRules.Text(root, "cwd"),
            SensitiveRules.Text(root, "tool_name"),
            root.TryGetProperty("tool_input", out JsonElement input) && input.ValueKind == JsonValueKind.Object ? input : null,
            SensitiveRules.Text(root, "notification_type"));
    }

    /// <summary>The card of a <c>PermissionRequest</c>: the command, or the file's path (never its content), or the whole input.</summary>
    public PermissionRequestInfo ToPermission(string session, SensitiveRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        string tool = ToolName ?? "?";
        Sensitivity sensitivity = rules.Check(ToolName, ToolInput, Cwd);
        string? folder = ProjectPaths.FolderName(Cwd);

        if (SensitiveRules.Text(ToolInput, "command") is { } command)
        {
            return Card(PermissionKind.Command, command, SensitiveRules.Text(ToolInput, "description"), sensitivity);
        }

        if ((SensitiveRules.Text(ToolInput, "file_path") ?? SensitiveRules.Text(ToolInput, "notebook_path")) is { } file)
        {
            return Card(PermissionKind.File, Shown(file), null, sensitivity);
        }

        if (ToolInput is not { } input)
        {
            return Card(PermissionKind.Other, "?", null, With(sensitivity, Unreadable));
        }

        string whole = JsonSerializer.Serialize(input, Compact);
        return whole.Length <= MaxSummaryLength
            ? Card(PermissionKind.Other, whole, null, sensitivity)
            : Card(PermissionKind.Other, whole[..MaxSummaryLength] + "…", null, With(sensitivity, TooLong));

        PermissionRequestInfo Card(PermissionKind kind, string summary, string? description, Sensitivity checks) =>
            new(session, tool, kind, summary, description, folder, checks.IsSensitive, checks.Reason, SessionId);
    }

    /// <summary>A file of the project by its path in the project; any other by its full path.</summary>
    private string Shown(string file)
    {
        if (Cwd is null || ProjectPaths.Full(file, Cwd) is not { } full)
        {
            return file;
        }

        return ProjectPaths.IsInside(full, Cwd) ? Path.GetRelativePath(Path.GetFullPath(Cwd), full) : full;
    }

    private static Sensitivity With(Sensitivity sensitivity, string reason) =>
        new(true, sensitivity.Reason is null ? reason : $"{sensitivity.Reason}, {reason}");
}
