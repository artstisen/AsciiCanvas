namespace AsciiCanvas.Avalonia;

/// <summary>How the grid relates to the size of the control.</summary>
public enum CanvasSizing
{
    /// <summary>The grid keeps its column and row count; cells and font scale to fit the control.</summary>
    ScaleToFit,

    /// <summary>Cells keep the size given by <c>CanvasView.FontSize</c>; the canvas gains or loses columns and rows as the control is resized.</summary>
    FillWithCells,
}
