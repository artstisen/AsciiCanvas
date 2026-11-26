namespace AsciiCanvas;

#pragma warning disable CS1591 // The member names are the key names.

/// <summary>A keyboard key, independent of keyboard layout and UI framework.</summary>
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
