using System.Text;

namespace AsciiCanvas;

/// <summary>A plain, non-thread-safe 2D array of <see cref="Cell"/>s. Also usable as an off-screen sprite or snapshot.</summary>
public sealed class CellGrid
{
    private Cell[] _cells;

    /// <summary>Creates a grid filled with blank cells.</summary>
    /// <param name="columns">The number of columns (at least 1).</param>
    /// <param name="rows">The number of rows (at least 1).</param>
    /// <param name="style">The style of the blank cells.</param>
    public CellGrid(int columns, int rows, CellStyle style = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        Columns = columns;
        Rows = rows;
        _cells = new Cell[columns * rows];
        Array.Fill(_cells, Cell.Blank(style));
    }

    /// <summary>The number of columns.</summary>
    public int Columns { get; private set; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; private set; }

    /// <summary>The area of the whole grid.</summary>
    public CellRect Bounds => new(0, 0, Columns, Rows);

    /// <summary>Raw cell access; does not repair double-width pairs. Use <see cref="Put"/> for that.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the grid.</exception>
    public Cell this[int x, int y]
    {
        get
        {
            ThrowIfOutOfBounds(x, y);
            return _cells[(y * Columns) + x];
        }
        set
        {
            ThrowIfOutOfBounds(x, y);
            _cells[(y * Columns) + x] = value;
        }
    }

    /// <summary>True if the position is inside the grid.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public bool InBounds(int x, int y) => (uint)x < (uint)Columns && (uint)y < (uint)Rows;

    /// <summary>Gets the cells of one row.</summary>
    /// <param name="y">The row.</param>
    public ReadOnlySpan<Cell> Row(int y) => _cells.AsSpan(y * Columns, Columns);

    /// <summary>Gets all cells, row by row.</summary>
    public ReadOnlySpan<Cell> AsSpan() => _cells;

    /// <summary>Sets every cell to <paramref name="cell"/>.</summary>
    /// <param name="cell">The cell to copy everywhere.</param>
    public void Fill(Cell cell) => Array.Fill(_cells, cell);

    /// <summary>
    /// Places a rune at a position, keeping double-width characters consistent: any wide character partly
    /// overwritten is replaced by blanks.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="rune">The character to place.</param>
    /// <param name="style">The style of the cell.</param>
    /// <returns>The columns occupied (0 if the rune has no width or does not fit).</returns>
    public int Put(int x, int y, Rune rune, CellStyle style)
    {
        if (!InBounds(x, y))
        {
            return 0;
        }

        var width = CharWidth.Of(rune);
        switch (width)
        {
            case 1:
                BreakPairAt(x, y);
                _cells[(y * Columns) + x] = new Cell(rune, style);
                return 1;

            case 2 when x + 1 < Columns:
                BreakPairAt(x, y);
                BreakPairAt(x + 1, y);
                _cells[(y * Columns) + x] = new Cell(rune, style, CellWidth.WideLead);
                _cells[(y * Columns) + x + 1] = new Cell(new Rune(' '), style, CellWidth.WideTrail);
                return 2;

            default:
                return 0;
        }
    }

    /// <summary>Resizes the grid, keeping existing content anchored at the top-left.</summary>
    /// <param name="columns">The new number of columns (at least 1).</param>
    /// <param name="rows">The new number of rows (at least 1).</param>
    /// <param name="fillStyle">The style of the new cells.</param>
    public void Resize(int columns, int rows, CellStyle fillStyle = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        if (columns == Columns && rows == Rows)
        {
            return;
        }

        var next = new Cell[columns * rows];
        Array.Fill(next, Cell.Blank(fillStyle));
        var copyColumns = Math.Min(columns, Columns);
        var copyRows = Math.Min(rows, Rows);
        for (var y = 0; y < copyRows; y++)
        {
            Array.Copy(_cells, y * Columns, next, y * columns, copyColumns);
        }

        var shrankHorizontally = columns < Columns;
        _cells = next;
        Columns = columns;
        Rows = rows;

        // A wide character cut by the new right edge must not leave a dangling half.
        if (shrankHorizontally)
        {
            for (var y = 0; y < copyRows; y++)
            {
                ref var last = ref _cells[(y * Columns) + Columns - 1];
                if (last.Width == CellWidth.WideLead)
                {
                    last = Cell.Blank(last.Style);
                }
            }
        }
    }

    /// <summary>Makes this grid an exact copy of another, resizing if needed.</summary>
    /// <param name="source">The grid to copy.</param>
    public void CopyFrom(CellGrid source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Columns != Columns || source.Rows != Rows)
        {
            Columns = source.Columns;
            Rows = source.Rows;
            _cells = new Cell[source._cells.Length];
        }

        Array.Copy(source._cells, _cells, _cells.Length);
    }

    private void ThrowIfOutOfBounds(int x, int y)
    {
        if (!InBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(x < 0 || x >= Columns ? nameof(x) : nameof(y));
        }
    }

    private void BreakPairAt(int x, int y)
    {
        ref var cell = ref _cells[(y * Columns) + x];
        switch (cell.Width)
        {
            case CellWidth.WideTrail when x > 0:
                _cells[(y * Columns) + x - 1] = Cell.Blank(_cells[(y * Columns) + x - 1].Style);
                cell = Cell.Blank(cell.Style);
                break;
            case CellWidth.WideLead when x + 1 < Columns:
                _cells[(y * Columns) + x + 1] = Cell.Blank(_cells[(y * Columns) + x + 1].Style);
                cell = Cell.Blank(cell.Style);
                break;
        }
    }
}
