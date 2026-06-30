# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

WPF floating widget app (.NET 10, MVVM). Shows real-time earnings as a borderless, always-on-top window. No system tray — the main window is the primary UI surface.

**Data flow:**
`DispatcherTimer` (configurable interval) → `MainViewModel.Refresh()` → `EarningsCalculator.Calculate()` → bound properties update the active view template.

**Display modes:** The app has three display modes (`Normal`, `Compact`, `Mini`) that swap view templates inside a single `MainWindow` via `DataTemplate` + `DataTrigger`. Double-clicking the widget opens the settings window. Each mode saves its last position independently per screen (`NormalPosition`, `CompactPosition`, `MiniPosition` in `SalarySettings`).

**Key files:**
- `src/PayBeat.App/App.xaml.cs` — `OnStartup` creates `MainViewModel`, shows `MainWindow`, restores saved position per display mode, and registers the global hotkey. `OnExit` saves window position back to settings.
- `src/PayBeat.App/Views/MainWindow.xaml` — borderless `Window` (`WindowStyle="None"`, `AllowsTransparency`, `ShowInTaskbar="False"`); hosts a `ContentControl` that switches between `NormalView`, `CompactView`, and `MiniView` templates based on `DisplayMode`.
- `src/PayBeat.App/ViewModels/MainViewModel.cs` — owns the timer and all earnings/display state; `ReloadSettings()` is called by `SettingsViewModel` after save; raises `HotkeySettingsChanged` event when hotkey config changes.
- `src/PayBeat.App/Services/EarningsCalculator.cs` — pure static calculations: `Calculate()`, `WorkdayProgress()`, `RatePerSecond()`.
- `src/PayBeat.App/Services/SettingsService.cs` — persists `SalarySettings` to `%APPDATA%\PayBeat\settings.json`; uses a custom `TimeOnlyConverter` for `HH:mm` JSON serialization.
- `src/PayBeat.App/Services/HotkeyService.cs` — registers a Win32 global hotkey via `RegisterHotKey`/`UnregisterHotKey`; supports `Suspend()`/`Resume()` to suppress firing while the settings window is open. Default: Ctrl+Alt+X.
- `src/PayBeat.App/Services/LocalizationService.cs` — swaps `Strings.en.xaml` / `Strings.zh-CN.xaml` into `MergedDictionaries` at startup; `"auto"` resolves from `CultureInfo.CurrentUICulture`.
- `src/PayBeat.App/Services/StartupService.cs` — reads/writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` to manage Windows startup registration.
- `src/PayBeat.App/Helpers/ScreenHelper.cs` — Win32 P/Invoke helpers for multi-monitor position restore (matches by device name, falls back to nearest monitor) and `ClampToWorkArea`/`ClampToCurrentScreen` on `Window`.

**Other view models & windows:**
- `src/PayBeat.App/ViewModels/SettingsViewModel.cs` — validates and saves user preferences; calls `MainViewModel.ReloadSettings()` after save. `HotkeyService` is suspended while the settings window is open to prevent the hotkey from interfering with keyboard capture.
- `src/PayBeat.App/Views/SettingsWindow.xaml` / `AboutWindow.xaml` — secondary windows; both are draggable via `MouseLeftButtonDown → DragMove()` and styled consistently with the main widget.

**Helpers:**
- `src/PayBeat.App/ViewModels/ViewModelBase.cs` — `INotifyPropertyChanged` base with `SetField<T>`.
- `src/PayBeat.App/Helpers/RelayCommand.cs` — minimal `ICommand` with optional `canExecute` and `RaiseCanExecuteChanged()`.
- `src/PayBeat.App/Views/Controls/TimePickerControl.xaml` — custom `UserControl` with `SelectedTime` dependency property (`TimeOnly`); up/down buttons + text boxes for hour and minute.

**Models:**
- `SalarySettings` — immutable `record`; defaults: `DailySalary=500`, `WorkStart=09:00`, `WorkEnd=18:00`, `Currency="¥"`, `DisplayMode=Normal`, `AlwaysOnTop=true`, `Opacity=1.0`, `RefreshInterval=1`, `Language="auto"`, `HotkeyModifiers=0x0003` (Ctrl+Alt), `HotkeyVirtualKey=0x58` (X). Stores per-mode `WindowPosition` (Left, Top, ScreenDeviceName).

**UI theme:** Catppuccin Mocha dark palette (background `#1E1E2E`, surface `#313244`, text `#CDD6F4`, green accent `#A6E3A1`, blue accent `#89B4FA`). Styles live in `src/PayBeat.App/Resources/Styles.xaml`. UI strings live in `Strings.en.xaml` / `Strings.zh-CN.xaml` and are accessed via `{DynamicResource}`.

## Solution Configuration

| File | Purpose |
|------|---------|
| `Directory.Build.props` | Shared build properties: `TargetFramework`, `Nullable`, `LangVersion`, `UseArtifactsOutput` |
| `Directory.Packages.props` | Central package versions (`ManagePackageVersionsCentrally=true`) |
| `nuget.config` | Restricts package sources to nuget.org only (`<clear/>` overrides global config) |

Artifacts output to `artifacts/bin/<ProjectName>/<config>/` (SDK artifacts layout via `UseArtifactsOutput=true`).
