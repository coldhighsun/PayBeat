# PayBeat

[![CI / Release](https://github.com/coldhighsun/PayBeat/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/PayBeat/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/coldhighsun/PayBeat)](https://github.com/coldhighsun/PayBeat/releases/latest)
[![GitHub All Releases](https://img.shields.io/github/downloads/coldhighsun/PayBeat/total)](https://github.com/coldhighsun/PayBeat/releases)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/coldhighsun/PayBeat)](https://github.com/coldhighsun/PayBeat/issues)
[![GitHub pull requests](https://img.shields.io/github/issues-pr/coldhighsun/PayBeat)](https://github.com/coldhighsun/PayBeat/pulls)
[![GitHub last commit](https://img.shields.io/github/last-commit/coldhighsun/PayBeat)](https://github.com/coldhighsun/PayBeat/commits/main)
[![GitHub Repo stars](https://img.shields.io/github/stars/coldhighsun/PayBeat)](https://github.com/coldhighsun/PayBeat/stargazers)

**English** | [简体中文](README.zh-CN.md)

Real-time salary progress widget for Windows

---

## Introduction

PayBeat is a borderless, always-on-top Windows widget that shows your real-time daily earnings down to the second. It lives in a corner of your screen and stays out of your way.

## Features

- Linearly calculates earnings per second from your daily salary and work hours, including overnight shifts that cross midnight (e.g. 22:00–06:00)
- Four display modes (None / Normal / Mini / Flex) — double-click to open Settings
- Flex mode is a fullscreen "show-off" view with a huge earnings figure, full workday stats, and a decorative animated background
- Each mode remembers its last position independently, with multi-monitor support
- System tray icon with a context menu (display mode, Settings, About, Exit); left-click brings the widget to the front with a brief scale-up flash
- Global hotkey to show / hide all windows (default `Ctrl+Alt+X`)
- Optional lunch break deduction and weekend earnings
- Tray balloon notifications for an end-of-day reminder, earnings milestones, and available updates — clicking an update notification opens its release page
- Checks GitHub Releases for updates on startup (throttled to once per day)
- English and Simplified Chinese UI, auto-detected from the OS locale
- Light and dark themes, auto-detected from the Windows appearance setting
- Configurable opacity, refresh interval, and currency symbol; numbers follow your Windows region format (decimal/thousands separators)
- Optional Windows startup registration

## Screenshots

| Flex | Normal | Mini |
|------|--------|------|
| ![Flex](src/PayBeat.App/Resources/Screenshots/flex.png) | ![Normal](src/PayBeat.App/Resources/Screenshots/normal.png) | ![Mini](src/PayBeat.App/Resources/Screenshots/mini.png) |

## Requirements

- Windows 10 / 11 (x64)
- Portable download: [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) must be installed
- Self-contained download: no runtime installation needed, everything is bundled

PayBeat is built on [Avalonia](https://avaloniaui.net/), so the code base is cross-platform. Official release packages are Windows-only for now; macOS and Linux builds are untested; the platform-specific features (global hotkey, "run at startup", z-order enforcement) fall back to no-ops there.

## Download

Grab the latest release from the [Releases page](https://github.com/coldhighsun/PayBeat/releases/latest). Three `win-x64` packages are published per version:

- `PayBeat-<version>-portable-runtime-required-win-x64.zip` — smaller, requires the .NET 10 Runtime
- `PayBeat-<version>-portable-standalone-win-x64.zip` — larger, runs standalone with no prerequisites
- `PayBeat-<version>-setup-win-x64.exe` — self-contained installer (no runtime prerequisite); installs per-user, adds a Start Menu shortcut, and registers for uninstall

## Build

```bash
# Build
dotnet build

# Run
dotnet run --project src/PayBeat.App/PayBeat.App.csproj

# Publish (portable)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release

# Publish (self-contained)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained

# Build installer (requires Inno Setup 7 and a self-contained publish at publish-selfcontained/)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained -o publish-selfcontained/
ISCC.exe installer\PayBeat.iss /DAppVersion=1.2.3
```

Output goes to `artifacts/bin/PayBeat.App/release/`. CI tags produce a `PayBeat-<version>-portable-runtime-required-win-x64.zip`, a `PayBeat-<version>-portable-standalone-win-x64.zip`, and a `PayBeat-<version>-setup-win-x64.exe` release artifact.

## Usage

1. On launch, the widget appears in the bottom-right corner
2. **Double-click** the widget to open Settings
3. **Drag** the widget anywhere; position is saved on exit
4. **Right-click** for a context menu with Settings and About
5. Use the **tray icon** for the same menu, plus display mode switching and Exit — left-click it to bring the widget to the front with a brief scale-up flash
6. Enter your daily salary, work hours, and currency symbol in Settings — changes apply immediately

## Settings Reference

| Option | Default | Description |
|--------|---------|-------------|
| Daily salary | 500 | Pre-tax gross salary per workday (decimals supported, max 99,999,999) |
| Work start | 09:00 | Time when earnings begin accruing |
| Work end | 18:00 | Time when earnings are capped at the daily total; set earlier than Work start for an overnight shift |
| Currency symbol | ¥ | Prefix shown before the amount |
| Display mode | Normal | Initial widget size (None / Normal / Mini / Flex) |
| Refresh interval | 1 s | UI update frequency (1–60 seconds) |
| Opacity | 0.8 | Window opacity when the mouse is not hovering (0.1–1.0) |
| Theme | Auto | UI color theme (Auto / Light / Dark); Auto follows the Windows appearance setting |
| Global hotkey | Ctrl+Alt+X | Toggle visibility of all windows |
| Run at startup | Off | Launch automatically when Windows starts |
| Deduct lunch break | Off | Excludes a daily break window from earnings (default 12:00–13:00) |
| Earn on weekends | Off | When off, Saturdays and Sundays earn nothing |
| End-of-day reminder | Off | Tray balloon a configurable number of minutes before work end (default 5) |
| Milestone notifications | Off | Tray balloon each time earnings cross an amount increment (default ¥100) |

## License

MIT
