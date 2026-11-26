namespace AsciiCanvas;

/// <summary>Text typed by the user, already translated for keyboard layout, dead keys and IME.</summary>
/// <param name="Text">The typed text.</param>
public sealed record TextEvent(string Text) : InputEvent;
