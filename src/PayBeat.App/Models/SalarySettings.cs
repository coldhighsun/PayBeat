namespace PayBeat.App.Models;

/// <summary>
/// Immutable user configuration record persisted to <c>%APPDATA%\PayBeat\settings.json</c>.
/// All properties have built-in defaults so a missing or corrupt file always yields a valid state.
/// </summary>
public record SalarySettings
{
    /// <summary>Daily gross salary used to compute the per-second earnings rate.</summary>
    public decimal DailySalary { get; init; } = 500m;

    /// <summary>Work day start time; earnings accrue from this moment.</summary>
    public TimeOnly WorkStart { get; init; } = new(9, 0);

    /// <summary>Work day end time; earnings are capped at <see cref="DailySalary"/> after this.</summary>
    public TimeOnly WorkEnd { get; init; } = new(18, 0);

    /// <summary>Currency symbol prepended to the formatted earnings string.</summary>
    public string Currency { get; init; } = "¥";

    /// <summary>Active widget display mode, determining which view template is shown.</summary>
    public DisplayMode DisplayMode { get; init; } = DisplayMode.Normal;

    /// <summary>Whether the widget window is pinned above all other windows.</summary>
    public bool AlwaysOnTop { get; init; } = true;

    /// <summary>Timer tick interval in seconds for refreshing the earnings display.</summary>
    public int RefreshInterval { get; init; } = 1;

    /// <summary>Window opacity when the mouse is not hovering (0.1–1.0).</summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// UI language code. <c>"auto"</c> resolves to the OS UI culture;
    /// supported explicit values are <c>"en"</c> and <c>"zh-CN"</c>.
    /// </summary>
    public string Language { get; init; } = "auto";

    /// <summary>
    /// Win32 modifier flags for the global show/hide hotkey (MOD_ALT=0x01, MOD_CONTROL=0x02,
    /// MOD_SHIFT=0x04, MOD_WIN=0x08). Default <c>0x0003</c> = Ctrl+Alt.
    /// </summary>
    public int HotkeyModifiers { get; init; } = 0x0003;

    /// <summary>Virtual-key code for the global show/hide hotkey. Default <c>0x58</c> = X.</summary>
    public int HotkeyVirtualKey { get; init; } = 0x58;

    /// <summary>Last saved position for <see cref="DisplayMode.Normal"/> mode.</summary>
    public WindowPosition? NormalPosition { get; init; }

    /// <summary>Last saved position for <see cref="DisplayMode.Compact"/> mode.</summary>
    public WindowPosition? CompactPosition { get; init; }

    /// <summary>Last saved position for <see cref="DisplayMode.Mini"/> mode.</summary>
    public WindowPosition? MiniPosition { get; init; }
}