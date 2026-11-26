namespace AsciiCanvas.Editor.Tui;

/// <summary>A button in a dialog.</summary>
/// <param name="Label">The text on the button.</param>
/// <param name="Choice">What pressing it answers.</param>
/// <param name="Hotkey">A letter that presses it from the keyboard; also underlined in the label.</param>
internal sealed record DialogButton(string Label, DialogChoice Choice, char Hotkey);
