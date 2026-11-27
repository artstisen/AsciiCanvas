using Avalonia.Input;
using AvaloniaKey = Avalonia.Input.Key;
using AvaloniaKeyModifiers = Avalonia.Input.KeyModifiers;

namespace AsciiCanvas.Avalonia;

internal static class InputMapping
{
    public static Key MapKey(AvaloniaKey key) => key switch
    {
        AvaloniaKey.PageUp => Key.PageUp,
        AvaloniaKey.PageDown => Key.PageDown,
        AvaloniaKey.Back => Key.Back,
        AvaloniaKey.Return => Key.Enter,
        _ => Enum.TryParse<Key>(key.ToString(), out var mapped) && mapped != Key.Unknown ? mapped : Key.Unknown,
    };

    public static KeyModifiers MapModifiers(AvaloniaKeyModifiers modifiers)
    {
        var result = KeyModifiers.None;
        if ((modifiers & AvaloniaKeyModifiers.Shift) != 0)
        {
            result |= KeyModifiers.Shift;
        }

        if ((modifiers & AvaloniaKeyModifiers.Control) != 0)
        {
            result |= KeyModifiers.Control;
        }

        if ((modifiers & AvaloniaKeyModifiers.Alt) != 0)
        {
            result |= KeyModifiers.Alt;
        }

        if ((modifiers & AvaloniaKeyModifiers.Meta) != 0)
        {
            result |= KeyModifiers.Meta;
        }

        return result;
    }

    /// <summary>
    /// Keys that must not also trigger Avalonia's own handling (focus movement and the like). Ctrl+Alt together is left
    /// unhandled: it is how Windows reports AltGr, which types characters such as <c>$ @ { [ \</c> on many layouts, and a handled
    /// key press makes Avalonia drop the text that follows it.
    /// </summary>
    public static bool ShouldHandle(Key key, KeyModifiers modifiers) =>
        key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Enter or Key.Escape
        || (!IsAltGr(modifiers) && !IsOptionText(key, modifiers) &&
            (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0);

    // On macOS, Option (Alt) alone with a letter or digit types a character (@ $ [ { on many layouts): left unhandled, so
    // Avalonia gives the text
    private static bool IsOptionText(Key key, KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS() && (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == KeyModifiers.Alt &&
        key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9);

    private static bool IsAltGr(KeyModifiers modifiers) =>
        (modifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt);

    /// <summary>Ctrl+V and Cmd+V, plus the classic Shift+Insert.</summary>
    public static bool IsPasteShortcut(Key key, KeyModifiers modifiers) =>
        (key == Key.V && modifiers is KeyModifiers.Control or KeyModifiers.Meta)
        || (key == Key.Insert && modifiers == KeyModifiers.Shift);

    /// <summary>Removes control characters from typed text; they arrive as key events instead.</summary>
    public static string FilterText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text.All(IsPrintable) ? text : string.Concat(text.Where(IsPrintable));
    }

    public static MouseButton MapButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
        _ => MouseButton.None,
    };

    private static bool IsPrintable(char c) => c >= 0x20 && c != 0x7F;
}
