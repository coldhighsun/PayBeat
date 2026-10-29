# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Prerequisites

- .NET 10 SDK
- Windows 10/11 x64 is the supported and tested platform. The UI is Avalonia, so macOS/Linux should build, but they are untested and the platform-specific features fall back to no-ops there.

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project src/PayBeat.App/PayBeat.App.csproj

# Test
dotnet test tests/PayBeat.App.Tests

# Publish (portable, requires the .NET 10 Runtime on target machine)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release

# Publish (self-contained, no runtime prerequisite)
dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained
```

Output goes to `artifacts/bin/PayBeat.App/release/`.

## Architecture

Avalonia floating widget app (.NET 10, MVVM). Shows real-time earnings as a borderless, always-on-top window, plus a system tray icon for display-mode switching, Settings/About, and Exit. Toast notifications are drawn by the app itself.

**Data flow:**
`DispatcherTimer` (configurable interval) → `MainViewModel.Refresh()` → `EarningsCalculator.Calculate()` → bound properties update the active view.

**Display modes:** `DisplayMode` has `None`, `Normal`, `Mini`, and `Flex`. `MainWindow` stacks `NormalView`, `MiniView` and `FlexView` in one `Panel` and toggles `IsVisible` from `MainViewModel.IsNormalMode/IsMiniMode/IsFlexMode` (`None` hides the window; only the tray icon remains). Double-clicking the widget (detected via the `PointerPressed` click count) opens the settings window. Windows are dragged by `Helpers/WindowDrag` (used by the widget and the Settings/About/Message windows): it captures the pointer and moves the window by hand, keeping the grab point as a *fraction* of the window size, instead of calling `BeginMoveDrag` — the native move loop keeps a fixed pixel offset, so the pointer drifts on the window when it is resized by a scaling change while crossing monitors of different DPI. `MainWindow` uses the drag-ended callback to re-fill the monitor in Flex mode (only if the window actually moved). The mini view's progress bar is a render-transform-scaled `Border` (`MiniView.axaml.cs`), not a `ProgressBar`: the stock indicator's width counts towards the desired width and makes an auto-sized window shrink in steps from its initial size. Each mode saves its last position independently per screen (`NormalPosition`, `MiniPosition`, `FlexPosition` in `SalarySettings`). `Flex` is a fullscreen "show-off" view (`FlexView`) with a huge earnings figure, full workday stats, and a decorative animated background/glow pulse; the looping animations are declared as `Style.Animations` in `FlexView.axaml` (Avalonia pauses them while the view is not effectively visible) and the code-behind only applies the theme colors. Manually started `Animation.RunAsync` loops did not tick in this setup, so avoid that approach here.

**Key entry points:**
- `Program.cs` — Avalonia `AppBuilder` setup.
- `App.axaml.cs` — owns the object graph (`SettingsService`, `AppWindowService`, `MainViewModel`, `MainWindow`, `IHotkeyService`, `TrayIconService`, `ToastService`); enforces single-instance via a named `Mutex`; saves window position on shutdown.
- `MainViewModel.cs` — owns the `DispatcherTimer` and all earnings/display state; `ReloadSettings()` (via `ISettingsApplier`) is called by `SettingsViewModel` after save.
- `MainWindow.axaml` — borderless transparent `Window` hosting the three views.
- `Services/AppWindowService.cs` — `IWindowService` implementation; the only place that creates the Settings/About windows, so view models never reference views.

**Important patterns:**
- **Platform seam (`Platform/`)**: everything OS-specific sits behind an interface created by `PlatformServices` — `IHotkeyService` (Win32 `RegisterHotKey` via `Win32Properties.AddWndProcHookCallback`; `NullHotkeyService` elsewhere, which also hides the hotkey section of the settings), `IStartupService` (HKCU `Run` registry entry on Windows), `ITopmostEnforcer` (Win32 `SetWindowPos` + foreground event hook that re-asserts the topmost z-order on Windows). Windows classes carry `[SupportedOSPlatform("windows")]` and are only constructed behind `OperatingSystem.IsWindows()`.
- `HotkeyKeyMap` converts between Avalonia `Key` and the Win32 virtual-key codes that `settings.json` stores (kept for compatibility with files written by the old WPF build) and formats hotkeys for display.
- `ScreenHelper` + `WindowPlacement` use Avalonia `Screens` in **device pixels**; `WindowPlacement` is pure geometry (clamp, center, dock, nearest screen) and unit tested. Saved `WindowPosition` values are device pixels; values written by the WPF build were logical units and are simply clamped to the screen.
- `TrayIconService` uses Avalonia `TrayIcon` + `NativeMenu` (no balloon tips). `ToastService` + `ToastWindow` show notifications as small non-activating topmost windows stacked bottom-right (`ToastLayout` is the pure stacking math).
- Dialogs use `MessageWindow` (a themed replacement for `MessageBox`), shown non-modally.
- `LocalizationService` copies the keys of `Strings.en.axaml` / `Strings.zh-CN.axaml` into `Application.Resources` (overwriting, so `DynamicResource` refreshes); UI strings use `{DynamicResource}`. `"auto"` resolves from `CultureInfo.CurrentUICulture`. `Get(key)` returns the key when no application is running (unit tests).
- `ThemeService` sets `Application.RequestedThemeVariant`; the palette lives in `Resources/Theme.axaml` as `ThemeDictionaries` (`Dark`/`Light`) which also override the Fluent control resources (TextBox/ComboBox/Slider/accent) so stock controls follow the palette. `"auto"` maps to `ThemeVariant.Default` (follows the OS).
- `SettingsViewModel` implements `INotifyDataErrorInfo` (per-field validation shown by Avalonia's `DataValidationErrors`); its collaborators (`ISettingsApplier`, `IStartupService`, `IWindowService`, `IAppearanceService`) are injected so it is unit testable.
- Number/percent text is formatted in view models (`EarnedFormatted`, `DailySalaryFormatted`, `ProgressFormatted`, `OpacityFormatted`) rather than via binding `StringFormat`, so it always follows the current culture.
- Bindings are reflection-based (`AvaloniaUseCompiledBindingsByDefault=false`), so XAML does not need `x:DataType`.

**Models:**
- `SalarySettings` — immutable `record`; defaults: `DailySalary=500`, `WorkStart=09:00`, `WorkEnd=18:00`, `Currency="¥"`, `DisplayMode=Normal`, `AlwaysOnTop=true`, `Opacity=0.8`, `RefreshInterval=1`, `Language="auto"`, `HotkeyModifiers=0x0003` (Ctrl+Alt), `HotkeyVirtualKey=0x58` (X). `MaxDailySalary` caps input at 99,999,999. Stores per-mode `WindowPosition` (Left, Top, ScreenDeviceName). Also carries `LunchBreakEnabled`/`LunchBreakStart`/`LunchBreakEnd`, `WorkOnWeekends`, and notification toggles (`EnableEndOfDayReminder`/`EndOfDayReminderMinutes`, `EnableMilestoneNotifications`/`MilestoneAmount`).
- `EarningsCalculator` — all earnings math is a pure function of `SalarySettings` + `DateTime`; `IsWorkday()` gates weekends, and `EffectiveWorkSeconds()`/`EffectiveElapsedSeconds()` subtract the lunch break window (elapsed time holds steady during the break) before `Calculate()`/`RatePerSecond()`/`WorkdayProgress()` divide by it.

**UI theme:** Catppuccin-derived palette (dark: background `#1E1E2E`, surface `#313244`, text `#CDD6F4`, green accent `#A6E3A1`, blue accent `#89B4FA`) defined per theme variant in `Resources/Theme.axaml`; structural styles (flat buttons, text boxes, tabs, section headers) live in `Resources/Styles.axaml` and reference the palette via `{DynamicResource}`. Controls use Fluent as the base and only override what differs; the flat `Button` replaces its template so Fluent's hover colors do not override local backgrounds.

