using System.Text;

namespace AsciiCanvas;

/// <summary>
/// A console-like layer over a <see cref="CellCanvas"/>: a text cursor with wrapping and scrolling output,
/// plus key and line input. It uses the canvas <see cref="CellCanvas.Pen"/> as the text style.
/// Only one task should read input at a time.
/// </summary>
/// <param name="canvas">The canvas to write to and read input from.</param>
public sealed class TextTerminal(CellCanvas canvas)
{
    private const int TabSize = 8;

    private readonly List<string> _history = [];

    // Key presses read from the input but not handed out yet; a paste becomes several.
    private readonly List<KeyPress> _pending = [];
    private int _pendingIndex;
    private int _column;
    private int _row;
    private int _scrolls;
    private long? _movedByResize; // RowsMovedUpByResize of the canvas when last seen (null: not yet)

    /// <summary>The canvas this terminal writes to. Its <see cref="CellCanvas.KeepCursorRowOnShrink"/> is turned on, so a shorter window keeps the line being written.</summary>
    public CellCanvas Canvas { get; } = KeepingCursorRow(canvas);

    /// <summary>A <see cref="TextWriter"/> that writes to this terminal, e.g. for code that expects <c>Console.Out</c>.</summary>
    public TextWriter Out => field ??= new TerminalTextWriter(this);

    /// <summary>
    /// Rows above this terminal that it never writes to or scrolls: its text goes in the rows between <see cref="MarginTop"/> and
    /// <see cref="MarginBottom"/>, which follow the canvas when it is resized. A margin that leaves no room keeps at least one row,
    /// so <c>MarginTop = int.MaxValue</c> keeps the terminal on the last row. 0 by default. Only a region that starts at row 0
    /// feeds the scrollback. Set it before writing.
    /// </summary>
    public int MarginTop { get; set; }

    /// <summary>Rows below this terminal that it never writes to or scrolls (see <see cref="MarginTop"/>). 0 by default.</summary>
    public int MarginBottom { get; set; }

    /// <summary>
    /// Columns at the left that this terminal never writes to, a gutter: every line starts after them, after a line break, a
    /// carriage return, <see cref="Clear"/> and where a long line wraps, and tab stops are counted from there. Scrolling still
    /// moves whole rows, so the scrollback keeps working, with the gutter blank in it. A margin that leaves no room keeps at
    /// least one column. 0 by default. A change applies to what is written after it; text already written stays where it is.
    /// </summary>
    public int MarginLeft { get; set; }

    /// <summary>
    /// The style of this terminal's own text, when several terminals (or other drawing) share a canvas. <see langword="null"/>
    /// (the default) writes with the canvas <see cref="CellCanvas.Pen"/>; when set, <see cref="Style"/> reads and changes this
    /// style instead of the canvas pen.
    /// </summary>
    public CellStyle? Pen { get; set; }

    /// <summary>
    /// Raised on the reading thread when a read of this terminal takes a <see cref="ResizeEvent"/> from the input: the program
    /// can redraw what it keeps outside the terminal's rows.
    /// </summary>
    public event Action? Resized;

    /// <summary>When true (the default), every write is presented immediately.</summary>
    public bool AutoPresent { get; set; } = true;

    /// <summary>The style of text written next. Same as <see cref="CellCanvas.Pen"/>.</summary>
    public CellStyle Style
    {
        get => Pen ?? Canvas.Pen;
        set
        {
            if (Pen is null)
            {
                Canvas.Pen = value;
            }
            else
            {
                Pen = value;
            }
        }
    }

    /// <summary>The column where the next character is written.</summary>
    public int CursorColumn
    {
        get
        {
            Constrain();
            return _column;
        }
    }

    /// <summary>The row where the next character is written.</summary>
    public int CursorRow
    {
        get
        {
            Constrain();
            return _row;
        }
    }

    /// <summary>The lines returned by <see cref="ReadLineAsync"/>, oldest first (not including masked input).</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>
    /// A <see cref="TextReader"/> for code that expects <c>Console.In</c>. Its <c>ReadLine</c> blocks until the user presses Enter,
    /// so use it from the program's own thread, never from the UI thread. It throws <see cref="OperationCanceledException"/> when
    /// the canvas is closed.
    /// </summary>
    public TextReader In => field ??= new TerminalTextReader(this);

    /// <summary>True when a key press or typed text is waiting, so <see cref="ReadKeyAsync"/> would return at once.</summary>
    public bool KeyAvailable
    {
        get
        {
            while (_pendingIndex >= _pending.Count && Canvas.Input.TryRead(out var input))
            {
                if (input is ResizeEvent)
                {
                    Constrain();
                    Resized?.Invoke();
                }
                else
                {
                    Queue(input);
                }
            }

            return _pendingIndex < _pending.Count;
        }
    }

