namespace AsciiCanvas;

/// <summary>Sent when the state of the window changes, for example when the user maximizes it or restores it from the taskbar.</summary>
/// <param name="State">The new state.</param>
public sealed record WindowStateEvent(CanvasWindowState State) : InputEvent;
