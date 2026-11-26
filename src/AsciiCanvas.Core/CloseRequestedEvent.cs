namespace AsciiCanvas;

/// <summary>
/// The user asked to close the window (for example with its close button). Only sent when the program has set
/// <see cref="CellCanvas.InterceptClose"/>; the window then stays open until the program calls <see cref="CellCanvas.Exit"/>,
/// which lets it ask about unsaved changes first.
/// </summary>
public sealed record CloseRequestedEvent : InputEvent;
