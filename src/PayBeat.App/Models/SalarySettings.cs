namespace PayBeat.App.Models;

/// <summary>
/// Immutable user configuration record persisted to <c>%APPDATA%\PayBeat\settings.json</c>.
/// All properties have built-in defaults so a missing or corrupt file always yields a valid state.
/// </summary>
public record SalarySettings
{
    public const decimal MaxDailySalary = 99_999_999m;

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
    public WindowPosition? NormalPosition
    {
        get; init;
    }

    /// <summary>Last saved position for <see cref="DisplayMode.Mini"/> mode.</summary>
    public WindowPosition? MiniPosition
    {
        get; init;
    }

    /// <summary>Last saved monitor for <see cref="DisplayMode.Flex"/> mode. Only <see cref="WindowPosition.ScreenDeviceName"/> is used.</summary>
    public WindowPosition? FlexPosition
    {
        get; init;
    }

    /// <summary>Whether earnings should be deducted for a daily lunch break.</summary>
    public bool LunchBreakEnabled { get; init; } = false;

    /// <summary>Lunch break start time; only used when <see cref="LunchBreakEnabled"/> is true.</summary>
    public TimeOnly LunchBreakStart { get; init; } = new(12, 0);

    /// <summary>Lunch break end time; only used when <see cref="LunchBreakEnabled"/> is true.</summary>
    public TimeOnly LunchBreakEnd { get; init; } = new(13, 0);

    /// <summary>Whether earnings accrue on Saturdays and Sundays. When false, weekends earn nothing.</summary>
    public bool WorkOnWeekends { get; init; } = false;

    /// <summary>Whether to show a tray balloon notification shortly before work ends.</summary>
    public bool EnableEndOfDayReminder { get; init; } = false;

    /// <summary>How many minutes before <see cref="WorkEnd"/> the end-of-day reminder fires.</summary>
    public int EndOfDayReminderMinutes { get; init; } = 5;

    /// <summary>Whether to show a tray balloon notification each time earnings cross a <see cref="MilestoneAmount"/> increment.</summary>
    public bool EnableMilestoneNotifications { get; init; } = false;

    /// <summary>Earnings increment that triggers a milestone notification (e.g. every ¥100).</summary>
    public decimal MilestoneAmount { get; init; } = 100m;
}