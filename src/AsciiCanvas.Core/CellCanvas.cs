using System.Text;

namespace AsciiCanvas;

/// <summary>
/// The surface programs draw on: a grid of character cells with drawing primitives, a text cursor and an input queue.
/// <para>
/// Drawing goes to a back buffer. <see cref="Present"/> publishes it to the host, so partially drawn frames are never shown.
/// All members are thread-safe; typically a program draws from a background thread while the host renders on the UI thread.
/// </para>
/// </summary>
/// <param name="columns">The initial number of columns.</param>
/// <param name="rows">The initial number of rows.</param>
public sealed class CellCanvas(int columns = 80, int rows = 25) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly CellGrid _back = new(columns, rows);
    private readonly CellGrid _front = new(columns, rows);
    private readonly List<CellRect> _clips = [];
    private readonly CancellationTokenSource _closed = new();

    private bool _changed;
    private string _title = string.Empty;
    private ReadOnlyMemory<byte> _icon;
    private long _version;
    private int _cursorColumn;
    private int _cursorRow;
    private FrameInfo _frameInfo = new(0, 0, 0, true, CursorShape.Block);

    /// <summary>Raised on the drawing thread after a <see cref="Present"/> that published new content. Hosts should marshal to their UI thread.</summary>
    public event Action? Presented;

    /// <summary>Raised when the program calls <see cref="Exit"/>. Hosts subscribe to close their window.</summary>
    public event Action? ExitRequested;

    /// <summary>Raised on the calling thread after <see cref="Title"/> changed, with the new title. Hosts should marshal to their UI thread.</summary>
    public event Action<string>? TitleChanged;

    /// <summary>Raised on the calling thread after <see cref="Icon"/> changed. Hosts should marshal to their UI thread.</summary>
    public event Action? IconChanged;

    /// <summary>Input from the user. Hosts post to it; programs read from it.</summary>
    public CanvasInput Input { get; } = new();

    /// <summary>
    /// The title of the window that shows the canvas. Set it at any time, for example to show a file name; the host updates its
    /// window. Starts with the title from the host's options, and is empty for a canvas without a host.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string Title
    {
        get
        {
            lock (_gate)
            {
                return _title;
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            bool changed;
            lock (_gate)
            {
                changed = _title != value;
                _title = value;
            }

            if (changed)
            {
                TitleChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// The icon of the window that shows the canvas, as the bytes of an image file (PNG or ICO). Set it at any time; the host
    /// updates its window. Empty (the default) leaves the host's default, which on Windows is the icon of the executable.
    /// The bytes are copied, and an image the host cannot read is ignored.
    /// </summary>
    public ReadOnlyMemory<byte> Icon
    {
        get
        {
            lock (_gate)
            {
                return _icon;
            }
        }

        set
        {
            bool changed;
            lock (_gate)
            {
                changed = !_icon.Span.SequenceEqual(value.Span);
                _icon = value.ToArray();
            }

            if (changed)
            {
                IconChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// When true, the user's attempt to close the window does not close it. The host sends a <see cref="CloseRequestedEvent"/>
    /// instead, and the program decides, for example by asking about unsaved changes, and calls <see cref="Exit"/> to close.
    /// Off by default. A program that sets it must keep reading <see cref="Input"/>, or the window cannot be closed.
    /// </summary>
    public bool InterceptClose
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        set
        {
            lock (_gate)
            {
                field = value;
            }
        }
    }

    /// <summary>The system clipboard, if the host provides one. <see langword="null"/> otherwise (for example in tests).</summary>
    public ICanvasClipboard? Clipboard { get; set; }

    /// <summary>Canceled when the host closes the canvas (for example, the window closes). Not usable after <see cref="Dispose"/>.</summary>
    public CancellationToken Closed => _closed.Token;

    /// <summary>The current number of columns.</summary>
    public int Columns
    {
        get
        {
            lock (_gate)
            {
                return _back.Columns;
            }
        }
    }

    /// <summary>The current number of rows.</summary>
    public int Rows
    {
        get
        {
            lock (_gate)
            {
                return _back.Rows;
            }
        }
    }

    /// <summary>The area of the whole canvas.</summary>
    public CellRect Bounds
    {
        get
        {
            lock (_gate)
            {
                return _back.Bounds;
            }
        }
    }

    /// <summary>The style used by every drawing method when no explicit style is passed.</summary>
    public CellStyle Pen
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        set
        {
            lock (_gate)
            {
                field = value;
            }
        }
    }

    /// <summary>The column of the text cursor.</summary>
    public int CursorColumn
    {
        get
        {
            lock (_gate)
            {
                return _cursorColumn;
            }
        }
    }

    /// <summary>The row of the text cursor.</summary>
    public int CursorRow
    {
        get
        {
            lock (_gate)
            {
                return _cursorRow;
            }
        }
    }

    /// <summary>Whether the host shows the text cursor.</summary>
    public bool CursorVisible
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        set
        {
            lock (_gate)
            {
                field = value;
                _changed = true;
            }
        }
    } = true;

    /// <summary>The shape of the text cursor.</summary>
    public CursorShape CursorShape
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        set
        {
            lock (_gate)
            {
                field = value;
                _changed = true;
            }
        }
    }

    /// <summary>Moves the text cursor, clamped to the canvas.</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    public void SetCursor(int column, int row)
    {
        lock (_gate)
        {
            _cursorColumn = Math.Clamp(column, 0, _back.Columns - 1);
            _cursorRow = Math.Clamp(row, 0, _back.Rows - 1);
            _changed = true;
        }
    }

    /// <summary>Asks the host to close the window now, without a <see cref="CloseRequestedEvent"/> round trip. Call it when the program is done or the user confirmed leaving.</summary>
    public void Exit() => ExitRequested?.Invoke();

    /// <summary>Called by hosts when the canvas is going away. Completes <see cref="Input"/> and cancels <see cref="Closed"/>.</summary>
    public void Close()
    {
        Input.Complete();
        _closed.Cancel();
    }

    /// <summary>Closes the canvas and releases its resources.</summary>
    public void Dispose()
    {
        Close();
        _closed.Dispose();
    }

    /// <summary>
    /// Changes the number of columns and rows, keeping content at the top-left, and presents the result.
    /// A <see cref="ResizeEvent"/> is posted to <see cref="Input"/>.
    /// </summary>
    /// <param name="columns">The new number of columns (at least 1).</param>
    /// <param name="rows">The new number of rows (at least 1).</param>
    public void Resize(int columns, int rows)
    {
        lock (_gate)
        {
            if (columns == _back.Columns && rows == _back.Rows)
            {
                return;
            }

            _back.Resize(columns, rows, Pen);
            _clips.Clear();
            _cursorColumn = Math.Min(_cursorColumn, columns - 1);
            _cursorRow = Math.Min(_cursorRow, rows - 1);
            _changed = true;
        }

        Input.Post(new ResizeEvent(columns, rows));
        Present();
    }

    /// <summary>Publishes everything drawn so far so the host can show it. Does nothing if nothing changed.</summary>
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
            _frameInfo = new FrameInfo(_version, _cursorColumn, _cursorRow, CursorVisible, CursorShape);
        }

        Presented?.Invoke();
    }

    /// <summary>For hosts: copies the most recently presented frame into <paramref name="destination"/>.</summary>
    /// <param name="destination">The grid to copy into; it is resized to match.</param>
    /// <returns>Cursor and version information of the frame.</returns>
    public FrameInfo CopyFrame(CellGrid destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (_gate)
        {
            destination.CopyFrom(_front);
            return _frameInfo;
        }
    }

    /// <summary>Restricts all drawing to <paramref name="rect"/> (intersected with any current clip) until the scope is disposed.</summary>
    /// <param name="rect">The area that stays drawable.</param>
    /// <returns>A scope that restores the previous clip when disposed.</returns>
    public ClipScope PushClip(CellRect rect)
    {
        lock (_gate)
        {
            _clips.Add(rect.Intersect(CurrentClip));
            return new ClipScope(this, _clips.Count);
        }
    }

    /// <summary>Fills the entire canvas with blanks (ignores the clip).</summary>
    /// <param name="style">The style of the blanks; defaults to <see cref="Pen"/>.</param>
    public void Clear(CellStyle? style = null)
    {
        lock (_gate)
        {
            _back.Fill(Cell.Blank(style ?? Pen));
            _changed = true;
        }
    }

    /// <summary>Gets a cell of the back buffer; positions outside the canvas give a blank cell.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public Cell GetCell(int x, int y)
    {
        lock (_gate)
        {
            return _back.InBounds(x, y) ? _back[x, y] : Cell.Blank();
        }
    }

    /// <summary>Draws one character. Does nothing if the position is outside the canvas or clip.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="c">The character.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void SetCell(int x, int y, char c, CellStyle? style = null) => SetCell(x, y, new Rune(c), style);

    /// <summary>Draws one character. Does nothing if the position is outside the canvas or clip.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="rune">The character.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void SetCell(int x, int y, Rune rune, CellStyle? style = null)
    {
        lock (_gate)
        {
            PutClipped(x, y, rune, style ?? Pen, CurrentClip);
            _changed = true;
        }
    }

    /// <summary>Draws a single line of text. Control characters and zero-width characters are skipped.</summary>
    /// <param name="x">The column of the first character.</param>
    /// <param name="y">The row.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    /// <returns>The column just after the text (even if it was clipped).</returns>
    public int Write(int x, int y, string text, CellStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Write(x, y, text.AsSpan(), style);
    }

    /// <summary>Draws a single line of text. Control characters and zero-width characters are skipped.</summary>
    /// <param name="x">The column of the first character.</param>
    /// <param name="y">The row.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    /// <returns>The column just after the text (even if it was clipped).</returns>
    public int Write(int x, int y, ReadOnlySpan<char> text, CellStyle? style = null)
    {
        lock (_gate)
        {
            var s = style ?? Pen;
            var clip = CurrentClip;
            foreach (var rune in text.EnumerateRunes())
            {
                var width = CharWidth.Of(rune);
                if (width == 0)
                {
                    continue;
                }

                PutClipped(x, y, rune, s, clip);
                x += width;
            }

            _changed = true;
            return x;
        }
    }

    /// <summary>Fills a rectangle with a character.</summary>
    /// <param name="rect">The area to fill.</param>
    /// <param name="c">The character; a space by default.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void Fill(CellRect rect, char c = ' ', CellStyle? style = null) => Fill(rect, new Rune(c), style);

    /// <summary>Fills a rectangle with a character.</summary>
    /// <param name="rect">The area to fill.</param>
    /// <param name="rune">The character.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void Fill(CellRect rect, Rune rune, CellStyle? style = null)
    {
        lock (_gate)
        {
            FillCore(rect, rune, style ?? Pen);
            _changed = true;
        }
    }

    /// <summary>Draws a horizontal line.</summary>
    /// <param name="x">The column of the left end.</param>
    /// <param name="y">The row.</param>
    /// <param name="length">The number of cells.</param>
    /// <param name="line">The line style.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void HLine(int x, int y, int length, BoxStyle line = BoxStyle.SingleLine, CellStyle? style = null)
    {
        lock (_gate)
        {
            var glyph = new Rune(BoxChars.For(line).Horizontal);
            FillCore(new CellRect(x, y, length, 1), glyph, style ?? Pen);
            _changed = true;
        }
    }

    /// <summary>Draws a vertical line.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row of the top end.</param>
    /// <param name="length">The number of cells.</param>
    /// <param name="line">The line style.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    public void VLine(int x, int y, int length, BoxStyle line = BoxStyle.SingleLine, CellStyle? style = null)
    {
        lock (_gate)
        {
            var glyph = new Rune(BoxChars.For(line).Vertical);
            FillCore(new CellRect(x, y, 1, length), glyph, style ?? Pen);
            _changed = true;
        }
    }

    /// <summary>Draws a rectangular frame, optionally with a title in the top edge and the inside cleared.</summary>
    /// <param name="rect">The outer area of the frame (at least 2 x 2, otherwise nothing is drawn).</param>
    /// <param name="box">The line style.</param>
    /// <param name="style">The style; defaults to <see cref="Pen"/>.</param>
    /// <param name="title">Text shown in the top edge, truncated to fit.</param>
    /// <param name="fill">True to clear the inside of the frame.</param>
    public void Box(CellRect rect, BoxStyle box = BoxStyle.SingleLine, CellStyle? style = null, string? title = null, bool fill = false)
    {
        if (rect.Width < 2 || rect.Height < 2)
        {
            return;
        }

        lock (_gate)
        {
            var s = style ?? Pen;
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
    /// <param name="source">The grid to copy from.</param>
    /// <param name="x">The column of the top-left corner on the canvas.</param>
    /// <param name="y">The row of the top-left corner on the canvas.</param>
    /// <param name="sourceRect">The part of <paramref name="source"/> to copy; all of it by default.</param>
    /// <param name="skipBlanks">True to leave the canvas unchanged where the source has blank cells (a transparent sprite).</param>
    public void Blit(CellGrid source, int x, int y, CellRect? sourceRect = null, bool skipBlanks = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            BlitCore(source, sourceRect ?? source.Bounds, x, y, CurrentClip, skipBlanks);
            _changed = true;
        }
    }

    /// <summary>Copies a region of the back buffer. Parts outside the canvas come back blank.</summary>
    /// <param name="rect">The area to copy.</param>
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
    /// <param name="region">The area to scroll.</param>
    /// <param name="dx">Columns to move right.</param>
    /// <param name="dy">Rows to move down.</param>
    /// <param name="style">The style of the vacated cells; defaults to <see cref="Pen"/>.</param>
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
            FillCore(area, new Rune(' '), style ?? Pen, ignoreClip: true);
            BlitCore(saved, saved.Bounds, area.X + dx, area.Y + dy, area, skipBlanks: false);
            _changed = true;
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

    private CellRect CurrentClip => _clips.Count == 0 ? _back.Bounds : _clips[^1];

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
        var clip = ignoreClip ? _back.Bounds : CurrentClip;
        var area = rect.Intersect(clip);
        if (area.IsEmpty)
        {
            return;
        }

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
}
