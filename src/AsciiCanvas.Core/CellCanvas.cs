using System.Text;

namespace AsciiCanvas;

public enum CursorShape
{
    Block,
    Underline,
    Bar,
}

/// <summary>Metadata for a presented frame. <see cref="Version"/> increases with every <see cref="CellCanvas.Present"/> that changed something.</summary>
public readonly record struct FrameInfo(
    long Version,
    int CursorColumn,
    int CursorRow,
    bool CursorVisible,
    CursorShape CursorShape);

/// <summary>
/// The surface programs draw on: a grid of character cells with drawing primitives, a text cursor and an input queue.
/// <para>
/// Drawing goes to a back buffer. <see cref="Present"/> publishes it to the host, so partially drawn frames are never shown.
/// All members are thread-safe; typically a program draws from a background thread while the host renders on the UI thread.
/// </para>
/// </summary>
public sealed class CellCanvas
{
    private readonly Lock _gate = new();
    private readonly CellGrid _back;
    private readonly CellGrid _front;
    private readonly List<CellRect> _clips = [];
    private readonly CancellationTokenSource _closed = new();

    private CellStyle _pen;
    private bool _changed;
    private long _version;
    private int _cursorColumn;
    private int _cursorRow;
    private bool _cursorVisible = true;
    private CursorShape _cursorShape;
    private FrameInfo _frameInfo;

    public CellCanvas(int columns = 80, int rows = 25)
    {
        _back = new CellGrid(columns, rows);
        _front = new CellGrid(columns, rows);
        _frameInfo = new FrameInfo(0, 0, 0, true, CursorShape.Block);
    }

    /// <summary>Raised on the drawing thread after a <see cref="Present"/> that published new content. Hosts should marshal to their UI thread.</summary>
    public event Action? Presented;

    /// <summary>Input from the user. Hosts post to it; programs read from it.</summary>
    public InputQueue Input { get; } = new();

    /// <summary>Canceled when the host closes the canvas (for example, the window closes).</summary>
    public CancellationToken Closed => _closed.Token;

    public int Columns
    {
        get { lock (_gate) { return _back.Columns; } }
    }

    public int Rows
    {
        get { lock (_gate) { return _back.Rows; } }
    }

    public CellRect Bounds
    {
        get { lock (_gate) { return _back.Bounds; } }
    }

    /// <summary>The style used by every drawing method when no explicit style is passed.</summary>
    public CellStyle Pen
    {
        get { lock (_gate) { return _pen; } }
        set { lock (_gate) { _pen = value; } }
    }

    public int CursorColumn
    {
        get { lock (_gate) { return _cursorColumn; } }
    }

    public int CursorRow
    {
        get { lock (_gate) { return _cursorRow; } }
    }

    public bool CursorVisible
    {
        get { lock (_gate) { return _cursorVisible; } }
        set { lock (_gate) { _cursorVisible = value; _changed = true; } }
    }

    public CursorShape CursorShape
    {
        get { lock (_gate) { return _cursorShape; } }
        set { lock (_gate) { _cursorShape = value; _changed = true; } }
    }

    public void SetCursor(int column, int row)
    {
        lock (_gate)
        {
            _cursorColumn = Math.Clamp(column, 0, _back.Columns - 1);
            _cursorRow = Math.Clamp(row, 0, _back.Rows - 1);
            _changed = true;
        }
    }

    /// <summary>Called by hosts when the canvas is going away. Completes <see cref="Input"/> and cancels <see cref="Closed"/>.</summary>
    public void Close()
    {
        Input.Complete();
        _closed.Cancel();
    }

    // ---- Size ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Changes the number of columns and rows, keeping content at the top-left, and presents the result.
    /// A <see cref="ResizeEvent"/> is posted to <see cref="Input"/>.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        lock (_gate)
        {
            if (columns == _back.Columns && rows == _back.Rows)
            {
                return;
            }

            _back.Resize(columns, rows, _pen);
            _clips.Clear();
            _cursorColumn = Math.Min(_cursorColumn, columns - 1);
            _cursorRow = Math.Min(_cursorRow, rows - 1);
            _changed = true;
        }

