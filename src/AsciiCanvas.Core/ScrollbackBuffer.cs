namespace AsciiCanvas;

/// <summary>The rows that scrolled off the top of a <see cref="CellCanvas"/>, oldest first, in a ring of at most <see cref="Limit"/> rows. Not thread-safe.</summary>
internal sealed class ScrollbackBuffer
{
    private Cell[][] _lines = [];
    private int _start;

    /// <summary>The number of rows kept.</summary>
    public int Count { get; private set; }

    /// <summary>The most rows kept; older rows are dropped. 0 keeps none.</summary>
    public int Limit => _lines.Length;

    /// <summary>A row by age: 0 is the oldest.</summary>
    public Cell[] this[int index] => _lines[(_start + index) % _lines.Length];

    /// <summary>Adds a row as the newest, dropping the oldest when full.</summary>
    /// <returns>The number of rows dropped (0 or 1).</returns>
    public int Add(Cell[] line)
    {
        if (_lines.Length == 0)
        {
            return 0;
        }

        if (Count < _lines.Length)
        {
            _lines[(_start + Count) % _lines.Length] = line;
            Count++;
            return 0;
        }

        _lines[_start] = line;
        _start = (_start + 1) % _lines.Length;
        return 1;
    }

    /// <summary>Changes the limit, keeping the newest rows.</summary>
    public void SetLimit(int limit)
    {
        var keep = Math.Min(Count, limit);
        var next = new Cell[limit][];
        for (var i = 0; i < keep; i++)
        {
            next[i] = this[Count - keep + i];
        }

        _lines = next;
        _start = 0;
        Count = keep;
    }

    /// <summary>Removes every row.</summary>
    public void Clear()
    {
        Array.Clear(_lines);
        _start = 0;
        Count = 0;
    }
}
