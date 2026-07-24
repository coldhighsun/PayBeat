# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Prerequisites

- Windows 10/11 x64
- .NET 10 SDK

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project src/PayBeat.App/PayBeat.App.csproj

# Publish (portable, requires .NET 10 Desktop Runtime on target machine)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release

# Publish (self-contained, no runtime prerequisite)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained
```

Output goes to `artifacts/bin/PayBeat.App/release/`.

## Architecture

WPF floating widget app (.NET 10, MVVM). Shows real-time earnings as a borderless, always-on-top window, plus a system tray icon for display-mode switching, Settings/About, and Exit.

**Data flow:**
`DispatcherTimer` (configurable interval) → `MainViewModel.Refresh()` → `EarningsCalculator.Calculate()` → bound properties update the active view template.

**Display modes:** `DisplayMode` has `None`, `Normal`, `Mini`, and `Flex`, swapped inside a single `MainWindow` via `DataTemplate` + `DataTrigger` (`None` shows no window; only the tray icon remains). Double-clicking the widget opens the settings window. Each mode saves its last position independently per screen (`NormalPosition`, `MiniPosition`, `FlexPosition` in `SalarySettings`). `Flex` is a fullscreen "show-off" view (`FlexView`) with a huge earnings figure, full workday stats, and a decorative animated background/glow pulse driven by `ColorAnimation`/`DoubleAnimation` started in its code-behind.

**Key entry points:**
- `App.xaml.cs` — owns the object graph (`SettingsService`, `MainViewModel`, `MainWindow`, `HotkeyService`, `TrayIconService`); enforces single-instance via a named `Mutex`; saves window position on exit.
- `MainViewModel.cs` — owns the `DispatcherTimer` and all earnings/display state; `ReloadSettings()` is called by `SettingsViewModel` after save.
- `MainWindow.xaml` — borderless `Window` with a `ContentControl` that switches view templates (`NormalView`, `MiniView`, `FlexView`) via `DataTrigger` on `DisplayMode`.

**Important patterns:**
- The tray icon (`TrayIconService`) uses **WinForms** `NotifyIcon` + `ContextMenuStrip` hosted inside the WPF app — any changes must account for the WinForms/WPF interop boundary.
- `HotkeyService` uses Win32 `RegisterHotKey`/`UnregisterHotKey` P/Invoke; it supports `Suspend()`/`Resume()` to avoid conflicts while the settings window captures key input.
- `ScreenHelper` uses Win32 P/Invoke for multi-monitor position restore (matches by device name, falls back to nearest monitor).
- `TopmostHelper` periodically re-asserts the window's topmost z-order via Win32 `SetWindowPos` P/Invoke, working around other apps that steal focus.
- `ForegroundWatcher` monitors the active foreground window via a Win32 event hook to trigger topmost re-assertion when another window takes focus.
- `StartupService` manages the Windows startup registry entry (`HKCU\...\Run`) for the "Run at startup" setting.
- `LocalizationService` handles runtime language switching by swapping `ResourceDictionary` entries in `MergedDictionaries`.
- Localization: `Strings.en.xaml` / `Strings.zh-CN.xaml` are swapped into `MergedDictionaries` at startup; UI strings use `{DynamicResource}`. `"auto"` resolves from `CultureInfo.CurrentUICulture`.

**Models:**
- `SalarySettings` — immutable `record`; defaults: `DailySalary=500`, `WorkStart=09:00`, `WorkEnd=18:00`, `Currency="¥"`, `DisplayMode=Normal`, `AlwaysOnTop=true`, `Opacity=1.0`, `RefreshInterval=1`, `Language="auto"`, `HotkeyModifiers=0x0003` (Ctrl+Alt), `HotkeyVirtualKey=0x58` (X). `MaxDailySalary` caps input at 99,999,999. Stores per-mode `WindowPosition` (Left, Top, ScreenDeviceName). Also carries `LunchBreakEnabled`/`LunchBreakStart`/`LunchBreakEnd`, `WorkOnWeekends`, and tray-balloon reminder toggles (`EnableEndOfDayReminder`/`EndOfDayReminderMinutes`, `EnableMilestoneNotifications`/`MilestoneAmount`).
- `EarningsCalculator` — all earnings math is a pure function of `SalarySettings` + `DateTime`; `IsWorkday()` gates weekends, and `EffectiveWorkSeconds()`/`EffectiveElapsedSeconds()` subtract the lunch break window (elapsed time holds steady during the break) before `Calculate()`/`RatePerSecond()`/`WorkdayProgress()` divide by it.

**UI theme:** Catppuccin Mocha dark palette (background `#1E1E2E`, surface `#313244`, text `#CDD6F4`, green accent `#A6E3A1`, blue accent `#89B4FA`). Styles live in `src/PayBeat.App/Resources/Styles.xaml`. UI strings live in `Strings.en.xaml` / `Strings.zh-CN.xaml` and are accessed via `{DynamicResource}`.

## Solution Configuration

`PayBeat.slnx` (new XML-based solution format) references the single app project — build and run commands typically target the project path directly rather than the solution.

| File | Purpose |
|------|---------|
| `global.json` | Pins SDK to `10.0.100` with `rollForward: latestMinor` |
| `Directory.Build.props` | Shared build properties: `Nullable`, `ImplicitUsings`, `LangVersion`, `UseArtifactsOutput`, `TreatWarningsAsErrors`, `SatelliteResourceLanguages` (en;zh-CN), `DebugType=embedded`, `MinVerTagPrefix=v`. Imports optional `Directory.Build.user` for local, gitignored overrides (e.g. a custom `ArtifactsPath`). |
| `Directory.Packages.props` | Central package versions (`ManagePackageVersionsCentrally=true`) |
| `nuget.config` | Restricts package sources to nuget.org only (`<clear/>` overrides global config) |

`PayBeat.App.csproj` targets `net10.0-windows`, sets `UseWPF=true` **and** `UseWindowsForms=true` (WinForms is pulled in for the tray icon's `NotifyIcon`/`ContextMenuStrip`), `PublishSingleFile=true`, and a custom `AssemblyName=PayBeat` (differs from the project name).

Artifacts output to `artifacts/bin/<ProjectName>/<config>/` (SDK artifacts layout via `UseArtifactsOutput=true`).

`TreatWarningsAsErrors` is enabled globally — all warnings are build errors.

## CI / Release

`.github/workflows/ci.yml` has two jobs (Windows runner, .NET 10). `build` runs on every push and just compiles. `release` runs `needs: build` with a job-level `if: startsWith(github.ref, 'refs/tags/v')` — only on a `v*` tag push does it publish both a portable (`--no-self-contained`) and a self-contained `win-x64` build, zip each (`PayBeat-<version>-portable-runtime-required-win-x64.zip`, `PayBeat-<version>-portable-standalone-win-x64.zip`), and create a GitHub Release via `softprops/action-gh-release`. Versioning is derived from git tags via MinVer (e.g. `v1.2.0`); locally, ensure the tag is reachable from HEAD for a meaningful version.

User settings are persisted to `%APPDATA%\PayBeat\settings.json`.

There is no test project in this repository yet.
