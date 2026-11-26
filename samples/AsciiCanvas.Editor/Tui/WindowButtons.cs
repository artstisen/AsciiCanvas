namespace AsciiCanvas.Editor.Tui;

/// <summary>The three window buttons (minimize, maximize or restore, close) at the right end of the top row, for a window without a frame.</summary>
internal static class WindowButtons
{
    private const int ButtonWidth = 3;

    private static readonly CellStyle _hover = new(CellColor.BrightWhite, CellColor.Blue);
    private static readonly CellStyle _hoverClose = new(CellColor.BrightWhite, CellColor.Red);

    /// <summary>The number of columns the three buttons take.</summary>
    public const int Width = 3 * ButtonWidth;

    /// <summary>Finds the button under a cell.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="column">The grid column.</param>
    /// <param name="row">The grid row.</param>
    public static WindowButton At(CellCanvas canvas, int column, int row)
    {
        var index = (column - (canvas.Columns - Width)) / ButtonWidth;
        return row != 0 || column < canvas.Columns - Width || column >= canvas.Columns
            ? WindowButton.None
            : (WindowButton)(index + 1);
    }

    /// <summary>Draws the buttons over the right end of the top row.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="maximized">True when the window is maximized, so the middle button restores it.</param>
    /// <param name="hover">The button under the pointer, which is highlighted.</param>
    public static void Draw(CellCanvas canvas, bool maximized, WindowButton hover)
    {
        var x = canvas.Columns - Width;
        Draw(canvas, x, '─', hover == WindowButton.Minimize ? _hover : UiStyles.MenuBar);
        Draw(canvas, x + ButtonWidth, maximized ? '◫' : '□', hover == WindowButton.Maximize ? _hover : UiStyles.MenuBar);
        Draw(canvas, x + (2 * ButtonWidth), '×', hover == WindowButton.Close ? _hoverClose : UiStyles.MenuBar);
    }

    private static void Draw(CellCanvas canvas, int x, char glyph, CellStyle style)
    {
        canvas.Write(x, 0, $" {glyph} ", style);
    }
}
