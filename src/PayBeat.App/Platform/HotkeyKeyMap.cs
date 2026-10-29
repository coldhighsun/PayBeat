using Avalonia.Input;

namespace PayBeat.App.Platform;

/// <summary>
/// Translates between Avalonia <see cref="Key"/> values and the Win32 virtual-key codes persisted
/// in <c>settings.json</c>, and formats hotkeys for display. The settings schema keeps the Win32
/// values so files written by the WPF build keep working.
/// </summary>
public static class HotkeyKeyMap
{
    /// <summary>
    /// Win32 modifier flag for Alt.
    /// </summary>
    public const int ModAlt = 0x0001;

    /// <summary>
    /// Win32 modifier flag for Ctrl.
    /// </summary>
    public const int ModControl = 0x0002;

    /// <summary>
    /// Win32 modifier flag for Shift.
    /// </summary>
    public const int ModShift = 0x0004;

    /// <summary>
    /// Win32 modifier flag for the Windows / Meta key.
    /// </summary>
    public const int ModWin = 0x0008;

    /// <summary>
    /// Virtual-key code to Avalonia key lookup.
    /// </summary>
    private static readonly Dictionary<int, Key> VirtualKeyToKey = BuildTable();

    /// <summary>
    /// Avalonia key to virtual-key code lookup (inverse of <see cref="VirtualKeyToKey"/>).
    /// </summary>
    private static readonly Dictionary<Key, int> KeyToVirtualKey =
        VirtualKeyToKey.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// Returns a human-readable string for a modifier + virtual-key combination (e.g. <c>Ctrl+Alt+X</c>).
    /// </summary>
    /// <param name="modifiers">Win32 modifier flags.</param>
    /// <param name="virtualKey">Virtual-key code.</param>
    /// <returns>The display string.</returns>
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        AddModifierNames(modifiers, parts);
        parts.Add(VirtualKeyToKey.TryGetValue(virtualKey, out var key) ? key.ToString() : $"0x{virtualKey:X2}");
        return string.Join("+", parts);
    }

    /// <summary>
    /// Returns the modifier names only (e.g. <c>Ctrl+Alt</c>), used as a live preview while capturing.
    /// </summary>
    /// <param name="modifiers">Win32 modifier flags.</param>
    /// <returns>The modifier names joined with <c>+</c>, or an empty string.</returns>
    public static string FormatModifiers(int modifiers)
    {
        var parts = new List<string>();
        AddModifierNames(modifiers, parts);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Converts Avalonia key modifiers to Win32 modifier flags (Ctrl, Alt and Shift only; the
    /// Windows key is deliberately not capturable).
    /// </summary>
    /// <param name="modifiers">Avalonia key modifiers.</param>
    /// <returns>The Win32 modifier flags.</returns>
    public static int ToWin32Modifiers(KeyModifiers modifiers)
    {
        var result = 0;
        if ((modifiers & KeyModifiers.Control) != 0)
        {
            result |= ModControl;
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            result |= ModAlt;
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            result |= ModShift;
        }

        return result;
    }

    /// <summary>
    /// Attempts to convert an Avalonia key to a Win32 virtual-key code.
    /// </summary>
    /// <param name="key">The Avalonia key.</param>
    /// <param name="virtualKey">The virtual-key code when the key is supported.</param>
    /// <returns><see langword="true"/> when the key can be used as a hotkey.</returns>
    public static bool TryGetVirtualKey(Key key, out int virtualKey) =>
        KeyToVirtualKey.TryGetValue(key, out virtualKey);

    /// <summary>
    /// Appends the display names of the set modifier flags in a stable order.
    /// </summary>
    private static void AddModifierNames(int modifiers, List<string> parts)
    {
        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }
    }

    /// <summary>
    /// Builds the virtual-key lookup table for the keys that make sense as hotkeys.
    /// </summary>
    private static Dictionary<int, Key> BuildTable()
    {
        var table = new Dictionary<int, Key>
        {
            [0x08] = Key.Back,
            [0x0D] = Key.Enter,
            [0x1B] = Key.Escape,
            [0x20] = Key.Space,
            [0x21] = Key.PageUp,
            [0x22] = Key.PageDown,
            [0x23] = Key.End,
            [0x24] = Key.Home,
            [0x25] = Key.Left,
            [0x26] = Key.Up,
            [0x27] = Key.Right,
            [0x28] = Key.Down,
            [0x2D] = Key.Insert,
            [0x2E] = Key.Delete,
            [0x6A] = Key.Multiply,
            [0x6B] = Key.Add,
            [0x6D] = Key.Subtract,
            [0x6E] = Key.Decimal,
            [0x6F] = Key.Divide,
            [0xBA] = Key.OemSemicolon,
            [0xBB] = Key.OemPlus,
            [0xBC] = Key.OemComma,
            [0xBD] = Key.OemMinus,
            [0xBE] = Key.OemPeriod,
            [0xBF] = Key.OemQuestion,
            [0xC0] = Key.OemTilde,
            [0xDB] = Key.OemOpenBrackets,
            [0xDC] = Key.OemPipe,
            [0xDD] = Key.OemCloseBrackets,
            [0xDE] = Key.OemQuotes,
        };

        for (var i = 0; i < 10; i++)
        {
            table[0x30 + i] = Key.D0 + i;
            table[0x60 + i] = Key.NumPad0 + i;
        }

        for (var i = 0; i < 26; i++)
        {
            table[0x41 + i] = Key.A + i;
        }

        for (var i = 0; i < 24; i++)
        {
            table[0x70 + i] = Key.F1 + i;
        }

        return table;
    }
}
