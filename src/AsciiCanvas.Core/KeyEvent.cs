namespace AsciiCanvas;

/// <summary>A physical key press (including auto-repeat). Printable characters additionally arrive as <see cref="TextEvent"/>.</summary>
/// <param name="Key">The key that was pressed.</param>
/// <param name="Modifiers">The modifier keys held.</param>
public sealed record KeyEvent(Key Key, KeyModifiers Modifiers = KeyModifiers.None) : InputEvent;
