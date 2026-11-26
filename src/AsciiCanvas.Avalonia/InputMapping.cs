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

        return result;
    }

    /// <summary>Keys that must not also trigger Avalonia's own handling (focus movement and the like).</summary>
    public static bool ShouldHandle(Key key, KeyModifiers modifiers) =>
        key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Enter or Key.Escape
        || (modifiers & (KeyModifiers.Control | KeyModifiers.Alt)) != 0;

    public static string FilterText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text.All(c => c >= 0x20 && c != 0x7F) ? text : new string(text.Where(c => c >= 0x20 && c != 0x7F).ToArray());
    }

    public static MouseButton MapButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
        _ => MouseButton.None,
    };
}
