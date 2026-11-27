namespace AsciiCanvas;

/// <summary>A key press as seen by <see cref="TextTerminal.ReadKeyAsync"/>.</summary>
/// <param name="Key">The key for action keys (arrows, Enter, F-keys, Ctrl+letter...); <see cref="AsciiCanvas.Key.Unknown"/> for typed text.</param>
/// <param name="Text">The typed text, or empty for action keys.</param>
/// <param name="Modifiers">The modifier keys held.</param>
/// <param name="Pasted">True for the text and the Enter presses of a paste (Ctrl+V ...), false for what was typed.</param>
public readonly record struct KeyPress(Key Key, string Text, KeyModifiers Modifiers, bool Pasted = false)
{
    /// <summary>True if the user typed text rather than pressing an action key.</summary>
    public bool IsText => Text.Length > 0;
}
