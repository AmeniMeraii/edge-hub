# Claude Code session files: observed format

Claude Code does not document the format of its session files. This page records what was **observed on real files** (13 sessions written by Claude Code 2.1.284 to 2.1.331, October and November 2026) before writing the session reader. It can change with any Claude Code update: the reader must stay tolerant and fail soft.

All examples on this page are invented.

## Where the files are

```
%USERPROFILE%\.claude\projects\
  <encoded project folder>\
    <sessionId>.jsonl                                  one file per session
    <sessionId>\custom-title.json                      the name given with /rename, when there is one
    <sessionId>\subagents\agent-<agentId>.jsonl        one file per subagent of that session
    <sessionId>\subagents\agent-<agentId>.meta.json    the subagent's settings (type, model…)
    <sessionId>\tool-results\                          saved tool outputs and screenshots
    memory\                                            Claude Code's memory for that project
```

- **The folder name cannot be decoded reliably.** Every character other than an ASCII letter or digit becomes `-`, accented letters included (checked on 13 of 13 sessions), and very long paths are shortened. So `C:\Users\<you>\repo\edge-hub` becomes `C--Users-<you>-repo-edge-hub`, and the hyphen in `edge-hub` is lost among the others. Read the `cwd` field inside the file instead.
- **The file name is the session id.** Every line that has a `sessionId` matches it.
- **Subagent files** use the parent's `sessionId`, and their lines carry `isSidechain: true` and an `agentId`.
- **Files can be large**: up to 77 MB was observed, with single lines up to 1.3 MB.

## Lines

Each line is one JSON object with a `type` field. 22 types were seen when this page was written, and new ones keep appearing (`pr-link`, `artifact-autoreact-ledger`…): the reader ignores the types it does not know.

| Type | Has `timestamp` | Has `cwd`, `gitBranch` | Useful to Edge Hub |
|---|---|---|---|
| `user`, `assistant` | yes | yes | activity, start time, branch, edited files |
| `attachment`, `system` | yes | yes | no |
| `custom-title` | no | no | session name, when the user renamed the session |
| `ai-title` | no | no | session name, generated |
| `continued-in` | yes | no | maybe: points to the session that continues this one (`continuedInSessionId`) |
| `cost-state` | no | no | maybe: `totalLinesAdded`, `totalLinesRemoved`, but present in only 5 of 13 sessions |
| `api-request`, `api-request-blob`, `api-request-shape`, `queue-operation`, `file-history-delta`, `file-history-snapshot`, `last-prompt`, `mode`, `permission-mode`, `session-owner`, `atis-latch`, `agent-name`, `tools-used`, `data-flow-records` | some | no | no |

Timestamps are ISO 8601 in UTC with milliseconds: `2026-01-15T09:30:00.000Z`.

## Metadata Edge Hub needs

| Need | Source | Notes |
|---|---|---|
| Session name | `<sessionId>\custom-title.json` → `customTitle` when the file exists; else the last `custom-title` line → `customTitle`; else the last `ai-title` line → `aiTitle` | The JSON file holds the same value as the last `custom-title` line and costs far less to read than a large session file. The lines are written several times per session: keep the last one. `/rename` appends a `custom-title` line at once, followed by an `agent-name` line with the same value; `ai-title` lines keep coming afterwards, so `customTitle` wins whatever the order. Only 2 sessions in 13 had a `customTitle` (one of them renamed while testing `/rename`), and 1 had no title at all, so a fallback is needed. The `slug` field (`quiet-dancing-otter`) is a random name, not a title. |
| Start time | first `timestamp` of the file | |
| Project folder | `cwd` on the **first** `user` / `assistant` line | `cwd` can change during a session (3 sessions in 13, up to 18 values in one). The first one matched the encoded folder name in 13 of 13 sessions, so it identifies the project; the last one only says where the session is now. |
| Git branch | `gitBranch` on the **last** `user` / `assistant` line | Can change during a session. `HEAD` means no branch (repository without commits, or detached HEAD). |
| Edited files | distinct `toolUseResult.filePath` on `user` lines whose `tool_result` block is not `is_error`, in the session file **and** its subagent files | Counts only edits that succeeded through `Edit` and `Write`; about 1 in 6 of them happen in subagents. `NotebookEdit` was never seen, so its result is unverified. Files changed through a shell command are not counted. Do **not** count `tool_use` blocks across all lines: `api-request-blob` lines repeat them. |

## Is a session active?

The file's last write time **does not** mean the session is active. Claude Code keeps appending lines without a timestamp (titles, mode, last prompt…) long after the last message. On 13 sessions, the gap between the file's last write time and its last `user` / `assistant` line ranged from 0 minutes to 9 days.

Rule validated in decision D10 (`docs/product/journal-des-decisions.md`). How subagents count was clarified after the review of PR #1 and is still to be confirmed:

1. Take the sessions whose file, or one of whose subagent files, was written in the last 15 minutes. This is a cheap first filter.
2. A session is active if its last `user` / `assistant` line, in its own file **or in one of its subagent files**, is less than 15 minutes old. A parent can stay silent for over 13 minutes while a subagent works. If several sessions match, keep the most recent one.
3. The same activity, within the last 2 minutes, drives the buddy's "reading" state.

## Privacy

The files hold the whole conversation and more:

- message text, tool inputs and outputs (`message.content`, `toolUseResult`, `api-request-blob`, the `tool-results` folder);
- the last prompt (`last-prompt`) and queued prompts (`queue-operation`);
- backups of edited files (`file-history-*`);
- account identifiers (`session-owner`: `accountUuid`, `organizationUuid`; `artifact-autoreact-ledger`: `accountUuid`);
- links to pull requests, which can name a work repository (`pr-link`: `prRepository`, `prUrl`);
- the task given to each subagent (`description` in `subagents\*.meta.json`).

So the reader:

- reads a fixed allowlist of fields (`type`, `timestamp`, `cwd`, `gitBranch`, `sessionId`, `isSidechain`, `customTitle`, `aiTitle`, `toolUseResult.filePath`, and `is_error` of the `tool_result` blocks in `message.content`) and ignores the rest. The top-level fields are read without building the whole line; only the `user` lines that carry a `toolUseResult` are parsed in full, in memory and for that line only, to read the file path and the error flag;
- sends the tablet only the session name, the branch, the time since the first message and the number of edited files. Full paths never leave the PC: they can contain client or project names. Two accepted exceptions, since they are the session's name: a generated title (`aiTitle`, written from the conversation) and, as the last fallback, the last folder name of the first `cwd`;
- is tested on invented fixtures only. Never copy a real line, even edited.

## Reading cost

Re-reading a 77 MB file every 30 seconds is wasteful. The reader should remember, for each file, the position it reached and the metadata found so far, and only parse the bytes appended since. Lines that fail to parse are skipped.

## Decisions

- **Session name.** Sessions are renamed after the ticket with `/rename`, every time. The reader shows `customTitle`, and falls back to `aiTitle`, then to the last folder name of the first `cwd` (never the full path), for sessions that were not renamed.
