namespace AsciiCanvas;

/// <summary>
/// Rows of cells, oldest first, in a ring of at most <see cref="Limit"/> rows: when it is full, adding a row drops the oldest.
/// The ring grows as rows arrive, so a large limit costs nothing until it is used. Not thread-safe.
/// </summary>
internal sealed class ScrollbackBuffer
{
    private const int InitialCapacity = 64;

    private Cell[][] _lines = [];
    private int _start;

    /// <summary>The number of rows kept.</summary>
    public int Count { get; private set; }

    /// <summary>The most rows kept; older rows are dropped. 0 keeps none.</summary>
    public int Limit { get; private set; }

    /// <summary>A row by age: 0 is the oldest.</summary>
    public Cell[] this[int index] => _lines[(_start + index) % _lines.Length];

    /// <summary>Adds a row as the newest, dropping the oldest when the buffer holds <see cref="Limit"/> rows.</summary>
    public void Add(Cell[] line)
    {
        if (Limit == 0)
        {
            return;
        }

        if (Count == _lines.Length && Count < Limit)
        {
            Reallocate(Math.Min(Limit, Math.Max(InitialCapacity, Count * 2)), Count);
        }

        if (Count < _lines.Length)
        {
            _lines[(_start + Count) % _lines.Length] = line;
            Count++;
            return;
        }

        _lines[_start] = line;
        _start = (_start + 1) % _lines.Length;
    }

    /// <summary>Changes the limit, keeping the newest rows.</summary>
    public void SetLimit(int limit)
    {
        Limit = limit;
        var keep = Math.Min(Count, limit);
        Reallocate(keep, keep);
    }

    /// <summary>Removes every row. The capacity stays, so a buffer that is filled and emptied every frame does not allocate again.</summary>
    public void Clear()
    {
        Array.Clear(_lines);
        _start = 0;
        Count = 0;
    }

    // Copies the newest `keep` rows, oldest first, into a new array of `capacity` slots.
    private void Reallocate(int capacity, int keep)
    {
        var next = new Cell[capacity][];
        for (var i = 0; i < keep; i++)
        {
            next[i] = this[Count - keep + i];
        }

        _lines = next;
        _start = 0;
        Count = keep;
    }
}
