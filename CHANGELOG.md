# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Solution skeleton: ASP.NET Core service (`src/EdgeHub`) with a `/health` route, xUnit test project.
- Shared build settings: nullable reference types, .NET analyzers, warnings treated as errors.
- Continuous integration on Windows (restore, build, test) for every push and pull request.
- Optional Claude Code integration: `/rapport`, `/prep-reunions` and `/decisions` commands, example hooks configuration.
- Current single-file screen (`dashboard/index.html`), kept as-is as the starting point for `wwwroot`.
- Observed format of Claude Code session files, as groundwork for the session reader (`docs/formats/claude-code-sessions.md`).
