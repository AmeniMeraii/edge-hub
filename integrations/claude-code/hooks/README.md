# Claude Code hooks (optional)

`settings.example.json` holds the `hooks` block that sends Claude Code's permission requests and notifications to Edge Hub, so they can be answered from the tablet.

**Do not install it yet.** It is checked and finished in phase 4c. Until then Edge Hub does not serve these routes, and every event would only log a hook failure.

## Install (from phase 4c)

1. Merge the `hooks` block into your existing `%USERPROFILE%\.claude\settings.json`. Do not replace the file.
2. Set the `EDGEHUB_HOOK_TOKEN` environment variable for your user, with the hook token Edge Hub gives you.
3. Restart Claude Code, and VS Code too when Claude Code runs inside it: a new environment variable only reaches programs started after it is set.

If Edge Hub is stopped, or nobody answers on the tablet in time, Claude Code asks on the PC as usual (terminal or VS Code): the service never decides on its own.

## Two tokens (planned, to be confirmed in phase 4c)

Every environment variable Claude Code can read is also readable by the commands it runs. So `EDGEHUB_HOOK_TOKEN` only lets a caller post to `/hooks/claude/*`. Answering a pending request needs the tablet's own token. Otherwise an agent could approve its own request. Keeping that token out of Claude Code's environment is not enough, since an agent can also read files on the PC:

- the PC keeps only a hash of the tablet token. The token itself is shown once, at install time and outside Claude Code, and then lives only in the tablet's cookie: never in a config file, a log or a Claude Code session;
- the routes that allow or deny a request refuse calls coming from the PC's own addresses, since the tablet is never the PC.

## What Edge Hub receives and keeps (planned, phase 4c)

Every hook sends the session id, the path of its transcript and the working folder. A permission request also sends the tool input (for `Write`, the whole file content; for a shell command, the full command), and `Stop` sends the last assistant message.

- `Stop`: Edge Hub keeps only the session name. The last assistant message is never shown, forwarded or logged.
- Permission cards show the command, the working folder and the session name. For `Write` and `Edit`, they show the file path, not its content.
- The real requests captured to design phase 4c are kept under `/local/` (ignored by git) or outside the repository, and deleted afterwards.

## Timeouts

The permission hook waits up to 75 seconds. Keep it above `ClaudeCode:PermissionTimeoutSeconds` (60 seconds by default) if you change that setting.