## Solution Configuration

`PayBeat.slnx` (new XML-based solution format) references the app project and the test project.

| File | Purpose |
|------|---------|
| `global.json` | Pins SDK to `10.0.100` with `rollForward: latestMinor` |
| `Directory.Build.props` | Shared build properties: `Nullable`, `ImplicitUsings`, `LangVersion`, `UseArtifactsOutput`, `TreatWarningsAsErrors`, `SatelliteResourceLanguages` (en;zh-CN), `DebugType=embedded`, `MinVerTagPrefix=v`. Imports optional `Directory.Build.user` for local, gitignored overrides (e.g. a custom `ArtifactsPath`). |
| `Directory.Packages.props` | Central package versions (`ManagePackageVersionsCentrally=true`) |
| `nuget.config` | Restricts package sources to nuget.org only (`<clear/>` overrides global config) |

`PayBeat.App.csproj` targets plain `net10.0` (`OutputType=WinExe`), references `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, uses `AvaloniaResource` items for the icon/screenshots, sets `PublishSingleFile=true`, a custom `AssemblyName=PayBeat` (differs from the project name), and strips the ~100 MB of native `.pdb` files from publish output.

Artifacts output to `artifacts/bin/<ProjectName>/<config>/` (SDK artifacts layout via `UseArtifactsOutput=true`).

`TreatWarningsAsErrors` is enabled globally — all warnings are build errors.

## Tests

`tests/PayBeat.App.Tests` uses xUnit v3 (required by `Avalonia.Headless.XUnit`) + AwesomeAssertions. Pure logic (earnings, settings persistence, placement/toast math, hotkey key map, view models) uses plain `[Fact]`; UI code uses `[AvaloniaFact]`/`[AvaloniaTheory]` with a headless Skia platform (`TestAppBuilder` boots the real `App`). `ViewSmokeTests` renders every window; set the `PAYBEAT_TEST_SHOTS` environment variable to a directory to also save the rendered frames as PNGs for visual inspection. Tests never touch the real settings file: `SettingsService` takes an optional file path.

## CI / Release

`.github/workflows/ci.yml` has two jobs (Windows runner, .NET 10). `build` runs on every push and just compiles. `release` runs `needs: build` with a job-level `if: startsWith(github.ref, 'refs/tags/v')` — only on a `v*` tag push does it publish both a portable (`--no-self-contained`) and a self-contained `win-x64` build, zip each (`PayBeat-<version>-portable-runtime-required-win-x64.zip`, `PayBeat-<version>-portable-standalone-win-x64.zip`), compile `installer/PayBeat.iss` with Inno Setup (installed via winget) into `PayBeat-<version>-setup-win-x64.exe`, and create a GitHub Release via `softprops/action-gh-release` with all three files. Versioning is derived from git tags via MinVer (e.g. `v1.2.0`); locally, ensure the tag is reachable from HEAD for a meaningful version. The installer script itself takes its version from `/DAppVersion=<version>` (tag name with the `v` prefix stripped), not MinVer. Publish output contains `PayBeat.exe` plus the native SkiaSharp/HarfBuzz/ANGLE DLLs, so zips and the installer package the whole publish folder.

`installer/PayBeat.iss` packages the self-contained publish output into a per-user installer (`PrivilegesRequired=lowest`, installs under `{localappdata}\Programs\PayBeat`, no UAC prompt). It refuses to install/uninstall while the app is running (checks the `PayBeat_SingleInstance` mutex from `App.axaml.cs`) and cleans up `%APPDATA%\PayBeat` and the HKCU `Run` startup entry on uninstall. Build the publish output first (`dotnet publish ... --self-contained -o publish-selfcontained/`), then compile with `ISCC.exe installer\PayBeat.iss /DAppVersion=<version>`.

User settings are persisted to `%APPDATA%\PayBeat\settings.json` (`~/.config/PayBeat` / `~/Library/Application Support/PayBeat` on other platforms).
