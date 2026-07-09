# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Prerequisites

- Windows 10/11 x64
- .NET 10 SDK

## Build & Run

```bash
# Build everything (App, Core, WinUI)
dotnet build

# Run the WPF build (mature, primary distribution today)
dotnet run --project src/PayBeat.App/PayBeat.App.csproj

# Run the WinUI 3 build (in-progress port, see below)
dotnet run --project src/PayBeat.WinUI/PayBeat.WinUI.csproj

# Publish (portable, matches CI)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained
dotnet publish src/PayBeat.WinUI/PayBeat.WinUI.csproj -c Release -r win-x64 --self-contained
```

There is no `.sln` file — use `PayBeat.slnx` (Visual Studio) or build/run via project paths directly.

## Architecture

The repo is mid-migration from a single WPF app to a shared-core + two-frontend layout:

- **`PayBeat.Core`** — UI-framework-agnostic models, services, and view models shared by both frontends: `SalarySettings`, `DisplayMode`, `WindowPosition`, `EarningsCalculator`, `SettingsService`, `StartupService`, `RelayCommand`, `ViewModelBase`. Pinned to `net10.0-windows10.0.19041.0` specifically (not unversioned `net10.0-windows`) so it never becomes the higher-versioned side of a project reference — both frontends' TFMs must resolve to a platform version >= Core's.
- **`PayBeat.App`** — the original WPF widget (floating window, tray icon, hotkey, borderless chrome). This is what ships in releases today and what most feature work still targets.
- **`PayBeat.WinUI`** — a WinUI 3 port of the same app, built independently against `PayBeat.Core`. It reimplements the WPF-side helpers/services in WinUI terms (`MainWindow` drives its own `AppWindow` chrome — manual drag-to-move, manual sizing, opacity fade, topmost re-assertion — instead of relying on WPF's `Window` defaults) and duplicates `MainViewModel`/`SettingsViewModel` under `PayBeat.WinUI.ViewModels` rather than sharing the App's. Views are switched by direct visibility toggling, not `DataTemplate`+`DataTrigger`. The tray icon still uses WinForms `NotifyIcon` (added via `FrameworkReference` to `Microsoft.WindowsDesktop.App.WindowsForms` — using the `UseWindowsForms` MSBuild property instead breaks WinUI's `.xaml` compilation, see comments in `PayBeat.WinUI.csproj`). Localization strings are plain `.json` `Content` files rather than `ResourceDictionary` XAML. **Not all features have parity yet** (e.g. tray balloon notifications are stubbed as a no-op — see the comment in `App.xaml.cs`); check before assuming WinUI behavior matches WPF.

When changing shared logic (earnings math, settings shape, startup registry handling), edit it once in `PayBeat.Core` — both frontends pick it up. When changing UI behavior, check whether the same change is needed in both `PayBeat.App` and `PayBeat.WinUI`; they are not kept in lockstep automatically.

### PayBeat.App (WPF) data flow

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
- `StartupService` (now in `PayBeat.Core`) manages the Windows startup registry entry (`HKCU\...\Run`) for the "Run at startup" setting.
- `LocalizationService` handles runtime language switching by swapping `ResourceDictionary` entries in `MergedDictionaries`.
- Localization: `Strings.en.xaml` / `Strings.zh-CN.xaml` are swapped into `MergedDictionaries` at startup; UI strings use `{DynamicResource}`. `"auto"` resolves from `CultureInfo.CurrentUICulture`.

**Models (`PayBeat.Core`):**
- `SalarySettings` — immutable `record`; defaults: `DailySalary=500`, `WorkStart=09:00`, `WorkEnd=18:00`, `Currency="¥"`, `DisplayMode=Normal`, `AlwaysOnTop=true`, `Opacity=1.0`, `RefreshInterval=1`, `Language="auto"`, `HotkeyModifiers=0x0003` (Ctrl+Alt), `HotkeyVirtualKey=0x58` (X). `MaxDailySalary` caps input at 99,999,999. Stores per-mode `WindowPosition` (Left, Top, ScreenDeviceName). Also carries `LunchBreakEnabled`/`LunchBreakStart`/`LunchBreakEnd`, `WorkOnWeekends`, and tray-balloon reminder toggles (`EnableEndOfDayReminder`/`EndOfDayReminderMinutes`, `EnableMilestoneNotifications`/`MilestoneAmount`).
- `EarningsCalculator` — all earnings math is a pure function of `SalarySettings` + `DateTime`; `IsWorkday()` gates weekends, and `EffectiveWorkSeconds()`/`EffectiveElapsedSeconds()` subtract the lunch break window (elapsed time holds steady during the break) before `Calculate()`/`RatePerSecond()`/`WorkdayProgress()` divide by it.

**UI theme:** Catppuccin Mocha dark palette (background `#1E1E2E`, surface `#313244`, text `#CDD6F4`, green accent `#A6E3A1`, blue accent `#89B4FA`). WPF styles live in `src/PayBeat.App/Resources/Styles.xaml`; WinUI styles live in `src/PayBeat.WinUI/Resources/Styles.xaml`. WPF UI strings live in `Strings.en.xaml` / `Strings.zh-CN.xaml` and are accessed via `{DynamicResource}`; WinUI UI strings live in `Strings.en.json` / `Strings.zh-CN.json`.

## Solution Configuration

| File | Purpose |
|------|---------|
| `Directory.Build.props` | Shared build properties: `Nullable`, `ImplicitUsings`, `LangVersion`, `UseArtifactsOutput`, `TreatWarningsAsErrors`, `SatelliteResourceLanguages` (en;zh-CN), `DebugType=embedded`, `MinVerTagPrefix=v`. Imports optional `Directory.Build.user` for local overrides. |
| `Directory.Packages.props` | Central package versions (`ManagePackageVersionsCentrally=true`) |
| `nuget.config` | Restricts package sources to nuget.org only (`<clear/>` overrides global config) |

Artifacts output to `artifacts/bin/<ProjectName>/<config>/` (SDK artifacts layout via `UseArtifactsOutput=true`).

`TreatWarningsAsErrors` is enabled globally — all warnings are build errors.

## CI / Release

`.github/workflows/ci.yml` builds on every push (Windows runner, .NET 10). On a `v*` tag push, it additionally publishes both frontends self-contained for `win-x64`, zips each separately (`PayBeat-<version>-wpf-portable-win-x64.zip`, `PayBeat-<version>-winui-portable-win-x64.zip`), and creates a GitHub Release via `softprops/action-gh-release`. The WinUI publish step prunes per-locale `.mui` resource folders from Windows App SDK's native components down to `en-us`/`zh-CN` (unaffected by `SatelliteResourceLanguages`, which only trims .NET managed satellite assemblies). Versioning is derived from git tags via MinVer (e.g. `v1.2.0`); locally, ensure the tag is reachable from HEAD for a meaningful version.

User settings are persisted to `%APPDATA%\PayBeat\settings.json` (shared by both frontends).

There is no test project in this repository yet.
