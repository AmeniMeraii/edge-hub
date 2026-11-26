using System.Text.Json;
using EdgeHub.ClaudeHooks;

namespace EdgeHub.Tests.ClaudeHooks;

/// <summary>Hook inputs shaped as observed in <c>docs/formats/claude-code-hooks.md</c>; every value is invented.</summary>
public sealed class HookInputTests
{
    private const string Project = @"C:\Users\demo\repo\bookshelf";

    private static readonly SensitiveRules Rules = new(SensitiveRules.Defaults);

    private static HookInput Read(object body) => HookInput.From(JsonSerializer.SerializeToElement(body));

    private static PermissionRequestInfo Permission(string tool, object toolInput) =>
        Read(new
        {
            hook_event_name = "PermissionRequest",
            session_id = "0f1e2d3c",
            transcript_path = @"C:\Users\demo\.claude\projects\C--Users-demo-repo-bookshelf\0f1e2d3c.jsonl",
            cwd = Project,
            permission_mode = "default",
            tool_name = tool,
            tool_input = toolInput,
        }).ToPermission("DEMO-101", Rules);

    [Fact]
    public void Reads_the_fields_common_to_every_hook()
    {
        HookInput input = Read(new
        {
            hook_event_name = "Notification",
            session_id = "0f1e2d3c",
            transcript_path = @"C:\t\0f1e2d3c.jsonl",
            cwd = Project,
            notification_type = "idle_prompt",
            message = "Claude is waiting for your input",
        });

        Assert.Equal(("0f1e2d3c", @"C:\t\0f1e2d3c.jsonl", Project, "idle_prompt"), (input.SessionId, input.TranscriptPath, input.Cwd, input.NotificationType));
    }

    [Fact]
    public void Takes_any_shape_without_failing()
    {
        HookInput input = HookInput.From(JsonSerializer.SerializeToElement(new object[] { 1, "two" }));
        Assert.Null(input.SessionId);

        input = Read(new { session_id = 12, cwd = new { path = Project }, tool_input = "mkdir x" });
        Assert.Equal((null, null, null), (input.SessionId, input.Cwd, (JsonElement?)input.ToolInput));
    }

    [Fact]
    public void A_shell_command_shows_the_command_and_its_description()
    {
        PermissionRequestInfo request = Permission("PowerShell", new { command = "git push origin main", description = "Push the branch" });

        Assert.Equal(
            ("DEMO-101", "PowerShell", PermissionKind.Command, "git push origin main", "Push the branch", "bookshelf", false, (string?)null, "0f1e2d3c"),
            (request.Session, request.Tool, request.Kind, request.Summary, request.Description, request.Folder, request.Sensitive, request.Reason, request.SessionId));
    }

    [Fact]
    public void A_risky_command_comes_with_its_reason()
    {
        PermissionRequestInfo request = Permission("Bash", new { command = "git push --force" });

        Assert.True(request.Sensitive);
        Assert.Equal("push forcé", request.Reason);
    }

    [Fact]
    public void A_file_shows_its_path_in_the_project_never_its_content()
    {
        PermissionRequestInfo request = Permission("Write", new { file_path = $@"{Project}\src\Export.cs", content = "class Export {}" });

        Assert.Equal((PermissionKind.File, @"src\Export.cs", (string?)null, false), (request.Kind, request.Summary, request.Description, request.Sensitive));
    }

    [Fact]
    public void A_file_outside_the_project_shows_its_full_path()
    {
        PermissionRequestInfo request = Permission("Edit", new { file_path = @"C:\Users\demo\.claude\settings.json", old_string = "a", new_string = "b" });

        Assert.Equal((PermissionKind.File, @"C:\Users\demo\.claude\settings.json", true), (request.Kind, request.Summary, request.Sensitive));
    }

    [Fact]
    public void Another_tool_shows_its_whole_input()
    {
        PermissionRequestInfo request = Permission("WebFetch", new { url = "https://example.test/doc", prompt = "Résume" });

        Assert.Equal((PermissionKind.Other, """{"url":"https://example.test/doc","prompt":"Résume"}""", false), (request.Kind, request.Summary, request.Sensitive));
    }

    [Fact]
    public void An_input_too_long_to_show_whole_cannot_be_allowed_from_the_tablet()
    {
        PermissionRequestInfo request = Permission("mcp__demo__save", new { text = new string('x', 3000) });

        Assert.Equal(2001, request.Summary.Length);
        Assert.EndsWith("…", request.Summary, StringComparison.Ordinal);
        Assert.True(request.Sensitive);
        Assert.Equal(HookInput.TooLong, request.Reason);
    }

    [Fact]
    public void A_request_without_tool_or_input_still_makes_a_card()
    {
        PermissionRequestInfo request = Read(new { session_id = "s" }).ToPermission("Claude Code", Rules);

        Assert.Equal(("?", PermissionKind.Other, "?", (string?)null, true), (request.Tool, request.Kind, request.Summary, request.Folder, request.Sensitive));
    }
}