        Input.Post(new ResizeEvent(columns, rows));
        Present();
    }

    // ---- Presenting ----------------------------------------------------------------------------------------

    /// <summary>Publishes everything drawn so far so the host can show it.</summary>
    public void Present()
    {
        lock (_gate)
        {
            if (!_changed)
            {
                return;
            }

            _front.CopyFrom(_back);
            _changed = false;
            _version++;
            _frameInfo = new FrameInfo(_version, _cursorColumn, _cursorRow, _cursorVisible, _cursorShape);
        }

        Presented?.Invoke();
    }

    /// <summary>For hosts: copies the most recently presented frame into <paramref name="destination"/>.</summary>
    public FrameInfo CopyFrame(CellGrid destination)
    {
        lock (_gate)
        {
            destination.CopyFrom(_front);
            return _frameInfo;
        }
    }

    // ---- Clipping ------------------------------------------------------------------------------------------

    /// <summary>Restricts all drawing to <paramref name="rect"/> (intersected with any current clip) until the scope is disposed.</summary>
    public ClipScope PushClip(CellRect rect)
    {
        lock (_gate)
        {
            _clips.Add(rect.Intersect(CurrentClip));
            return new ClipScope(this, _clips.Count);
        }
    }

    internal void PopClip(int depth)
    {
        lock (_gate)
        {
            if (_clips.Count >= depth)
            {
                _clips.RemoveRange(depth - 1, _clips.Count - depth + 1);
            }
        }
    }

    private CellRect CurrentClip => _clips.Count == 0 ? _back.Bounds : _clips[^1];

    // ---- Drawing -------------------------------------------------------------------------------------------

    /// <summary>Fills the entire canvas with blanks (ignores the clip).</summary>
    public void Clear(CellStyle? style = null)
    {
        lock (_gate)
        {
            _back.Fill(Cell.Blank(style ?? _pen));
            _changed = true;
        }
    }

    public Cell GetCell(int x, int y)
    {
        lock (_gate)
        {
            return _back.InBounds(x, y) ? _back[x, y] : Cell.Blank();
        }
    }

    public void SetCell(int x, int y, char c, CellStyle? style = null) => SetCell(x, y, new Rune(c), style);

    public void SetCell(int x, int y, Rune rune, CellStyle? style = null)
    {
        lock (_gate)
        {
            PutClipped(x, y, rune, style ?? _pen, CurrentClip);
            _changed = true;
        }
    }

    /// <summary>
    /// Draws a single line of text. Control characters and zero-width characters are skipped.
    /// Returns the column just after the text (even if it was clipped).
    /// </summary>
    public int Write(int x, int y, string text, CellStyle? style = null) => Write(x, y, text.AsSpan(), style);

    public int Write(int x, int y, ReadOnlySpan<char> text, CellStyle? style = null)
    {
        lock (_gate)
        {
            var s = style ?? _pen;
            var clip = CurrentClip;
            foreach (var rune in text.EnumerateRunes())
            {
                var width = CharWidth.Of(rune);
                if (width == 0)
                {
                    continue;
                }

                if (clip.Contains(x, y))
                {
                    PutClipped(x, y, rune, s, clip);
                }

                x += width;
            }

            _changed = true;
            return x;
        }
    }

    /// <summary>Fills a rectangle with a character.</summary>
    public void Fill(CellRect rect, char c = ' ', CellStyle? style = null) => Fill(rect, new Rune(c), style);

    public void Fill(CellRect rect, Rune rune, CellStyle? style = null)
    {
        lock (_gate)
        {
            FillCore(rect, rune, style ?? _pen);
            _changed = true;
        }
    }

    public void HLine(int x, int y, int length, BoxStyle line = BoxStyle.Single, CellStyle? style = null)
    {
        lock (_gate)
        {
            var glyph = new Rune(BoxChars.For(line).Horizontal);
            FillCore(new CellRect(x, y, length, 1), glyph, style ?? _pen);
            _changed = true;
        }
    }

    public void VLine(int x, int y, int length, BoxStyle line = BoxStyle.Single, CellStyle? style = null)
    {
        lock (_gate)
        {
            var glyph = new Rune(BoxChars.For(line).Vertical);
            FillCore(new CellRect(x, y, 1, length), glyph, style ?? _pen);
            _changed = true;
        }
    }

    /// <summary>Draws a rectangular frame, optionally with a title in the top edge and the inside cleared.</summary>
    public void Box(CellRect rect, BoxStyle box = BoxStyle.Single, CellStyle? style = null, string? title = null, bool fill = false)
    {
        if (rect.Width < 2 || rect.Height < 2)
        {
            return;
        }

        lock (_gate)
        {
            var s = style ?? _pen;
            var chars = BoxChars.For(box);
            var clip = CurrentClip;

            if (fill)
            {
                FillCore(rect.Inflate(-1, -1), new Rune(' '), s);
            }

            FillCore(new CellRect(rect.X + 1, rect.Y, rect.Width - 2, 1), new Rune(chars.Horizontal), s);
            FillCore(new CellRect(rect.X + 1, rect.Bottom - 1, rect.Width - 2, 1), new Rune(chars.Horizontal), s);
            FillCore(new CellRect(rect.X, rect.Y + 1, 1, rect.Height - 2), new Rune(chars.Vertical), s);
            FillCore(new CellRect(rect.Right - 1, rect.Y + 1, 1, rect.Height - 2), new Rune(chars.Vertical), s);
            PutClipped(rect.X, rect.Y, new Rune(chars.TopLeft), s, clip);
            PutClipped(rect.Right - 1, rect.Y, new Rune(chars.TopRight), s, clip);
            PutClipped(rect.X, rect.Bottom - 1, new Rune(chars.BottomLeft), s, clip);
            PutClipped(rect.Right - 1, rect.Bottom - 1, new Rune(chars.BottomRight), s, clip);

            if (!string.IsNullOrEmpty(title) && rect.Width >= 5)
            {
                var label = " " + TruncateToWidth(title, rect.Width - 4) + " ";
                Write(rect.X + 1, rect.Y, label, s);
            }

            _changed = true;
        }
    }

    /// <summary>Copies a region of a grid onto the canvas with its top-left at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="skipBlanks">Leave the canvas unchanged where the source has blank cells (a transparent sprite).</param>
    public void Blit(CellGrid source, int x, int y, CellRect? sourceRect = null, bool skipBlanks = false)
    {
        lock (_gate)
        {
            BlitCore(source, sourceRect ?? source.Bounds, x, y, CurrentClip, skipBlanks);
            _changed = true;
        }
    }

    /// <summary>Copies a region of the back buffer. Parts outside the canvas come back blank.</summary>
    public CellGrid Capture(CellRect rect)
    {
        lock (_gate)
        {
            return CaptureCore(rect);
        }
    }

    /// <summary>
    /// Moves the content of a region by (<paramref name="dx"/>, <paramref name="dy"/>) cells; the vacated area is cleared.
    /// Negative <paramref name="dy"/> scrolls up.
    /// </summary>
    public void Scroll(CellRect region, int dx, int dy, CellStyle? style = null)
    {
        lock (_gate)
        {
            var area = region.Intersect(CurrentClip);
            if (area.IsEmpty)
            {
                return;
            }

            var saved = CaptureCore(area);
            FillCore(area, new Rune(' '), style ?? _pen, ignoreClip: true);
            BlitCore(saved, saved.Bounds, area.X + dx, area.Y + dy, area, skipBlanks: false);
            _changed = true;
        }
    }

    // ---- Internals (callers hold the lock) -----------------------------------------------------------------

    private void PutClipped(int x, int y, Rune rune, CellStyle style, CellRect clip)
    {
        if (!clip.Contains(x, y))
        {
            return;
        }

        // A wide character that would stick out of the clip is shown as a blank instead of a half glyph.
        if (CharWidth.Of(rune) == 2 && x + 1 >= clip.Right)
        {
            rune = new Rune(' ');
        }

        _back.Put(x, y, rune, style);
    }

    private void FillCore(CellRect rect, Rune rune, CellStyle style, bool ignoreClip = false)
    {
        var area = rect.Intersect(ignoreClip ? _back.Bounds : CurrentClip);
        if (area.IsEmpty)
        {
            return;
        }

        var clip = ignoreClip ? _back.Bounds : CurrentClip;
        var width = Math.Max(1, CharWidth.Of(rune));
        for (var y = area.Y; y < area.Bottom; y++)
        {
            for (var x = area.X; x < area.Right; x += width)
            {
                PutClipped(x, y, rune, style, clip);
            }
        }
    }

    private CellGrid CaptureCore(CellRect rect)
    {
        var result = new CellGrid(Math.Max(1, rect.Width), Math.Max(1, rect.Height));
        var overlap = rect.Intersect(_back.Bounds);
        for (var y = overlap.Y; y < overlap.Bottom; y++)
        {
            for (var x = overlap.X; x < overlap.Right; x++)
            {
                result[x - rect.X, y - rect.Y] = _back[x, y];
            }
        }

        return result;
    }

    private void BlitCore(CellGrid source, CellRect sourceRect, int dx, int dy, CellRect clip, bool skipBlanks)
    {
        var src = sourceRect.Intersect(source.Bounds);
        for (var sy = src.Y; sy < src.Bottom; sy++)
        {
            var ty = dy + (sy - sourceRect.Y);
            for (var sx = src.X; sx < src.Right; sx++)
            {
                var tx = dx + (sx - sourceRect.X);
                if (!clip.Contains(tx, ty))
                {
                    continue;
                }

                var cell = source[sx, sy];
                if (skipBlanks && cell.IsBlank)
                {
                    continue;
                }

                switch (cell.Width)
                {
                    case CellWidth.WideTrail:
                        // The lead already drew both halves, unless it was cut off by the source or clip edge.
                        if (sx == sourceRect.X || !clip.Contains(tx - 1, ty))
                        {
                            _back.Put(tx, ty, new Rune(' '), cell.Style);
                        }

                        break;

                    case CellWidth.WideLead when sx + 1 >= src.Right || tx + 1 >= clip.Right:
                        _back.Put(tx, ty, new Rune(' '), cell.Style);
                        break;

                    default:
                        _back.Put(tx, ty, cell.Rune.Value == 0 ? new Rune(' ') : cell.Rune, cell.Style);
                        break;
                }
            }
        }
    }

    private static string TruncateToWidth(string text, int columns)
    {
        var width = 0;
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var w = CharWidth.Of(rune);
            if (width + w > columns)
            {
                break;
            }

            width += w;
            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }
}

/// <summary>Disposing restores the clip that was active before <see cref="CellCanvas.PushClip"/>.</summary>
public readonly struct ClipScope : IDisposable
{
    private readonly CellCanvas? _canvas;
    private readonly int _depth;

    internal ClipScope(CellCanvas canvas, int depth)
    {
        _canvas = canvas;
        _depth = depth;
    }

    public void Dispose() => _canvas?.PopClip(_depth);
}
