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
    private readonly ScrollbackBuffer _scrollback = new();

    // Rows that left the top since the last Present; they join the scrollback when the frame without them is published.
    // It has the same limit as the scrollback, because older rows would be dropped there anyway.
    private readonly ScrollbackBuffer _pendingScrollback = new();

    private bool _changed;
    private string _title = string.Empty;
    private ReadOnlyMemory<byte> _icon;
    private CanvasWindowState _windowState;
    private CellRect[] _dragRegions = [];
    private long _version;
    private int _cursorColumn;
    private int _cursorRow;
    private int _scrollbackOffset;

    // All rows scrolled off since the last Present, including those the pending buffer already dropped; a scrolled-back view moves by this.
    private int _pendingRowCount;
    private FrameInfo _frameInfo = new(0, 0, 0, true, CursorShape.Block);

    /// <summary>
    /// Raised after a <see cref="Present"/> that published new content, and when the view moves through the scrollback
    /// (<see cref="ScrollbackOffset"/>, <see cref="ScrollBack"/>, <see cref="ScrollbackLimit"/>, <see cref="ClearScrollback"/>),
    /// and when <see cref="Texture"/> changes.
    /// It comes on the thread that made the change, so hosts should marshal to their UI thread.
    /// </summary>
    public event Action? Presented;

    /// <summary>Raised when the program calls <see cref="Exit"/>. Hosts subscribe to close their window.</summary>
    public event Action? ExitRequested;

    /// <summary>Raised on the calling thread after <see cref="Title"/> changed, with the new title. Hosts should marshal to their UI thread.</summary>
    public event Action<string>? TitleChanged;

    /// <summary>Raised on the calling thread after <see cref="Icon"/> changed. Hosts should marshal to their UI thread.</summary>
    public event Action? IconChanged;

    /// <summary>Raised on the calling thread after <see cref="FontSize"/> changed, with the new size. Hosts should marshal to their UI thread.</summary>
    public event Action<double>? FontSizeChanged;

    /// <summary>Raised on the calling thread after <see cref="FontName"/> changed, with the new name. Hosts should marshal to their UI thread.</summary>
    public event Action<string>? FontNameChanged;

    /// <summary>
    /// Raised on the calling thread when the program asks for a grid size (<see cref="RequestGridSize"/>), with the columns and
    /// rows. Hosts should marshal to their UI thread and size the window to fit them.
    /// </summary>
    public event Action<int, int>? GridSizeRequested;

    /// <summary>Raised on the calling thread when the program sets <see cref="WindowState"/>. Hosts should marshal to their UI thread and change the window.</summary>
    public event Action<CanvasWindowState>? WindowStateRequested;

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
    /// The font size of the cells, in points. Set it at any time (a setting of the program, a zoom key); the host shows the
    /// cells at that size. With the host sizing <c>FillWithCells</c> the window keeps its size and the grid gets more or
    /// fewer columns and rows (a <see cref="ResizeEvent"/> follows); with <c>ScaleToFit</c> the cells keep filling the window.
    /// Starts with the size from the host's options, and is 16 for a canvas without a host.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a positive, finite number.</exception>
    public double FontSize
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
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The font size must be a positive number.");
            }

            bool changed;
            lock (_gate)
            {
                changed = field != value;
                field = value;
            }

            if (changed)
            {
                FontSizeChanged?.Invoke(value);
            }
        }
    } = 16;

    /// <summary>
    /// The font of the cells, by name: "Cascadia Mono" or "JetBrains Mono", the fonts the Avalonia host has built in, or empty
    /// (the default) for the font of the host's options. Set it at any time; the host shows the cells in it, and a name it does
    /// not know is ignored. As with <see cref="FontSize"/>, the window keeps its size and the grid gets the cells that fit.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string FontName
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
            ArgumentNullException.ThrowIfNull(value);
            bool changed;
            lock (_gate)
            {
                changed = field != value;
                field = value;
            }

            if (changed)
            {
                FontNameChanged?.Invoke(value);
            }
        }
    } = string.Empty;

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
    /// The state of the window. Reading gives the state the host last reported; setting asks the host to minimize, maximize or restore
    /// the window, and the host then reports the new state with a <see cref="WindowStateEvent"/>. Nothing happens without a host.
    /// </summary>
    public CanvasWindowState WindowState
    {
        get
        {
            lock (_gate)
            {
                return _windowState;
            }
        }

        set => WindowStateRequested?.Invoke(value);
    }

    /// <summary>
    /// The cells that move the window when the user drags them, for a window without a frame. Typically the title bar, without
    /// the buttons in it. Double-clicking a region maximizes or restores the window. A press in a region is handled by the host and
    /// does not reach <see cref="Input"/>. Empty by default; the host ignores it for a window with a frame. Set a new list whenever
    /// the layout changes, for example after a <see cref="ResizeEvent"/>; the list is copied.
    /// </summary>
    public IReadOnlyList<CellRect> WindowDragRegions
    {
        get
        {
            lock (_gate)
            {
                return _dragRegions;
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var copy = value.ToArray();
            lock (_gate)
            {
                if (!copy.AsSpan().SequenceEqual(_dragRegions))
                {
                    _dragRegions = copy;
                }
            }
        }
    }

    /// <summary>For hosts: reports the state of the window. A change is posted to <see cref="Input"/> as a <see cref="WindowStateEvent"/>.</summary>
    /// <param name="state">The state the window is in now.</param>
    public void ReportWindowState(CanvasWindowState state)
    {
        lock (_gate)
        {
            if (_windowState == state)
            {
                return;
            }

            _windowState = state;
        }

        Input.Post(new WindowStateEvent(state));
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

    /// <summary>
    /// When true, making the canvas shorter than the row of the cursor scrolls the content up, as a terminal does, so the cursor row
    /// becomes the last row instead of being cut off; with <see cref="ScrollbackLimit"/> above 0 the rows that leave the top go to the
    /// scrollback. Off by default (the content stays at the top-left). <see cref="TextTerminal"/> turns it on for its canvas.
    /// </summary>
    public bool KeepCursorRowOnShrink
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

    /// <summary>
    /// The number of rows at the bottom that stay in place while the view is scrolled back (<see cref="ScrollbackOffset"/>): a
    /// status line or an input line that the program keeps below its scrolling text. 0 (the default) scrolls the whole canvas.
    /// The scrollback then shows above those rows, and the cursor stays visible when it is in them.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int ScrollbackPinnedRows
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
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            bool viewChanged;
            lock (_gate)
            {
                viewChanged = field != value && _scrollbackOffset > 0;
                field = value;
                if (viewChanged)
                {
                    _version++;
                    _frameInfo = _frameInfo with { Version = _version };
                }
            }

            if (viewChanged)
            {
                Presented?.Invoke();
            }
        }
    }

    /// <summary>
    /// A still texture the host paints behind the cells that have the default background, or <see langword="null"/> (the default)
    /// for none. It belongs to the window, not to the cells: it stays in place while the content scrolls and while the view is
    /// scrolled back, and a change shows at once, without <see cref="Present"/>.
    /// </summary>
    public CanvasTexture? Texture
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
                if (field == value)
                {
                    return;
                }

                field = value;
            }

            Presented?.Invoke();
        }
    }

    // The rows the content has moved up in all by Resize (KeepCursorRowOnShrink), so a TextTerminal can move its own row with it.
    internal long RowsMovedUpByResize
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        private set;
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

    /// <summary>
    /// The most rows kept in the scrollback; 0 (the default) turns it off and clears it. With scrollback on, the rows that an upward
    /// <see cref="Scroll"/> of a full-width region starting at row 0 pushes off the top are kept, as a terminal does, and the user can
    /// scroll back to them (<see cref="ScrollbackOffset"/>). Hosts then use the mouse wheel and Shift+PageUp/PageDown for scrolling
    /// instead of passing them to the program, so leave it off in programs that need those.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int ScrollbackLimit
    {
        get
        {
            lock (_gate)
            {
                return _scrollback.Limit;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            bool viewChanged;
            lock (_gate)
            {
                if (value == _scrollback.Limit)
                {
                    return;
                }

                _scrollback.SetLimit(value);
                _pendingScrollback.SetLimit(value);
                if (value == 0)
                {
                    _pendingRowCount = 0;
                }

                viewChanged = SetOffsetCore(_scrollbackOffset);
            }

            if (viewChanged)
            {
                Presented?.Invoke();
            }
        }
    }

    /// <summary>The number of rows in the scrollback that can be shown (rows scrolled off since the last <see cref="Present"/> are not counted yet).</summary>
    public int ScrollbackCount
    {
        get
        {
            lock (_gate)
            {
                return _scrollback.Count;
            }
        }
    }

    /// <summary>
    /// How many rows the view is scrolled back into the scrollback: 0 shows the canvas itself, <see cref="ScrollbackCount"/> shows the
    /// oldest row at the top. Clamped to that range. Hosts change it for the user (the wheel, Shift+PageUp) and set it back to 0 when
    /// the user types. While it is above 0, new rows entering the scrollback keep the view on the same content.
    /// It changes only what <see cref="CopyFrame"/> shows, never the canvas, and the program's cursor is hidden when it is scrolled out of view.
    /// To move the view relative to where it is, use <see cref="ScrollBack"/>, which cannot miss rows arriving at the same time.
    /// </summary>
    public int ScrollbackOffset
    {
        get
        {
            lock (_gate)
            {
                return _scrollbackOffset;
            }
        }

        set
        {
            bool viewChanged;
            lock (_gate)
            {
                viewChanged = SetOffsetCore(value);
            }

            if (viewChanged)
            {
                Presented?.Invoke();
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

    /// <summary>
    /// The color of the text cursor. <see cref="CellColor.Default"/> (the default) draws a block cursor in the swapped colors
    /// of the cell under it and a bar or underline in the default foreground. Another color fills the block with it, with the
    /// character under it in the default background, and paints the bar or underline in it. Shown with the next
    /// <see cref="Present"/>, as <see cref="CursorShape"/> is, so a shape and a color changed together show together.
    /// </summary>
    public CellColor CursorColor
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

    /// <summary>
    /// Asks the host for a grid of this many columns and rows: the window is made just large enough for them at
    /// <see cref="FontSize"/> (and brought back from maximized or full screen first). The canvas gets the new size from the
    /// host, with a <see cref="ResizeEvent"/>, as for a window resized by hand. Without a host the canvas is resized at once.
    /// </summary>
    /// <param name="columns">The columns, at least 1.</param>
    /// <param name="rows">The rows, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">The columns or the rows are less than 1.</exception>
    public void RequestGridSize(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        if (GridSizeRequested is { } requested)
        {
            requested(columns, rows);
        }
        else
        {
            Resize(columns, rows);
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
    /// Changes the number of columns and rows, keeping content at the top-left, and presents the result. With
    /// <see cref="KeepCursorRowOnShrink"/>, a canvas made shorter than the cursor row is first scrolled up so the cursor row stays.
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

            var pinned = ScrollbackPinnedRows;
            if (pinned > 0 && pinned < Math.Min(rows, _back.Rows))
            {
                ResizeKeepingPinnedRows(columns, rows, pinned);
            }
            else
            {
                // A terminal keeps the line it writes on: rows leave the top, rather than the cursor row leaving the bottom.
                MoveUpForResize(KeepCursorRowOnShrink ? _cursorRow - (rows - 1) : 0, _back.Rows);
                _back.Resize(columns, rows, Pen);
            }

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
            _frameInfo = new FrameInfo(_version, _cursorColumn, _cursorRow, CursorVisible, CursorShape, CursorColor);
            PublishScrollback();
        }

        Presented?.Invoke();
    }

    /// <summary>
    /// For hosts: copies the most recently presented frame into <paramref name="destination"/>. When the view is scrolled back
    /// (<see cref="ScrollbackOffset"/>), the frame is the matching mix of scrollback rows and canvas rows, and the cursor is moved with it.
    /// </summary>
    /// <param name="destination">The grid to copy into; it is resized to match.</param>
    /// <returns>Cursor and version information of the frame.</returns>
    public FrameInfo CopyFrame(CellGrid destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (_gate)
        {
            destination.CopyFrom(_front);
            if (_scrollbackOffset == 0)
            {
                return _frameInfo;
            }

            ComposeScrolledView(destination);
            var scrolledRows = ScrolledRows(_front.Rows);
            if (_frameInfo.CursorRow >= scrolledRows)
            {
                // (in the pinned rows: it stays where it is)
                return _frameInfo;
            }

            var cursorRow = _frameInfo.CursorRow + _scrollbackOffset;
            return _frameInfo with { CursorRow = Math.Min(cursorRow, _front.Rows - 1), CursorVisible = _frameInfo.CursorVisible && cursorRow < scrolledRows };
        }
    }

    /// <summary>
    /// Moves the view <paramref name="rows"/> rows further back into the scrollback (negative moves toward the canvas), clamped like
    /// <see cref="ScrollbackOffset"/>. Hosts use it for the wheel and Shift+PageUp/PageDown: unlike reading and setting
    /// <see cref="ScrollbackOffset"/>, it is one step, so a <see cref="Present"/> on another thread cannot slip in between and move the view.
    /// </summary>
    /// <param name="rows">The number of rows to move back; negative moves forward.</param>
    public void ScrollBack(int rows)
    {
        bool viewChanged;
        lock (_gate)
        {
            viewChanged = SetOffsetCore((int)Math.Clamp((long)_scrollbackOffset + rows, 0, int.MaxValue));
        }

        if (viewChanged)
        {
            Presented?.Invoke();
        }
    }

    /// <summary>Removes every row from the scrollback and shows the canvas itself again.</summary>
    public void ClearScrollback()
    {
        bool viewChanged;
        lock (_gate)
        {
            _scrollback.Clear();
            _pendingScrollback.Clear();
            _pendingRowCount = 0;
            viewChanged = SetOffsetCore(0);
        }

        if (viewChanged)
        {
            Presented?.Invoke();
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
    /// Negative <paramref name="dy"/> scrolls up. With <see cref="ScrollbackLimit"/> above 0, scrolling a full-width region that starts
    /// at row 0 straight up keeps the rows that leave the top in the scrollback.
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

            // The terminal rule: only rows leaving the top of the screen are history; a scrolled window further down is not.
            if (dx == 0 && dy < 0 && area.Y == 0 && area.X == 0 && area.Width == _back.Columns)
            {
                KeepRowsLeavingTheTop(Math.Min(-dy, area.Height));
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

    // Called under the lock. Returns true if the view changed, so the caller raises Presented after leaving the lock.
    private bool SetOffsetCore(int offset)
    {
        offset = Math.Clamp(offset, 0, _scrollback.Count);
        if (offset == _scrollbackOffset)
        {
            return false;
        }

        _scrollbackOffset = offset;
        _version++;
        _frameInfo = _frameInfo with { Version = _version };
        return true;
    }

    // Called under the lock: the pinned rows at the bottom stay at the bottom. The rows above them get fewer or more; with
    // KeepCursorRowOnShrink, fewer rows first take the blank rows at the bottom of that part (one is left, for the next line
    // written), and only then move its top rows into the scrollback.
    private void ResizeKeepingPinnedRows(int columns, int rows, int pinned)
    {
        var pinnedTop = _back.Rows - pinned;
        var bottom = CaptureCore(new CellRect(0, pinnedTop, _back.Columns, pinned));
        var cursorPinned = _cursorRow >= pinnedTop;
        var removed = pinnedTop - (rows - pinned);
        if (removed > 0 && KeepCursorRowOnShrink)
        {
            var lowest = cursorPinned ? 0 : _cursorRow + 1;
            var blank = 0;
            while (blank < pinnedTop - lowest && IsBlankRow(pinnedTop - 1 - blank))
            {
                blank++;
            }

            var shift = removed - Math.Max(0, blank - 1);
            if (!cursorPinned)
            {
                shift = Math.Max(shift, _cursorRow - (rows - pinned - 1));
            }

            MoveUpForResize(shift, pinnedTop);
        }

        FillCore(new CellRect(0, pinnedTop, _back.Columns, pinned), new Rune(' '), Pen, ignoreClip: true);
        _back.Resize(columns, rows, Pen);
        var target = new CellRect(0, rows - pinned, columns, pinned);
        BlitCore(bottom, bottom.Bounds, 0, rows - pinned, target, skipBlanks: false);
        if (cursorPinned)
        {
            _cursorRow += rows - pinned - pinnedTop;
        }
    }

    // Called under the lock: moves the top `rows` rows of the region [0, height) into the scrollback and the rest up, for a resize.
    private void MoveUpForResize(int shift, int height)
    {
        if (shift <= 0)
        {
            return;
        }

        var area = new CellRect(0, 0, _back.Columns, height);
        KeepRowsLeavingTheTop(Math.Min(shift, height));
        var saved = CaptureCore(area);
        FillCore(area, new Rune(' '), Pen, ignoreClip: true);
        BlitCore(saved, saved.Bounds, 0, -shift, area, skipBlanks: false);
        if (_cursorRow < height)
        {
            _cursorRow -= shift;
        }

        RowsMovedUpByResize += shift;
    }

    // Called under the lock: a row of the back buffer with nothing written in it.
    private bool IsBlankRow(int y)
    {
        foreach (var cell in _back.Row(y))
        {
            if (cell.Rune.Value is not (0 or ' '))
            {
                return false;
            }
        }

        return true;
    }

    // Called under the lock before the top `count` rows of the back buffer scroll off the screen: they become scrollback at the next Present.
    private void KeepRowsLeavingTheTop(int count)
    {
        if (_scrollback.Limit == 0)
        {
            return;
        }

        for (var y = 0; y < count; y++)
        {
            _pendingScrollback.Add(_back.Row(y).ToArray());
            _pendingRowCount = _pendingRowCount == int.MaxValue ? int.MaxValue : _pendingRowCount + 1;
        }
    }

    // Called under the lock when a frame is published: the rows that left that frame become visible history.
    private void PublishScrollback()
    {
        if (_pendingRowCount == 0)
        {
            return;
        }

        for (var i = 0; i < _pendingScrollback.Count; i++)
        {
            _scrollback.Add(_pendingScrollback[i]);
        }

        // A view that is scrolled back stays on the same content while new rows arrive below it, until that content is dropped.
        var offset = _scrollbackOffset == 0 ? 0 : (long)_scrollbackOffset + _pendingRowCount;
        _pendingScrollback.Clear();
        _pendingRowCount = 0;
        _scrollbackOffset = (int)Math.Min(offset, _scrollback.Count);
    }

    // Called under the lock: the rows of the view that scroll back, above the pinned rows (at least one).
    private int ScrolledRows(int rows) => Math.Max(1, rows - ScrollbackPinnedRows);

    // Called under the lock: destination holds the front buffer; shift it down and put the scrollback rows above it. The pinned
    // rows at the bottom keep the front buffer's rows.
    private void ComposeScrolledView(CellGrid destination)
    {
        var columns = destination.Columns;
        var rows = ScrolledRows(destination.Rows);
        for (var y = rows - 1; y >= 0; y--)
        {
            var line = _scrollback.Count - _scrollbackOffset + y;
            if (line >= _scrollback.Count)
            {
                var source = line - _scrollback.Count;
                for (var x = 0; x < columns; x++)
                {
                    destination[x, y] = _front[x, source];
                }

                continue;
            }

            // Rows keep the width they had, so a row from a wider or narrower canvas is cut or padded.
            var cells = _scrollback[line];
            for (var x = 0; x < columns; x++)
            {
                destination[x, y] = x < cells.Length ? cells[x] : Cell.Blank();
            }

            if (cells.Length > columns && destination[columns - 1, y].Width == CellWidth.WideLead)
            {
                destination[columns - 1, y] = Cell.Blank(destination[columns - 1, y].Style);
            }
        }
    }

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
