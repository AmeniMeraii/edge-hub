# Edge Hub

A small ASP.NET Core service that runs on a Windows PC and turns a spare Android tablet into an always-on side screen: focus timer, today's agenda, meeting prep, Claude Code status, Spotify controls and light PC stats. The tablet only needs a browser.

> **Work in progress.** Today the PC serves the current screen, reads today's calendar for the sport line and the next event, and streams its stats and the current track (the screen does not show those yet). Features, screenshots and setup instructions will come with the first release.

## Build

Requirements: Windows 10 (build 19041) or later, [.NET 10 SDK](https://dotnet.microsoft.com/download) 10.0.402 or a later 10.0.4xx patch (see `global.json`).

```powershell
dotnet build
dotnet test
```

## Run the service

Give the service an access token once, outside git: 32 characters or more, only letters, digits, `-` and `_`, since it travels in an address. This PowerShell line prints a suitable random one:

```powershell
$bytes = New-Object byte[] 24; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
```

During development, keep it in user secrets, then run the service:

```powershell
dotnet user-secrets set Security:Token "<token>" --project src/EdgeHub
dotnet run --project src/EdgeHub
```

A published service reads `appsettings.Local.json` next to `EdgeHub.exe` instead (user secrets only exist in development). `src/EdgeHub/appsettings.example.json` explains every setting, including the one that opens the service to the tablet; by default it only answers on this PC.

Open `http://<PC address>:5080/?k=<token>` once on the screen: the token moves into a cookie and the address is cleaned. The page then receives the PC's stats and the current track every second from `/api/stream`. Prefer opening it on the tablet rather than in the PC's own browser: cookies are not tied to a port, so a browser on the PC would also send this one to other local development servers.

## Calendar

The sport line and the « Ensuite » line under the clock come from your calendar's secret iCal address (Google Calendar: Settings and sharing, Integrate calendar). Keep it with the token, outside git:

```powershell
dotnet user-secrets set Calendar:IcalUrl "<secret iCal address>" --project src/EdgeHub
```

The PC downloads it when the screen asks, at most every 5 minutes, or every minute after a failed download, and never writes the address in its logs. When a download fails, it keeps serving the last readable copy for up to a day, and the screen shows when that copy was read. Recurring events are unfolded with their moved, excluded or cancelled occurrences, in the `Calendar:TimeZone` time zone (Europe/Paris by default). A session is recognised from its title: 🏃, « course », « footing » or « run » for a run, 💪, « muscu », « full body » or « salle » for strength, 🚶 or « marche » for a walk; a title that starts with ❌ or contains « annul » is a cancelled session. As on the original screen, « course », « run », « salle » and « marche » are whole words (« Faire les courses » is not a run), while « footing », « muscu » and « full body » also match the start of a longer word (« Footings », « Musculation »): in the settings, a keyword ending with `*` matches the start of a word. Case does not matter; emojis match anywhere. The keywords can be changed in `Calendar:Sport` (see `appsettings.example.json`). One broken event in the calendar is skipped without hiding the others. Without an address, the screen keeps the manual choice.

## Meetings page

Swipe left on the screen, or touch the dots at the bottom, for the meetings page (`#reunions` in the address opens it directly). It shows the morning's preparation from the activity journal: one line per ticket for the daily (done, remaining, testable), the technical choices to explain and the points to raise, in orange, and the state of the previous decisions. Without a preparation for the day, it says « Lance /prep-reunions dans Claude Code », and still shows the day's meeting decisions if there are any.

The journal is written by the optional Claude Code commands in `integrations/claude-code/commands/` (`/rapport`, `/prep-reunions`, `/decisions`) to `%USERPROFILE%\Documents\Journal-activite\YYYY-MM-DD.md`; `Journal:Folder` changes the folder. The PC reads the day's file again as soon as it changes, and the screen asks every 30 seconds. The format is described in [`docs/formats/journal.md`](docs/formats/journal.md).

The top of the page shows the active Claude Code session: its name, its branch, how long it has been going and how many files it edited. The same name fills « Une seule chose » on the first page when it is empty and nothing is being typed there; a task typed by hand is never replaced. A renamed session keeps its task and its time, a task finished or replaced there does not come back for that session, and a task from a session goes away once the screen has not seen its session active for 10 minutes, whether the PC answers or not. The screen follows the most recently active session: with two sessions at once, the task stays with the first one while it keeps being seen. Name your sessions after their ticket with `/rename`. The PC finds the session in `%USERPROFILE%\.claude\projects` (`ClaudeCode:ProjectsFolder`) every 30 seconds: a session is active while its last message, subagents included, is less than 15 minutes old. It only reads metadata, never the conversation, and only that metadata reaches the screen ([`docs/formats/claude-code-sessions.md`](docs/formats/claude-code-sessions.md)). An empty folder setting turns either part off.

During a focus session, or in the 30 minutes before a sport session, the screen goes back to the first page after a minute without being touched.

## My week

The top of the meetings page shows « Ma semaine », the indicators of [`docs/product/indicateurs.md`](docs/product/indicateurs.md) for the current week, Monday to today, from `/api/metrics/week`:

- **days held**, the main indicator: work days whose preparation was done and where each task finished with « Terminé » in « Une seule chose » has a report (`/rapport`) the same day whose ticket is a whole word of the task (« [DEMO-101] Export », « J-3 : livrer DEMO-101 »), or, for a task without a ticket, with the same title;
- **preparation before the daily**: the PC notes the first time it sees the day's preparation in the journal, and compares it with `Metrics:DailyTime` (for example `09:30`), out of the days it can tell: a preparation already there when the service first reads the journal came at an unknown time. Without that setting, only the days with a preparation are counted;
- **days with a report**, out of the work days with a journal file or an event, **focus sessions** completed, **sport sessions** done among those planned in the calendar so far, and **pause reminders** followed within 10 minutes by a pause, a finished task or a change of task. The screen only reminds, and counts, during the work hours set in « Journée », on weekdays.

Today enters a ratio, on both sides, once it is settled: when it scores, or when it can no longer (after the daily time for the preparation, after the session's time for sport). A day not over yet is not a day missed, and a ratio over no day yet shows « — ».

The screen sends what it notices (focus completed, pause started, pause reminder, task finished or changed, sport session done) to `POST /api/events`, each with an id so that an event sent twice is kept once; events it could not send wait on the tablet and leave later with their time. The PC appends them, with its own `preparation-seen`, to `%LOCALAPPDATA%\EdgeHub\events.jsonl`, one JSON object per line (`at`, `type`, `id`, and `task`, at most 200 characters, for a finished task). Nothing leaves the PC and the tablet. `Metrics:Enabled` set to `false`, or an empty `Metrics:Folder`, turns the measure and the tile off.

## Logs

The service writes its logs to the console and to one file per day, `%LOCALAPPDATA%\EdgeHub\logs\edgehub-YYYYMMDD.log`, starting a new file past 10 MB and deleting files older than 14 days; past 100 files (1 GB), the oldest go first whatever their age. `Logs:Folder` and `Logs:RetainedDays` (1 to 90 days) change that, and an empty folder turns the file off (see `appsettings.example.json`). The `Logging` settings choose what is written, for the file as for the console, but request addresses never reach either, whatever those settings say: the first one carries the token. If the folder cannot be written to when the service starts, the console says so once and the service runs without a log file.

## Console mode

Shows CPU, memory, network and disk usage and the current track, refreshed every second:

```powershell
dotnet run --project src/EdgeHub -- --console
```

Keys: `Space` play/pause, `N` next, `P` previous, `Q` or `Esc` quit. Keys can also be piped in as characters (`' '`, `n`, `p`, `q`); piped input does not end the console, so finish with `q`. The track comes from Windows' media controls, so Spotify's desktop app works without its Web API or a Premium account.

## License

[MIT](LICENSE). The screen's fonts, Chakra Petch and IBM Plex Sans, are served by the PC from `src/EdgeHub/wwwroot/fonts` and keep their own license, the SIL Open Font License 1.1 (license files next to them): the screen loads nothing from the internet. The IBM Plex Sans files are IBM's own, unchanged, since the « Plex » name is reserved to them.
