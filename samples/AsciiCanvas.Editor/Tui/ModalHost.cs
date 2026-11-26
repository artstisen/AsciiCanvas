namespace AsciiCanvas.Editor.Tui;

/// <summary>What a dialog needs from the editor: the canvas, and a way to draw the editor behind the dialog.</summary>
/// <param name="Canvas">The canvas.</param>
/// <param name="DrawBackground">Draws the editor without presenting, so the dialog can be drawn on top.</param>
internal sealed record ModalHost(CellCanvas Canvas, Action DrawBackground);
