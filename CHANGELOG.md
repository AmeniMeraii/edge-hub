# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- The screen's day now changes at local midnight; it used to change at 1 or 2 a.m. in Paris (UTC date).

### Added
- Service: `/api/stream` sends the PC's stats and the current track every second as Server-Sent Events, to any number of screens.
- Access token for every `/api/*` route, given once in the address (`?k=`) and then kept in a cookie. Configuration checked at startup.
- The screen is served by the PC from `wwwroot`, with a web app manifest (full screen, landscape) and a « PC hors ligne » indicator.
- Console mode (`EdgeHub.exe --console`): CPU, memory, network and disk usage, current track from Windows' media controls, play/pause, next and previous from the keyboard.
- Solution skeleton: ASP.NET Core service (`src/EdgeHub`) with a `/health` route, xUnit test project.
- Shared build settings: nullable reference types, .NET analyzers, warnings treated as errors.
- Continuous integration on Windows (restore, build, test) for every pull request and every push to main.
- Optional Claude Code integration: `/rapport`, `/prep-reunions` and `/decisions` commands, example hooks configuration with its install notes.
- Current single-file screen (`dashboard/index.html`), kept as-is as the starting point for `wwwroot`.
- Observed format of Claude Code session files, as groundwork for the session reader (`docs/formats/claude-code-sessions.md`).
