namespace AsciiCanvas.Editor.Tui;

/// <summary>Draws the frame of a dialog or menu: a filled box with a border, an optional title and a shadow.</summary>
internal static class DialogBox
{
    /// <summary>Centers a box of the given size on the canvas, shrinking it if the canvas is small.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="width">The wanted width.</param>
    /// <param name="height">The wanted height.</param>
    public static CellRect Center(CellCanvas canvas, int width, int height)
    {
        var w = Math.Min(width, Math.Max(10, canvas.Columns - 2));
        var h = Math.Min(height, Math.Max(5, canvas.Rows - 1));
        return new CellRect(Math.Max(0, (canvas.Columns - w) / 2), Math.Max(0, (canvas.Rows - h) / 2), w, h);
    }

    /// <summary>Draws the box with its shadow.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="rect">The outer area of the box.</param>
    /// <param name="title">The title in the top edge, or <see langword="null"/>.</param>
    public static void Draw(CellCanvas canvas, CellRect rect, string? title)
    {
        DrawShadow(canvas, rect);
        canvas.Box(rect, BoxStyle.DoubleLine, UiStyles.Panel, fill: true);
        if (!string.IsNullOrEmpty(title))
        {
            var label = " " + TextFit.Truncate(title, rect.Width - 6) + " ";
            canvas.Write(rect.X + ((rect.Width - CharWidth.Of(label)) / 2), rect.Y, label, UiStyles.PanelTitle);
        }
    }

    /// <summary>Darkens the cells just right of and below a box, so it seems to float above the text.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="rect">The area of the box.</param>
    public static void DrawShadow(CellCanvas canvas, CellRect rect)
    {
        for (var y = rect.Y + 1; y <= rect.Bottom; y++)
        {
            for (var x = rect.Right; x <= rect.Right + 1; x++)
            {
                Darken(canvas, x, y);
            }
        }

        for (var x = rect.X + 2; x < rect.Right; x++)
        {
            Darken(canvas, x, rect.Bottom);
        }
    }

    private static void Darken(CellCanvas canvas, int x, int y)
    {
        if (!canvas.Bounds.Contains(x, y))
        {
            return;
        }

        // A half-covered wide character would look broken, so those cells become blanks.
        var cell = canvas.GetCell(x, y);
        canvas.SetCell(x, y, cell.Width == CellWidth.Narrow ? cell.Rune : new System.Text.Rune(' '), UiStyles.Shadow);
    }
}
