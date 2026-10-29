using Avalonia.Input;
using AwesomeAssertions;
using PayBeat.App.Platform;

namespace PayBeat.App.Tests.Platform;

/// <summary>
/// Tests for <see cref="HotkeyKeyMap"/>.
/// </summary>
public class HotkeyKeyMapTests
{
    /// <summary>
    /// Verifies the default hotkey formats like the WPF build did.
    /// </summary>
    [Fact]
    public void Format_DefaultHotkey_ReturnsCtrlAltX()
    {
        var text = HotkeyKeyMap.Format(0x0003, 0x58);

        text.Should().Be("Ctrl+Alt+X");
    }

    /// <summary>
    /// Verifies all modifiers are listed in a stable order.
    /// </summary>
    [Fact]
    public void Format_AllModifiers_ListsInStableOrder()
    {
        var text = HotkeyKeyMap.Format(0x000F, 0x74);

        text.Should().Be("Ctrl+Alt+Shift+Win+F5");
    }

    /// <summary>
    /// Verifies an unmapped virtual key is shown as hexadecimal instead of throwing.
    /// </summary>
    [Fact]
    public void Format_UnknownVirtualKey_ReturnsHex()
    {
        var text = HotkeyKeyMap.Format(HotkeyKeyMap.ModControl, 0xFF);

        text.Should().Be("Ctrl+0xFF");
    }

    /// <summary>
    /// Verifies the modifier preview omits the key.
    /// </summary>
    [Fact]
    public void FormatModifiers_CtrlShift_ReturnsModifiersOnly()
    {
        var text = HotkeyKeyMap.FormatModifiers(HotkeyKeyMap.ModControl | HotkeyKeyMap.ModShift);

        text.Should().Be("Ctrl+Shift");
    }

    /// <summary>
    /// Verifies keys convert to the Win32 virtual-key codes persisted in settings.
    /// </summary>
    /// <param name="key">The Avalonia key.</param>
    /// <param name="expected">The expected virtual-key code.</param>
    [Theory]
    [InlineData(Key.X, 0x58)]
    [InlineData(Key.A, 0x41)]
    [InlineData(Key.D0, 0x30)]
    [InlineData(Key.NumPad5, 0x65)]
    [InlineData(Key.F1, 0x70)]
    [InlineData(Key.F12, 0x7B)]
    [InlineData(Key.Left, 0x25)]
    [InlineData(Key.Space, 0x20)]
    [InlineData(Key.OemTilde, 0xC0)]
    public void TryGetVirtualKey_SupportedKey_ReturnsWin32Code(Key key, int expected)
    {
        var supported = HotkeyKeyMap.TryGetVirtualKey(key, out var virtualKey);

        supported.Should().BeTrue();
        virtualKey.Should().Be(expected);
    }

    /// <summary>
    /// Verifies modifier keys cannot be used as the hotkey itself.
    /// </summary>
    [Fact]
    public void TryGetVirtualKey_ModifierKey_ReturnsFalse()
    {
        var supported = HotkeyKeyMap.TryGetVirtualKey(Key.LeftCtrl, out _);

        supported.Should().BeFalse();
    }

    /// <summary>
    /// Verifies every mapped virtual key survives a format round trip without falling back to hex.
    /// </summary>
    [Theory]
    [InlineData(0x41)]
    [InlineData(0x5A)]
    [InlineData(0x30)]
    [InlineData(0x39)]
    [InlineData(0x70)]
    [InlineData(0x87)]
    public void Format_MappedVirtualKey_DoesNotUseHexFallback(int virtualKey)
    {
        var text = HotkeyKeyMap.Format(HotkeyKeyMap.ModControl, virtualKey);

        text.Should().NotContain("0x");
    }

    /// <summary>
    /// Verifies Avalonia modifiers convert to Win32 flags and the Meta key is ignored.
    /// </summary>
    [Fact]
    public void ToWin32Modifiers_CtrlAltMeta_ReturnsCtrlAltOnly()
    {
        var flags = HotkeyKeyMap.ToWin32Modifiers(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta);

        flags.Should().Be(0x0003);
    }
}
