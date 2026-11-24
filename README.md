# Edge Hub

A small ASP.NET Core service that runs on a Windows PC and turns a spare Android tablet into an always-on side screen: focus timer, today's agenda, meeting prep, Claude Code status, Spotify controls and light PC stats. The tablet only needs a browser.

> **Work in progress.** Only the console mode below works today. Features, screenshots and setup instructions will come with the first release.

## Build

Requirements: Windows 10 (build 19041) or later, [.NET 10 SDK](https://dotnet.microsoft.com/download) 10.0.402 or a later 10.0.4xx patch (see `global.json`).

```powershell
dotnet build
dotnet test
```

## Run the service

Give the service an access token once (32 characters or more), outside git:

```powershell
dotnet user-secrets set Security:Token "<a long random token>" --project src/EdgeHub
dotnet run --project src/EdgeHub
```

Open `http://localhost:5080/?k=<token>` once: the token moves into a cookie and the address is cleaned. The screen then gets the PC's stats and the current track every second from `/api/stream`. By default the service only answers on this PC; `src/EdgeHub/appsettings.example.json` explains the settings, including the one that opens it to the tablet.

## Console mode

Shows CPU, memory, network and disk usage and the current track, refreshed every second:

```powershell
dotnet run --project src/EdgeHub -- --console
```

Keys: `Space` play/pause, `N` next, `P` previous, `Q` or `Esc` quit. Keys can also be piped in as characters (`' '`, `n`, `p`, `q`); piped input does not end the console, so finish with `q`. The track comes from Windows' media controls, so Spotify's desktop app works without its Web API or a Premium account.

## License

[MIT](LICENSE)
