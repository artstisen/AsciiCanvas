using System.Text;

namespace AsciiCanvas;

/// <summary>A key press as seen by <see cref="TextTerminal.ReadKeyAsync"/>.</summary>
/// <param name="Key">The key for action keys (arrows, Enter, F-keys, Ctrl+letter...); <see cref="AsciiCanvas.Key.Unknown"/> for typed text.</param>
/// <param name="Text">The typed text, or empty for action keys.</param>
public readonly record struct KeyPress(Key Key, string Text, KeyModifiers Modifiers)
{
    public bool IsText => Text.Length > 0;
}

/// <summary>
/// A console-like layer over a <see cref="CellCanvas"/>: a text cursor with wrapping and scrolling output,
/// plus key and line input. It uses the canvas <see cref="CellCanvas.Pen"/> as the text style.
/// Only one task should read input at a time.
/// </summary>
public sealed class TextTerminal
{
    private const int TabSize = 8;

    private readonly CellCanvas _canvas;
    private readonly List<string> _history = [];
    private int _column;
    private int _row;
    private int _scrolls;

    public TextTerminal(CellCanvas canvas)
    {
        _canvas = canvas;
        Out = new TerminalTextWriter(this);
    }

    public CellCanvas Canvas => _canvas;

    /// <summary>A <see cref="TextWriter"/> that writes to this terminal, e.g. for code that expects <c>Console.Out</c>.</summary>
    public TextWriter Out { get; }

    /// <summary>When true (the default), every write is presented immediately.</summary>
    public bool AutoPresent { get; set; } = true;

    /// <summary>The style of text written next. Same as <see cref="CellCanvas.Pen"/>.</summary>
    public CellStyle Style
    {
        get => _canvas.Pen;
        set => _canvas.Pen = value;
    }

    public int CursorColumn
    {
        get { Constrain(); return _column; }
    }

    public int CursorRow
    {
        get { Constrain(); return _row; }
    }

    public IReadOnlyList<string> History => _history;

    public void SetCursorPosition(int column, int row)
    {
        _column = Math.Clamp(column, 0, _canvas.Columns);
        _row = Math.Clamp(row, 0, _canvas.Rows - 1);
        Finish();
    }

    /// <summary>Clears the canvas and moves the cursor to the top-left corner.</summary>
    public void Clear()
    {
        _canvas.Clear();
        _column = 0;
        _row = 0;
        Finish();
    }

    // ---- Output --------------------------------------------------------------------------------------------

    public void Write(string text) => Write(text.AsSpan());

    public void Write(ReadOnlySpan<char> text)
    {
        WriteCore(text);
        Finish();
    }

    public void WriteLine(string text = "")
    {
        WriteCore(text);
        WriteCore("\n");
        Finish();
    }

    /// <summary>Writes with a temporary style.</summary>
    public void Write(string text, CellStyle style)
    {
        var previous = _canvas.Pen;
        _canvas.Pen = style;
        try
        {
            Write(text);
        }
        finally
        {
            _canvas.Pen = previous;
        }
    }

    public void WriteLine(string text, CellStyle style)
    {
        var previous = _canvas.Pen;
        _canvas.Pen = style;
        try
        {
            WriteLine(text);
        }
        finally
        {
            _canvas.Pen = previous;
        }
    }

    // ---- Input ---------------------------------------------------------------------------------------------

    /// <summary>Waits for typed text or an action key. Mouse input and plain key-down events for printable keys are skipped.</summary>
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
    public async ValueTask<string> ReadLineAsync(string? prompt = null, char? mask = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            WriteCore(prompt);
        }

        Constrain();
        var editor = new LineEditor(_column, _row, mask);
        var historyIndex = _history.Count;
        string pending = "";
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
                    case Key.Back: editor.Backspace(); break;
                    case Key.Delete: editor.Delete(); break;
                    case Key.Left: editor.Move(-1); break;
                    case Key.Right: editor.Move(1); break;
                    case Key.Home: editor.MoveTo(0); break;
                    case Key.End: editor.MoveTo(int.MaxValue); break;
                    case Key.Escape: editor.Set(""); break;
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

