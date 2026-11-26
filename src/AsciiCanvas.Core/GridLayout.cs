namespace AsciiCanvas;

/// <summary>Where the grid sits inside a pixel area and how large its cells are. Pure math, shared by hosts.</summary>
/// <param name="CellWidth">The width of a cell in pixels.</param>
/// <param name="CellHeight">The height of a cell in pixels.</param>
/// <param name="OffsetX">The distance from the left of the area to the grid.</param>
/// <param name="OffsetY">The distance from the top of the area to the grid.</param>
public readonly record struct GridLayout(double CellWidth, double CellHeight, double OffsetX, double OffsetY)
{
    /// <summary>
    /// Scales a fixed <paramref name="columns"/> x <paramref name="rows"/> grid to fit the area.
    /// With <paramref name="preserveAspect"/> cells keep <paramref name="cellAspect"/> and the grid is centered;
    /// otherwise cells stretch to fill the area.
    /// </summary>
    /// <param name="width">The width of the area in pixels.</param>
    /// <param name="height">The height of the area in pixels.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="cellAspect">The width divided by the height of a cell.</param>
    /// <param name="preserveAspect">True to keep the cell proportions.</param>
    public static GridLayout Fit(double width, double height, int columns, int rows, double cellAspect, bool preserveAspect)
    {
        if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0)
        {
            return default;
        }

        if (!preserveAspect || cellAspect <= 0)
        {
            return new GridLayout(width / columns, height / rows, 0, 0);
        }

        var cellWidth = Math.Min(width / columns, height / rows * cellAspect);
        var cellHeight = cellWidth / cellAspect;
        return new GridLayout(
            cellWidth,
            cellHeight,
            (width - (cellWidth * columns)) / 2,
            (height - (cellHeight * rows)) / 2);
    }

    /// <summary>Counts how many whole cells of the given size fit in the area (at least 1 x 1).</summary>
    /// <param name="width">The width of the area in pixels.</param>
    /// <param name="height">The height of the area in pixels.</param>
    /// <param name="cellWidth">The width of a cell in pixels.</param>
    /// <param name="cellHeight">The height of a cell in pixels.</param>
    public static (int Columns, int Rows) CountFor(double width, double height, double cellWidth, double cellHeight)
    {
        if (cellWidth <= 0 || cellHeight <= 0)
        {
            return (1, 1);
        }

        return (Math.Max(1, (int)Math.Floor(width / cellWidth)), Math.Max(1, (int)Math.Floor(height / cellHeight)));
    }

    /// <summary>Converts a pixel position to a cell, clamped to the grid.</summary>
    /// <param name="x">The horizontal pixel position.</param>
    /// <param name="y">The vertical pixel position.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    public (int Column, int Row) CellAt(double x, double y, int columns, int rows)
    {
        if (CellWidth <= 0 || CellHeight <= 0)
        {
            return (0, 0);
        }

        var column = (int)Math.Floor((x - OffsetX) / CellWidth);
        var row = (int)Math.Floor((y - OffsetY) / CellHeight);
        return (Math.Clamp(column, 0, columns - 1), Math.Clamp(row, 0, rows - 1));
    }
}
