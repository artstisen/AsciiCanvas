namespace AsciiCanvas;

/// <summary>A rectangle in cell coordinates.</summary>
public readonly record struct CellRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public CellRect Intersect(CellRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= x || bottom <= y ? default : new CellRect(x, y, right - x, bottom - y);
    }

    public CellRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public CellRect Inflate(int dx, int dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);
}
