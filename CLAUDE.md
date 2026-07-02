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

# Publish
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release
```

## Architecture

WPF floating widget app (.NET 10, MVVM). Shows real-time earnings as a borderless, always-on-top window, plus a system tray icon for display-mode switching, Settings/About, and Exit.

**Data flow:**
`DispatcherTimer` (configurable interval) → `MainViewModel.Refresh()` → `EarningsCalculator.Calculate()` → bound properties update the active view template.

**Display modes:** `DisplayMode` has `None`, `Normal`, `Compact`, `Mini`, and `Flex`, swapped inside a single `MainWindow` via `DataTemplate` + `DataTrigger` (`None` shows no window; only the tray icon remains). Double-clicking the widget opens the settings window. Each mode saves its last position independently per screen (`NormalPosition`, `CompactPosition`, `MiniPosition`, `FlexPosition` in `SalarySettings`). `Flex` is a fullscreen "show-off" view (`FlexView`) with a huge earnings figure, full workday stats, and a decorative animated background/glow pulse driven by `ColorAnimation`/`DoubleAnimation` started in its code-behind.

**Key files:**
- `src/PayBeat.App/App.xaml.cs` — `OnStartup` creates `MainViewModel`, shows `MainWindow`, restores saved position per display mode, and registers the global hotkey. `OnExit` saves window position back to settings.
- `src/PayBeat.App/Views/MainWindow.xaml` — borderless `Window` (`WindowStyle="None"`, `AllowsTransparency`, `ShowInTaskbar="False"`); hosts a `ContentControl` that switches between `NormalView`, `CompactView`, `MiniView`, and `FlexView` templates based on `DisplayMode`.
- `src/PayBeat.App/ViewModels/MainViewModel.cs` — owns the timer and all earnings/display state; `ReloadSettings()` is called by `SettingsViewModel` after save; raises `HotkeySettingsChanged` event when hotkey config changes.
- `src/PayBeat.App/Services/EarningsCalculator.cs` — pure static calculations: `Calculate()`, `WorkdayProgress()`, `RatePerSecond()`.
- `src/PayBeat.App/Services/SettingsService.cs` — persists `SalarySettings` to `%APPDATA%\PayBeat\settings.json`; uses a custom `TimeOnlyConverter` for `HH:mm` JSON serialization.
- `src/PayBeat.App/Services/HotkeyService.cs` — registers a Win32 global hotkey via `RegisterHotKey`/`UnregisterHotKey`; supports `Suspend()`/`Resume()` to suppress firing while the settings window is open. Default: Ctrl+Alt+X.
- `src/PayBeat.App/Services/LocalizationService.cs` — swaps `Strings.en.xaml` / `Strings.zh-CN.xaml` into `MergedDictionaries` at startup; `"auto"` resolves from `CultureInfo.CurrentUICulture`.
- `src/PayBeat.App/Services/StartupService.cs` — reads/writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` to manage Windows startup registration.
- `src/PayBeat.App/Helpers/ScreenHelper.cs` — Win32 P/Invoke helpers for multi-monitor position restore (matches by device name, falls back to nearest monitor) and `ClampToWorkArea`/`ClampToCurrentScreen` on `Window`.
- `src/PayBeat.App/Services/TrayIconService.cs` — `NotifyIcon` + `ContextMenuStrip` (WinForms) with display-mode submenu, Settings/About/Exit; left-click invokes an `onActivate` callback to bring the widget to front.

**Other view models & windows:**
- `src/PayBeat.App/ViewModels/SettingsViewModel.cs` — validates and saves user preferences; calls `MainViewModel.ReloadSettings()` after save. `HotkeyService` is suspended while the settings window is open to prevent the hotkey from interfering with keyboard capture.
- `src/PayBeat.App/Views/SettingsWindow.xaml` / `AboutWindow.xaml` — secondary windows; both are draggable via `MouseLeftButtonDown → DragMove()` and styled consistently with the main widget.

**Helpers:**
- `src/PayBeat.App/ViewModels/ViewModelBase.cs` — `INotifyPropertyChanged` base with `SetField<T>`.
- `src/PayBeat.App/Helpers/RelayCommand.cs` — minimal `ICommand` with optional `canExecute` and `RaiseCanExecuteChanged()`.
- `src/PayBeat.App/Views/Controls/TimePickerControl.xaml` — custom `UserControl` with `SelectedTime` dependency property (`TimeOnly`); up/down buttons + text boxes for hour and minute.

**Models:**
- `SalarySettings` — immutable `record`; defaults: `DailySalary=500`, `WorkStart=09:00`, `WorkEnd=18:00`, `Currency="¥"`, `DisplayMode=Normal`, `AlwaysOnTop=true`, `Opacity=1.0`, `RefreshInterval=1`, `Language="auto"`, `HotkeyModifiers=0x0003` (Ctrl+Alt), `HotkeyVirtualKey=0x58` (X). `MaxDailySalary` caps input at 99,999,999. Stores per-mode `WindowPosition` (Left, Top, ScreenDeviceName).

**UI theme:** Catppuccin Mocha dark palette (background `#1E1E2E`, surface `#313244`, text `#CDD6F4`, green accent `#A6E3A1`, blue accent `#89B4FA`). Styles live in `src/PayBeat.App/Resources/Styles.xaml`. UI strings live in `Strings.en.xaml` / `Strings.zh-CN.xaml` and are accessed via `{DynamicResource}`.

## Solution Configuration

| File | Purpose |
|------|---------|
| `Directory.Build.props` | Shared build properties: `TargetFramework`, `Nullable`, `LangVersion`, `UseArtifactsOutput` |
| `Directory.Packages.props` | Central package versions (`ManagePackageVersionsCentrally=true`) |
| `nuget.config` | Restricts package sources to nuget.org only (`<clear/>` overrides global config) |

Artifacts output to `artifacts/bin/<ProjectName>/<config>/` (SDK artifacts layout via `UseArtifactsOutput=true`).

## CI / Release

`.github/workflows/ci.yml` builds on every push (Windows runner, .NET 10). On a `v*` tag push, it additionally publishes a self-contained-false `win-x64` build, zips it, and creates a GitHub Release via `softprops/action-gh-release`. Versioning is derived from git tags via MinVer (e.g. `v1.2.0`); locally, ensure the tag is reachable from HEAD for a meaningful version.

User settings are persisted to `%APPDATA%\PayBeat\settings.json`.

There is no test project in this repository yet.
