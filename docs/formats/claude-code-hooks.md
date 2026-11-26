# Claude Code hooks: observed requests

Edge Hub receives three Claude Code hooks over HTTP (`integrations/claude-code/hooks/`): `PermissionRequest`, `Notification` and `Stop`. This page records what the [hooks documentation](https://code.claude.com/docs/en/hooks) says and what was **observed on real requests** (Claude Code 2.1, November 2026, two sessions, one in `auto` permission mode and one in `default`), captured to design phase 4c and then deleted.

All examples on this page are invented.

## Transport

- Claude Code POSTs the hook's input as JSON (`Content-Type: application/json`). The headers seen were `Accept`, `Accept-Encoding`, `Connection`, `Content-Length`, `Content-Type`, `Host`, `User-Agent`, plus the `Authorization` header set in the hook's configuration.
- A **2xx answer with an empty body means « no decision »**: Claude Code goes on as without the hook, and asks as usual. So do a non-2xx answer, a refused connection and a timeout (non-blocking errors). Edge Hub therefore never needs to answer to let Claude Code ask on the PC.
- A 2xx answer with a JSON body is read as the hook's output (below).
- Hook settings are picked up by a running session; a hook configured in `.claude/settings.local.json` only applies to sessions of that project.

## Fields common to every hook

| Field | Example | Used by Edge Hub |
|---|---|---|
| `hook_event_name` | `PermissionRequest`, `Notification`, `Stop` | which hook it is |
| `session_id` | `0f1e2d3c-…` | ties the request to a session, for its name |
| `transcript_path` | path of the session's `.jsonl` | to find the session's name; never shown |
| `cwd` | `C:\Users\demo\repo\bookshelf` | the folder shown on the card (`Notification` has it too) |
| `prompt_id`, `scratchpad_dir`, `effort` (`{"level": "high"}`) | | no |
| `permission_mode` | `default`, `auto`, `acceptEdits`… | no (absent from `Notification`) |

## PermissionRequest

Sent when Claude Code needs a permission decision, that is when it would show a permission question. A session in `auto` mode, or a command already allowed by the user's rules, sends none: read-only commands such as `whoami` or an allowed `dotnet --list-sdks` went through without a request, while writing a file or `mkdir` sent one.

```json
{
  "hook_event_name": "PermissionRequest",
  "session_id": "0f1e2d3c-…",
  "cwd": "C:\\Users\\demo\\repo\\bookshelf",
  "permission_mode": "default",
  "tool_name": "PowerShell",
  "tool_input": { "command": "mkdir exports", "description": "Create the exports folder" },
  "permission_suggestions": [
    { "type": "addDirectories", "directories": ["C:\\Users\\demo\\repo\\bookshelf"], "destination": "session" },
    { "type": "setMode", "mode": "acceptEdits", "destination": "session" }
  ]
}
```

- **Shell commands**: `tool_name` is `PowerShell` on this PC (`Bash` elsewhere), `tool_input` has `command` and `description` (written by the model).
- **Files**: `tool_name` `Write` had `tool_input` `file_path` and `content` (the whole file). `Edit` and the others were not observed; the reader must take any tool.
- `permission_suggestions` are the « always allow » choices Claude Code would offer. Edge Hub does not use them: the tablet answers for this request only.
- A `Notification` of type `permission_prompt` followed **6 seconds** after each request, while the hook was still waiting.

**Answering.** From the documentation:

```json
{ "hookSpecificOutput": { "hookEventName": "PermissionRequest", "decision": { "behavior": "allow" } } }
{ "hookSpecificOutput": { "hookEventName": "PermissionRequest", "decision": { "behavior": "deny", "message": "Refusé depuis la tablette" } } }
```

The user's `deny` rules still win over a hook that allows. `updatedInput` and `updatedPermissions` exist but are never sent by Edge Hub.

**While the hook waits**, the VS Code extension showed a waiting message. Whether the usual question is also shown in the meantime was not observed: the service must work either way. If the question is answered on the PC first, Claude Code is expected to drop the hook's request, and the card must leave the tablet.

## Notification

```json
{ "hook_event_name": "Notification", "session_id": "0f1e2d3c-…", "cwd": "C:\\Users\\demo\\repo\\bookshelf",
  "notification_type": "idle_prompt", "message": "Claude is waiting for your input" }
```

Observed types: `idle_prompt` (« Claude is waiting for your input », a minute after a turn ends) and `permission_prompt` (« Claude needs your permission »). The documentation lists more (`agent_needs_input`, `agent_completed`, `elicitation_dialog`…). No `title` was sent. The answer is ignored.

## Stop

Sent at the end of each turn. Besides the common fields: `stop_hook_active` (`false`), `background_tasks` (a list), `session_crons` (a list), and `last_assistant_message`, the text of Claude's last answer.

- Edge Hub never reads, shows, keeps or logs `last_assistant_message`.
- A `Stop` hook can keep Claude going (`{"decision": "block", …}`): Edge Hub always answers an empty 2xx.

## Privacy

A permission request carries the full command, or the full content of a file being written, and the session's paths. Edge Hub shows the tablet the command, the file path (never its content), the folder and the session's name, keeps nothing of them on disk except its log of the decisions taken from the tablet, and never logs a request.
