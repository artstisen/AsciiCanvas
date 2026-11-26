namespace AsciiCanvas;

public enum Key
{
    Unknown = 0,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Left, Right, Up, Down,
    Home, End, PageUp, PageDown, Insert, Delete,
    Enter, Escape, Back, Tab, Space,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
}

public enum MouseButton
{
    None = 0,
    Left,
    Middle,
    Right,
}

public enum MouseAction
{
    Press,
    Release,
    Move,
    Wheel,
}

/// <summary>Base type of everything a program can receive from the canvas host.</summary>
public abstract record InputEvent;

/// <summary>A physical key press (including auto-repeat). Printable characters additionally arrive as <see cref="TextEvent"/>.</summary>
public sealed record KeyEvent(Key Key, KeyModifiers Modifiers = KeyModifiers.None) : InputEvent;

/// <summary>Text typed by the user, already translated for keyboard layout, dead keys and IME.</summary>
public sealed record TextEvent(string Text) : InputEvent;

/// <summary>Mouse input in cell coordinates. <paramref name="WheelDelta"/> is in notches (positive = up).</summary>
public sealed record MouseEvent(
    int Column,
    int Row,
    MouseButton Button,
    MouseAction Action,
    int WheelDelta = 0,
    KeyModifiers Modifiers = KeyModifiers.None) : InputEvent;

/// <summary>The canvas grid changed size.</summary>
public sealed record ResizeEvent(int Columns, int Rows) : InputEvent;