    /// <summary>Moves the cursor, clamped to the canvas (and to the rows between the margins).</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row of the canvas.</param>
    public void SetCursorPosition(int column, int row)
    {
        Constrain();
        _column = Math.Clamp(column, RegionLeft, Canvas.Columns);
        _row = Math.Clamp(row, RegionTop, RegionBottom - 1);
        Finish();
    }

    /// <summary>Clears the canvas (with margins: the rows between them) and moves the cursor to its top-left corner.</summary>
    public void Clear()
    {
        Constrain();
        if (MarginTop <= 0 && MarginBottom <= 0)
        {
            Canvas.Clear(Pen);
        }
        else
        {
            Canvas.Fill(Region, ' ', Pen);
        }

        _column = RegionLeft;
        _row = RegionTop;
        Finish();
    }

    /// <summary>Writes text at the cursor. Handles <c>\n</c>, <c>\r</c>, <c>\t</c> and <c>\b</c>; wraps and scrolls at the edges.</summary>
    /// <param name="text">The text to write.</param>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Write(text.AsSpan());
    }

    /// <summary>Writes text at the cursor. Handles <c>\n</c>, <c>\r</c>, <c>\t</c> and <c>\b</c>; wraps and scrolls at the edges.</summary>
    /// <param name="text">The text to write.</param>
    public void Write(ReadOnlySpan<char> text)
    {
        WriteCore(text);
        Finish();
    }

    /// <summary>Writes text followed by a line break.</summary>
    /// <param name="text">The text to write.</param>
    public void WriteLine(string text = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        WriteCore(text);
        WriteCore("\n");
        Finish();
    }

    /// <summary>Writes text with a temporary style.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="style">The style to use for this text only.</param>
    public void Write(string text, CellStyle style)
    {
        var previous = Style;
        Style = style;
        try
        {
            Write(text);
        }
        finally
        {
            Style = previous;
        }
    }

    /// <summary>Writes a line with a temporary style.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="style">The style to use for this text only.</param>
    public void WriteLine(string text, CellStyle style)
    {
        var previous = Style;
        Style = style;
        try
        {
            WriteLine(text);
        }
        finally
        {
            Style = previous;
        }
    }

    /// <summary>
    /// Waits for typed text or an action key. Mouse input and plain key-down events for printable keys are skipped.
    /// A paste arrives as a console delivers it: the text of each line, with an <see cref="Key.Enter"/> press for each line break.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="OperationCanceledException">The wait was canceled or the canvas was closed.</exception>
    public async ValueTask<KeyPress> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (await ReadKeyOrResizeAsync(cancellationToken).ConfigureAwait(false) is { } key)
            {
                return key;
            }
        }
    }

    /// <summary>
    /// Reads a line with basic editing (caret movement, Backspace/Delete, Home/End, history with Up/Down, Escape to clear).
    /// When several lines are pasted, the first line break ends this line and the rest is kept for the next reads, one line each.
    /// </summary>
    /// <param name="prompt">Written before the editable text.</param>
    /// <param name="mask">If set, this character is echoed instead of what is typed (for passwords). The line is not added to history.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line the user entered, without the line break.</returns>
    /// <exception cref="OperationCanceledException">The read was canceled or the canvas was closed.</exception>
    public async ValueTask<string> ReadLineAsync(string? prompt = null, char? mask = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            WriteCore(prompt);
        }

        Constrain();
        var editor = new LineEditor(_column, _row, mask);
        var historyIndex = _history.Count;
        var pending = "";
        RenderLine(editor);

        while (true)
        {
            var key = await ReadKeyOrResizeAsync(cancellationToken).ConfigureAwait(false);
            if (key is null)
            {
                RenderLine(editor);
                continue;
            }

            var press = key.Value;
            if (press.IsText)
            {
                editor.Insert(press.Text);
            }
            else
            {
                switch (press.Key)
                {
                    case Key.Enter:
                        var result = editor.Text;
                        FinishLine(editor);
                        if (mask is null && result.Length > 0 && (_history.Count == 0 || _history[^1] != result))
                        {
                            _history.Add(result);
                        }

                        return result;
                    case Key.Back:
                        editor.Backspace();
                        break;
                    case Key.Delete:
                        editor.Delete();
                        break;
                    case Key.Left:
                        editor.Move(-1);
                        break;
                    case Key.Right:
                        editor.Move(1);
                        break;
                    case Key.Home:
                        editor.MoveTo(0);
                        break;
                    case Key.End:
                        editor.MoveTo(int.MaxValue);
                        break;
                    case Key.Escape:
                        editor.Set("");
                        break;
                    case Key.Up when mask is null && historyIndex > 0:
                        if (historyIndex == _history.Count)
                        {
                            pending = editor.Text;
                        }

                        editor.Set(_history[--historyIndex]);
                        break;
                    case Key.Down when mask is null && historyIndex < _history.Count:
                        historyIndex++;
                        editor.Set(historyIndex == _history.Count ? pending : _history[historyIndex]);
                        break;
                }
            }

            RenderLine(editor);
        }
    }

    internal void Finish()
    {
        Constrain();
        Canvas.SetCursor(_column, _row);
        if (AutoPresent)
        {
            Canvas.Present();
        }
    }

    private async ValueTask<KeyPress?> ReadKeyOrResizeAsync(CancellationToken cancellationToken)
    {
        while (_pendingIndex >= _pending.Count)
        {
            var input = await Canvas.Input.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (input is ResizeEvent)
            {
                Constrain();
                Resized?.Invoke();
                return null;
            }

            // Mouse input and key presses that also arrive as text add nothing, so the loop reads on.
            Queue(input);
        }

        return _pending[_pendingIndex++];
    }

    private void Queue(InputEvent input)
    {
        if (_pendingIndex >= _pending.Count)
        {
            _pending.Clear();
            _pendingIndex = 0;
        }

        KeyPresses.Add(input, _pending);
    }

    private void WriteCore(ReadOnlySpan<char> text)
    {
        Constrain();
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '\n':
                    NewLine();
                    break;
                case '\r':
                    _column = RegionLeft;
                    break;
                case '\b':
                    _column = Math.Max(RegionLeft, _column - 1);
                    break;
                case '\t':
                    // (the tab stops are counted from the gutter)
                    var left = RegionLeft;
                    var stop = Math.Min(left + ((((_column - left) / TabSize) + 1) * TabSize), Canvas.Columns);
                    while (_column < stop)
                    {
                        PutRune(new Rune(' '));
                    }

                    break;
                default:
                    PutRune(rune);
                    break;
            }
        }
    }

    private void PutRune(Rune rune)
    {
        var width = CharWidth.Of(rune);
        if (width == 0)
        {
            return;
        }

        if (_column + width > Canvas.Columns)
        {
            NewLine();
        }

        Canvas.SetCell(_column, _row, rune, Pen);
        _column += width;
    }

    private void NewLine()
    {
        _column = RegionLeft;
        if (_row + 1 >= RegionBottom)
        {
            Canvas.Scroll(Region, 0, -1, Pen);
            _scrolls++;
        }
        else
        {
            _row++;
        }
    }

    private static CellCanvas KeepingCursorRow(CellCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.KeepCursorRowOnShrink = true;
        return canvas;
    }

    // The first row this terminal writes in, and the row after its last (the margins, with at least one row left).
    private int RegionTop => Math.Clamp(MarginTop, 0, Canvas.Rows - 1);

    private int RegionBottom => Math.Max(RegionTop + 1, Canvas.Rows - Math.Max(0, MarginBottom));

    private CellRect Region => new(0, RegionTop, Canvas.Columns, RegionBottom - RegionTop);

    // The first column this terminal writes in (the gutter, with at least one column left).
    private int RegionLeft => Math.Clamp(MarginLeft, 0, Math.Max(0, Canvas.Columns - 1));

    // Keeps the cursor valid if the canvas was resized behind our back: it moves up with content a shorter canvas moved up, and
    // stays in the region. A column equal to Columns is a pending wrap.
    private void Constrain()
    {
        var moved = Canvas.RowsMovedUpByResize;
        _row -= (int)Math.Min(moved - (_movedByResize ?? moved), int.MaxValue);
        _movedByResize = moved;
        _column = Math.Clamp(_column, RegionLeft, Canvas.Columns);
        _row = Math.Clamp(_row, RegionTop, RegionBottom - 1);
    }

    private void RenderLine(LineEditor editor)
    {
        Constrain();
        var columns = Canvas.Columns;
        var rows = RegionBottom;
        editor.StartColumn = Math.Min(editor.StartColumn, columns - 1);
        editor.StartRow = Math.Clamp(editor.StartRow, RegionTop, rows - 1);

        _column = editor.StartColumn;
        _row = editor.StartRow;
        _scrolls = 0;

        var text = editor.Display(0, editor.Length);
        WriteCore(text);
        var width = CharWidth.Of(text);
        if (editor.RenderedWidth > width)
        {
            WriteCore(new string(' ', editor.RenderedWidth - width));
        }

        editor.RenderedWidth = width;
        editor.StartRow -= _scrolls;

        // Place the caret by walking the text before it; a pending wrap resolves to the start of the next row.
        var (column, row) = editor.Walk(editor.Display(0, editor.Caret), columns);
        if (column >= columns)
        {
            column = 0;
            row++;
        }

        if (row >= rows)
        {
            Canvas.Scroll(Region, 0, -1, Pen);
            editor.StartRow--;
            row--;
        }

        _column = column;
        _row = row;
        Finish();
    }

    private void FinishLine(LineEditor editor)
    {
        RenderLine(editor);
        var (column, row) = editor.Walk(editor.Display(0, editor.Length), Canvas.Columns);
        _column = column;
        _row = row;
        NewLine();
        Finish();
    }
}
