namespace AsciiCanvas;

/// <summary>The canvas grid changed size.</summary>
/// <param name="Columns">The new number of columns.</param>
/// <param name="Rows">The new number of rows.</param>
public sealed record ResizeEvent(int Columns, int Rows) : InputEvent;
