namespace AsciiCanvas;

/// <summary>Mouse input in cell coordinates.</summary>
/// <param name="Column">The column under the pointer.</param>
/// <param name="Row">The row under the pointer.</param>
/// <param name="Button">The button involved, or the button held during a move.</param>
/// <param name="Action">What happened.</param>
/// <param name="WheelDelta">Wheel movement in notches (positive = up); 0 unless <paramref name="Action"/> is <see cref="MouseAction.Wheel"/>.</param>
/// <param name="Modifiers">The modifier keys held.</param>
public sealed record MouseEvent(
    int Column,
    int Row,
    MouseButton Button,
    MouseAction Action,
    int WheelDelta = 0,
    KeyModifiers Modifiers = KeyModifiers.None) : InputEvent;
