# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- « Ma semaine » on the meetings page, from `/api/metrics/week`: days held (preparation done and each finished task reported with the same ticket), preparation before the daily (`Metrics:DailyTime`), reports, focus sessions, sport sessions done out of those planned, pause reminders followed. The screen sends its events to `POST /api/events`, kept in `%LOCALAPPDATA%\EdgeHub\events.jsonl` and nowhere else, and keeps those it could not send for later; the PC notes the first time it sees the day's preparation. `Metrics:Enabled` turns it all off.
- Meetings page, reached by swiping or with the dots at the bottom: the morning's preparation from the activity journal (daily per ticket, technical choices, points to raise, state of the previous decisions) and the day's meeting decisions, from `/api/journal/today`; « Lance /prep-reunions dans Claude Code » without a preparation. The day's file is read again as soon as it changes.
- Active Claude Code session (`/api/claude/current`): name, branch, start and number of edited files, found every 30 seconds with the rule of decision D10 and read incrementally, metadata only. It fills « Une seule chose » when it is empty, never over a task typed by hand, keeps its task when renamed, and heads the meetings page.
- The screen remembers its page, opens the meetings page with `#reunions`, and goes back to the first page during a focus session or in the 30 minutes before a sport session.
- Log file: one file per day under `%LOCALAPPDATA%\EdgeHub\logs`, kept 14 days, 1 GB at most; request addresses, which carry the token, never reach it nor the console, whatever the `Logging` settings. `Logs:Folder` and `Logs:RetainedDays` in the settings.
- Calendar: today's events from the secret iCal address, recurring events unfolded with their exceptions, in the configured time zone; downloaded when the screen asks, at most every 5 minutes (every minute after a failure), the last readable copy kept for up to a day and its read time shown, a broken event skipped without hiding the others, the address never logged. `/api/calendar/today` gives the day's events and the sport session to show.
- Sport sessions recognised from calendar titles (run, strength, walk, cancelled) with keywords in the settings, whole words or word starts (`*`) as on the original screen; the screen's sport line and « Ensuite » line now come from the PC instead of the claude.ai connector, and the screen drops the calendar data of a previous day.
- Service: `/api/stream` sends the PC's stats and the current track every second as Server-Sent Events, to any number of screens. The adapter's name and the drives stay on the PC.
- Access token for every `/api/*` route, given once in the address (`?k=`) and then kept in a cookie that each call renews. Calls that change something must come from the screen itself. Configuration checked at startup.
- The screen is served by the PC from `wwwroot`, with a web app manifest (full screen, landscape) and a « PC hors ligne » indicator; it reconnects on its own, and says when the token is refused.
- Console mode (`EdgeHub.exe --console`): CPU, memory, network and disk usage, current track from Windows' media controls, play/pause, next and previous from the keyboard.
- Solution skeleton: ASP.NET Core service (`src/EdgeHub`) with a `/health` route, xUnit test project.
- Shared build settings: nullable reference types, .NET analyzers, warnings treated as errors.
- Continuous integration on Windows (restore, build, test) for every pull request and every push to main.
- Optional Claude Code integration: `/rapport`, `/prep-reunions` and `/decisions` commands, example hooks configuration with its install notes.
- The original single-file screen, imported unchanged and now served from `src/EdgeHub/wwwroot/index.html`.
- Observed format of Claude Code session files, as groundwork for the session reader (`docs/formats/claude-code-sessions.md`).

### Changed
- The screen's fonts are served by the PC instead of Google Fonts: the screen loads nothing from the internet.

### Fixed
- The screen's day, and its count of focus sessions, now change at local midnight; the day used to change at 1 or 2 a.m. in Paris (UTC date).
