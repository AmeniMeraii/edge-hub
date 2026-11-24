# Claude Code session files: observed format

Claude Code does not document the format of its session files. This page records what was **observed on real files** (Claude Code 2.1.325 to 2.1.330, November 2026) before writing the session reader. It can change with any Claude Code update: the reader must stay tolerant and fail soft.

All examples on this page are invented.

## Where the files are

```
%USERPROFILE%\.claude\projects\
  <encoded project folder>\
    <sessionId>.jsonl                          one file per session
    <sessionId>\subagents\agent-<agentId>.jsonl   one file per subagent of that session
```

- **The folder name cannot be decoded reliably.** `:` and `\` are both replaced by `-`, so `C:\Users\<you>\repo\edge-hub` becomes `C--Users-<you>-repo-edge-hub`, and the hyphen in `edge-hub` is lost among the others. Read the `cwd` field inside the file instead.
- **The file name is the session id.** Every line that has a `sessionId` matches it.
- **Subagent files** use the parent's `sessionId`, and their lines carry `isSidechain: true` and an `agentId`.
- **Files can be large**: up to 28 MB was observed, with single lines of several hundred KB.

## Lines

Each line is one JSON object with a `type` field. 22 types were seen:

| Type | Has `timestamp` | Has `cwd`, `gitBranch` | Useful to Edge Hub |
|---|---|---|---|
| `user`, `assistant` | yes | yes | activity, start time, branch, edited files |
| `attachment`, `system` | yes | yes | no |
| `custom-title` | no | no | session name, when the user renamed the session |
| `ai-title` | no | no | session name, generated |
| `continued-in` | yes | no | maybe: points to the session that continues this one (`continuedInSessionId`) |
| `cost-state` | no | no | maybe: `totalLinesAdded`, `totalLinesRemoved`, but present in only 5 of 13 sessions |
| `api-request`, `api-request-blob`, `api-request-shape`, `queue-operation`, `file-history-delta`, `file-history-snapshot`, `last-prompt`, `mode`, `permission-mode`, `session-owner`, `atis-latch`, `agent-name`, `tools-used`, `data-flow-records` | some | no | no |

Timestamps are ISO 8601 in UTC with milliseconds: `2026-11-23T20:43:33.756Z`.

## Metadata Edge Hub needs

| Need | Source | Notes |
|---|---|---|
| Session name | last `custom-title` → `customTitle`, else last `ai-title` → `aiTitle` | Both are written several times per session: keep the last one. `/rename` appends a `custom-title` line at once, followed by an `agent-name` line with the same value; `ai-title` lines keep coming afterwards, so `customTitle` wins whatever the order. Only 1 session in 13 had a `customTitle`, and 1 had no title at all, so a fallback is needed. The `slug` field (`quiet-dancing-otter`) is a random name, not a title. |
| Start time | first `timestamp` of the file | |
| Project folder | `cwd` on `user` / `assistant` lines | Stable during a session. |
| Git branch | `gitBranch` on the **last** `user` / `assistant` line | Can change during a session. `HEAD` means no branch (repository without commits, or detached HEAD). |
| Edited files | distinct `toolUseResult.filePath` on `user` lines whose `tool_result` block is not `is_error` | Counts only edits that succeeded through `Edit`, `Write` and `NotebookEdit`. Files changed through a shell command are not counted. Do **not** count `tool_use` blocks across all lines: `api-request-blob` lines repeat them. |

## Is a session active?

The file's last write time **does not** mean the session is active. Claude Code keeps appending lines without a timestamp (titles, mode, last prompt…) long after the last message. On 13 sessions, the gap between the file's last write time and its last `user` / `assistant` line ranged from 0 minutes to 9 days.

Rule, validated (decision D10 in `docs/product/journal-des-decisions.md`):
1. Take the top-level `.jsonl` files (not the subagent ones) written in the last 15 minutes. This is a cheap first filter.
2. A session is active if its last `user` / `assistant` line is less than 15 minutes old. If several match, keep the most recent one.
3. Activity in a subagent file counts as activity of its parent session (for the buddy's "reading" state).

## Privacy

The files hold the whole conversation and more:

- message text, tool inputs and outputs (`message.content`, `toolUseResult`, `api-request-blob`);
- the last prompt (`last-prompt`) and queued prompts (`queue-operation`);
- backups of edited files (`file-history-*`);
- account identifiers (`session-owner`: `accountUuid`, `organizationUuid`).

So the reader:

- reads a fixed allowlist of fields (`type`, `timestamp`, `cwd`, `gitBranch`, `sessionId`, `isSidechain`, `customTitle`, `aiTitle`, `toolUseResult.filePath`) and ignores the rest;
- never sends anything but the metadata above to the tablet;
- is tested on invented fixtures only. Never copy a real line, even edited.

## Reading cost

Re-reading a 28 MB file every 30 seconds is wasteful. The reader should remember, for each file, the position it reached and the metadata found so far, and only parse the bytes appended since. Lines that fail to parse are skipped.

## Decisions

- **Session name.** Sessions are renamed after the ticket with `/rename`, every time. The reader shows `customTitle`, and falls back to `aiTitle`, then to the `cwd` folder name, for sessions that were not renamed.