    private async ValueTask<KeyPress?> ReadKeyOrResizeAsync(CancellationToken cancellationToken)
    {
        switch (await _canvas.Input.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            case TextEvent text:
                return new KeyPress(Key.Unknown, text.Text, KeyModifiers.None);
            case KeyEvent key when IsActionKey(key):
                return new KeyPress(key.Key, "", key.Modifiers);
            case ResizeEvent:
                Constrain();
                return null;
            default:
                // Mouse input and key presses that also arrive as text are skipped; the caller keeps waiting.
                return await ReadKeyOrResizeAsync(cancellationToken).ConfigureAwait(false);
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
                var ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
                var alt = (e.Modifiers & KeyModifiers.Alt) != 0;
                return ctrl != alt;
            default:
                return false;
        }
    }

    // ---- Text output internals -----------------------------------------------------------------------------

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
                    var stop = Math.Min((_column / TabSize + 1) * TabSize, _canvas.Columns);
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

        if (_column + width > _canvas.Columns)
        {
            NewLine();
        }

        _canvas.SetCell(_column, _row, rune);
        _column += width;
    }

    private void NewLine()
    {
        _column = 0;
        if (_row + 1 >= _canvas.Rows)
        {
            _canvas.Scroll(_canvas.Bounds, 0, -1);
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
        _column = Math.Clamp(_column, 0, _canvas.Columns);
        _row = Math.Clamp(_row, 0, _canvas.Rows - 1);
    }

    internal void Finish()
    {
        Constrain();
        _canvas.SetCursor(_column, _row);
        if (AutoPresent)
        {
            _canvas.Present();
        }
    }

    // ---- Line editing --------------------------------------------------------------------------------------

    private void RenderLine(LineEditor editor)
    {
        var columns = _canvas.Columns;
        var rows = _canvas.Rows;
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
            _canvas.Scroll(_canvas.Bounds, 0, -1);
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
        var (column, row) = editor.Walk(editor.Display(0, editor.Length), _canvas.Columns);
        _column = column;
        _row = row;
        NewLine();
        Finish();
    }

    private sealed class LineEditor(int startColumn, int startRow, char? mask)
    {
        private readonly List<Rune> _runes = [];

        public int StartColumn { get; set; } = startColumn;

        public int StartRow { get; set; } = startRow;

        public int RenderedWidth { get; set; }

        public int Caret { get; private set; }

        public int Length => _runes.Count;

        public string Text => string.Concat(_runes.Select(r => r.ToString()));

        public void Insert(string text)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                if (CharWidth.Of(rune) > 0)
                {
                    _runes.Insert(Caret++, rune);
                }
            }
        }

        public void Backspace()
        {
            if (Caret > 0)
            {
                _runes.RemoveAt(--Caret);
            }
        }

        public void Delete()
        {
            if (Caret < _runes.Count)
            {
                _runes.RemoveAt(Caret);
            }
        }

        public void Move(int delta) => MoveTo(Caret + delta);

        public void MoveTo(int index) => Caret = (int)Math.Clamp((long)index, 0, _runes.Count);

        public void Set(string text)
        {
            _runes.Clear();
            Caret = 0;
            Insert(text);
        }

        public string Display(int start, int end)
        {
            var builder = new StringBuilder();
            for (var i = start; i < end; i++)
            {
                builder.Append(mask is { } m ? m.ToString() : _runes[i].ToString());
            }

            return builder.ToString();
        }

        /// <summary>Where the cursor ends up after writing <paramref name="text"/> from the start position (without scrolling).</summary>
        public (int Column, int Row) Walk(string text, int columns)
        {
            var column = StartColumn;
            var row = StartRow;
            foreach (var rune in text.EnumerateRunes())
            {
                var width = CharWidth.Of(rune);
                if (column + width > columns)
                {
                    column = 0;
                    row++;
                }

                column += width;
            }

            return (column, row);
        }
    }
}
