# Claude Code hooks (optional)

`settings.example.json` holds the `hooks` block that sends Claude Code's permission requests and notifications to Edge Hub, so they can be answered from the tablet.

It only matters once the tablet reaches the service (`Server:ListenOnNetwork` and a firewall rule for the Private profile, see `appsettings.example.json`). Until a screen is open on another device, every request gets no decision at once and Claude Code asks on the PC as usual.

## Install

1. Make the hooks' token, in your own terminal: `dotnet run --project src/EdgeHub -- --new-hook-token` (or `EdgeHub.exe --new-hook-token`). It prints the token and the hash to keep on the PC.
2. Keep the hash as the command says (`ClaudeCode:HookTokenHash`, in user secrets or `appsettings.Local.json`), then restart Edge Hub.
3. Merge the `hooks` block into your existing `%USERPROFILE%\.claude\settings.json`, with the token in place of `<hook token from --new-hook-token>` in the three `Authorization` headers. Do not replace the file. Claude Code picks the hooks up in the sessions already open.

If Edge Hub is stopped, if no screen is open on another device than the PC, or if nobody answers on the tablet in time, Claude Code asks on the PC as usual (terminal or VS Code): the service never decides on its own.

## Two tokens

The hooks' token sits in a file every agent on the PC can read, so it only lets a caller on the PC post to `/hooks/claude/*`. Answering a request needs the tablet's own token. Otherwise an agent could approve its own request:

- the PC keeps only a hash of the tablet's token. The token itself is shown once, by `--new-token` and outside Claude Code, and then lives only in the tablet's cookie: never in a config file, a log or a Claude Code session;
- Allow and Refuse are refused when they come from one of the PC's own addresses, since the tablet is never the PC. « Répondre sur le PC » is accepted from anywhere: it only hands the question back;
- the service refuses to start when the two hashes are the same, and a hook coming from another device is answered 404.

## What Edge Hub receives and keeps

Every hook sends the session id, the path of its transcript and the working folder. A permission request also sends the tool input (for `Write`, the whole file content; for a shell command, the full command), and `Stop` sends the last assistant message. The observed format is in [`docs/formats/claude-code-hooks.md`](../../../docs/formats/claude-code-hooks.md).

- Permission cards show the command in full, the working folder's name and the session's name (its `/rename`, else its title, else its folder). For a file, they show its path, never its content; for another tool, its whole input.
- `Notification` and `Stop` become short notices on the screen (« a besoin de toi », « a fini »), kept in memory for an hour, 20 at most. The last assistant message is never read, shown, forwarded or logged.
- Each decision taken on the tablet (allowed, refused, handed back to the PC) adds one line to `%LOCALAPPDATA%\EdgeHub\decisions.log` (`ClaudeCode:DecisionsFolder`): time, decision, session, folder, tool and command or file path. Nothing else of a request is written on disk, and the service's log only names the tool and the outcome.

## Sensitive requests

The tablet cannot allow a request that matches a rule of `ClaudeCode:SensitiveCommands`, or that touches a `.git` folder or reaches outside the working folder: its card is red, and only offers to refuse or to answer on the PC. The default rules: `git push --force` (and `-f`, `--force-with-lease`, `+branch`), `git reset --hard`, `git clean -f`, recursive deletes (`rm -r`, `Remove-Item -Recurse`, `del /s`, `rmdir /s`). A list in the settings replaces them; `.git` and the working folder are always checked. In doubt (a path it cannot read, an input too long to show whole), a request is sensitive.

## Timeouts

A request waits for the tablet `ClaudeCode:PermissionTimeoutSeconds` (60 seconds by default). The permission hook's own `timeout` is 75 seconds: keep it above that setting if you change it. If Claude Code stops waiting first (the question answered on the PC), the card leaves the tablet.
