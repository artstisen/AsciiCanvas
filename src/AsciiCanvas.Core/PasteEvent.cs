namespace AsciiCanvas;

/// <summary>
/// Text the user pasted, for example with Ctrl+V, Cmd+V or Shift+Insert. The host reads the clipboard and sends the text
/// as one event; the key press itself is not sent. Line breaks are normalized to <c>\n</c>.
/// </summary>
/// <param name="Text">The pasted text, which may span several lines.</param>
public sealed record PasteEvent(string Text) : InputEvent;
