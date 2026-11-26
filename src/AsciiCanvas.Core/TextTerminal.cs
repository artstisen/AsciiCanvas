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
    private int _column;
    private int _row;
    private int _scrolls;

    /// <summary>The canvas this terminal writes to.</summary>
    public CellCanvas Canvas { get; } = canvas;

    /// <summary>A <see cref="TextWriter"/> that writes to this terminal, e.g. for code that expects <c>Console.Out</c>.</summary>
    public TextWriter Out => field ??= new TerminalTextWriter(this);

    /// <summary>When true (the default), every write is presented immediately.</summary>
    public bool AutoPresent { get; set; } = true;

    /// <summary>The style of text written next. Same as <see cref="CellCanvas.Pen"/>.</summary>
    public CellStyle Style
    {
        get => Canvas.Pen;
        set => Canvas.Pen = value;
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

    /// <summary>Moves the cursor, clamped to the canvas.</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    public void SetCursorPosition(int column, int row)
    {
        _column = Math.Clamp(column, 0, Canvas.Columns);
        _row = Math.Clamp(row, 0, Canvas.Rows - 1);
        Finish();
    }

    /// <summary>Clears the canvas and moves the cursor to the top-left corner.</summary>
    public void Clear()
    {
        Canvas.Clear();
        _column = 0;
        _row = 0;
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
        var previous = Canvas.Pen;
        Canvas.Pen = style;
        try
        {
            Write(text);
        }
        finally
        {
            Canvas.Pen = previous;
        }
    }

    /// <summary>Writes a line with a temporary style.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="style">The style to use for this text only.</param>
    public void WriteLine(string text, CellStyle style)
    {
        var previous = Canvas.Pen;
        Canvas.Pen = style;
        try
        {
            WriteLine(text);
        }
        finally
        {
            Canvas.Pen = previous;
        }
    }

    /// <summary>Waits for typed text or an action key. Mouse input and plain key-down events for printable keys are skipped.</summary>
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

    private static bool IsActionKey(KeyEvent e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown
                or Key.Insert or Key.Delete or Key.Enter or Key.Escape or Key.Back or Key.Tab:
                return true;
            case >= Key.F1 and <= Key.F12:
                return true;
            case (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9):
                // Ctrl+Alt is how some layouts type AltGr characters, which arrive as text instead.
                var command = (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
                var alt = (e.Modifiers & KeyModifiers.Alt) != 0;
                return command != alt;
            default:
                return false;
        }
    }

    private async ValueTask<KeyPress?> ReadKeyOrResizeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            switch (await Canvas.Input.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                case TextEvent text:
                    return new KeyPress(Key.Unknown, text.Text, KeyModifiers.None);
                case PasteEvent { Text.Length: > 0 } paste:
                    // A line editor has one line, so line breaks in pasted text become spaces.
                    return new KeyPress(Key.Unknown, paste.Text.ReplaceLineEndings(" "), KeyModifiers.None);
                case KeyEvent key when IsActionKey(key):
                    return new KeyPress(key.Key, "", key.Modifiers);
                case ResizeEvent:
                    Constrain();
                    return null;
            }

            // Mouse input and key presses that also arrive as text are skipped.
        }
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
                    _column = 0;
                    break;
                case '\b':
                    _column = Math.Max(0, _column - 1);
                    break;
                case '\t':
                    var stop = Math.Min((((_column / TabSize) + 1) * TabSize), Canvas.Columns);
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

        Canvas.SetCell(_column, _row, rune);
        _column += width;
    }

    private void NewLine()
    {
        _column = 0;
        if (_row + 1 >= Canvas.Rows)
        {
            Canvas.Scroll(Canvas.Bounds, 0, -1);
            _scrolls++;
        }
        else
        {
            _row++;
        }
    }

    // Keeps the cursor valid if the canvas was resized behind our back. A column equal to Columns is a pending wrap.
    private void Constrain()
    {
        _column = Math.Clamp(_column, 0, Canvas.Columns);
        _row = Math.Clamp(_row, 0, Canvas.Rows - 1);
    }

    private void RenderLine(LineEditor editor)
    {
        var columns = Canvas.Columns;
        var rows = Canvas.Rows;
        editor.StartColumn = Math.Min(editor.StartColumn, columns - 1);
        editor.StartRow = Math.Min(editor.StartRow, rows - 1);

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
            Canvas.Scroll(Canvas.Bounds, 0, -1);
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
