# Edge Hub

A small ASP.NET Core service that runs on a Windows PC and turns a spare Android tablet into an always-on side screen: focus timer, today's agenda, meeting prep, Claude Code status, Spotify controls and light PC stats. The tablet only needs a browser.

> **Work in progress.** Nothing is usable yet. Features, screenshots and setup instructions will come with the first release.

## Build

Requirements: Windows 10 (build 19041) or later, [.NET 10 SDK](https://dotnet.microsoft.com/download) 10.0.402 or a later 10.0.4xx patch (see `global.json`).

```powershell
dotnet build
dotnet test
```

## License

[MIT](LICENSE)
