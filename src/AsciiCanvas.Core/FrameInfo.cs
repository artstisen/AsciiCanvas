namespace AsciiCanvas;

/// <summary>Metadata for a presented frame.</summary>
/// <param name="Version">Increases with every <see cref="CellCanvas.Present"/> that published a change.</param>
/// <param name="CursorColumn">The column of the cursor.</param>
/// <param name="CursorRow">The row of the cursor.</param>
/// <param name="CursorVisible">True if the cursor should be shown.</param>
/// <param name="CursorShape">The shape of the cursor.</param>
public readonly record struct FrameInfo(
    long Version,
    int CursorColumn,
    int CursorRow,
    bool CursorVisible,
    CursorShape CursorShape);
