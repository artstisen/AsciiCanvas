namespace AsciiCanvas;

/// <summary>A rectangle in cell coordinates.</summary>
/// <param name="X">The column of the left edge.</param>
/// <param name="Y">The row of the top edge.</param>
/// <param name="Width">The number of columns.</param>
/// <param name="Height">The number of rows.</param>
public readonly record struct CellRect(int X, int Y, int Width, int Height)
{
    /// <summary>The column just right of the rectangle.</summary>
    public int Right => X + Width;

    /// <summary>The row just below the rectangle.</summary>
    public int Bottom => Y + Height;

    /// <summary>True if the rectangle covers no cells.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>True if the cell at (<paramref name="x"/>, <paramref name="y"/>) is inside the rectangle.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>The overlap of this rectangle and <paramref name="other"/>, or an empty rectangle if they do not overlap.</summary>
    /// <param name="other">The rectangle to intersect with.</param>
    public CellRect Intersect(CellRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= x || bottom <= y ? default : new CellRect(x, y, right - x, bottom - y);
    }

    /// <summary>Moves the rectangle.</summary>
    /// <param name="dx">Columns to move right.</param>
    /// <param name="dy">Rows to move down.</param>
    public CellRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>Grows the rectangle on every side; negative values shrink it.</summary>
    /// <param name="dx">Columns added to the left and right.</param>
    /// <param name="dy">Rows added to the top and bottom.</param>
    public CellRect Inflate(int dx, int dy) => new(X - dx, Y - dy, Width + (2 * dx), Height + (2 * dy));
}
