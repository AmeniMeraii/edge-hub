# Claude Code hooks (optional)

`settings.example.json` holds the `hooks` block that sends Claude Code's permission requests and notifications to Edge Hub, so they can be answered from the tablet.

**Do not install it yet.** It is checked and finished in phase 4c. Until then Edge Hub does not serve these routes, and every event would only log a hook failure.

## Install (from phase 4c)

1. Merge the `hooks` block into your existing `%USERPROFILE%\.claude\settings.json`. Do not replace the file.
2. Set the `EDGEHUB_HOOK_TOKEN` environment variable for your user, with the hook token Edge Hub gives you.
3. Restart Claude Code.

If Edge Hub is stopped, or nobody answers on the tablet in time, Claude Code asks in the terminal as usual: the service never decides on its own.

## Two tokens (planned, to be confirmed in phase 4c)

Every environment variable Claude Code can read is also readable by the commands it runs. So `EDGEHUB_HOOK_TOKEN` only lets a caller post to `/hooks/claude/*`. Answering a pending request needs the tablet's own token, which never goes into Claude Code's environment. Otherwise an agent could approve its own request.

## Timeouts

The permission hook waits up to 75 seconds. Keep it above `ClaudeCode:PermissionTimeoutSeconds` (60 seconds by default) if you change that setting.
