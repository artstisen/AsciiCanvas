using System.Globalization;
using System.Text;

namespace AsciiCanvas.Editor;

/// <summary>Draws an <see cref="EditorModel"/> on a canvas: a title bar, line numbers, the text with selection, a scroll bar and a status line.</summary>
internal sealed class EditorView
{
    private const string HelpText = " ^S Save  ^Q Quit  ^Z/^Y Undo/Redo  ^C/^X/^V Copy/Cut/Paste  ^A Select all  F3 Diagnostics";

    private static readonly CellStyle _currentLine = new(CellColor.Default, CellColor.Gray(3));
    private static readonly CellStyle _selection = new(CellColor.BrightWhite, CellColor.Blue);
    private static readonly CellStyle _gutter = new(CellColor.BrightBlack);
    private static readonly CellStyle _gutterCurrent = new(CellColor.BrightYellow);
    private static readonly CellStyle _bar = new(CellColor.Default, CellColor.Default, CellAttributes.Reverse);
    private static readonly CellStyle _help = new(CellColor.BrightBlack);
    private static readonly CellStyle _scrollTrack = new(CellColor.BrightBlack);
    private static readonly CellStyle _scrollThumb = new(CellColor.White);

    private CellRect _text;
    private int _gutterWidth;

    /// <summary>The first line shown.</summary>
    public int TopLine { get; private set; }

    /// <summary>The first grid column of the text that is shown, when long lines are scrolled sideways.</summary>
    public int LeftColumn { get; private set; }

    /// <summary>The number of text lines that fit on the screen.</summary>
    public int PageSize => _text.Height;

    /// <summary>Scrolls the view without moving the caret.</summary>
    /// <param name="model">The document.</param>
    /// <param name="lines">Lines to scroll; positive moves down.</param>
    public void Scroll(EditorModel model, int lines) => TopLine = Math.Clamp(TopLine + lines, 0, model.Buffer.LineCount - 1);

    /// <summary>Draws everything and presents the frame.</summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="model">The document.</param>
    /// <param name="chrome">Title, message and prompt.</param>
    /// <param name="followCaret">True to scroll so the caret is visible.</param>
    public void Draw(CellCanvas canvas, EditorModel model, EditorChrome chrome, bool followCaret)
    {
        Layout(canvas, model);
        if (followCaret)
        {
            EnsureCaretVisible(model);
        }

        TopLine = Math.Clamp(TopLine, 0, model.Buffer.LineCount - 1);
        LeftColumn = Math.Max(0, LeftColumn);

        canvas.Pen = default;
        canvas.Clear();

        DrawTitle(canvas, chrome);
        var caretLine = model.Caret.Line;
        for (var row = 0; row < _text.Height; row++)
        {
            var line = TopLine + row;
            if (line < model.Buffer.LineCount)
            {
                DrawGutter(canvas, line, _text.Y + row, line == caretLine);
                DrawLine(canvas, model, line, _text.Y + row);
            }
        }

        DrawScrollBar(canvas, model);
        canvas.Write(0, canvas.Rows - 2, HelpText, _help);
        DrawStatus(canvas, model, chrome);
        PlaceCursor(canvas, model, chrome);
        canvas.Present();
    }

    /// <summary>Finds the text position under a grid cell.</summary>
    /// <param name="model">The document.</param>
    /// <param name="column">The grid column of the cell.</param>
    /// <param name="row">The grid row of the cell.</param>
    public TextPosition HitTest(EditorModel model, int column, int row)
    {
        var line = Math.Clamp(TopLine + row - _text.Y, 0, model.Buffer.LineCount - 1);
        var displayColumn = LeftColumn + Math.Max(0, column - _text.X);
        return new TextPosition(line, TextMeasure.IndexAtDisplayColumn(model.Buffer.GetLine(line), displayColumn, model.TabSize));
    }

    private void Layout(CellCanvas canvas, EditorModel model)
    {
        var digits = Math.Max(2, model.Buffer.LineCount.ToString(CultureInfo.InvariantCulture).Length);
        _gutterWidth = digits + 2;
        _text = new CellRect(_gutterWidth, 1, Math.Max(1, canvas.Columns - _gutterWidth - 1), Math.Max(1, canvas.Rows - 3));
    }

    private void EnsureCaretVisible(EditorModel model)
    {
        var caret = model.Caret;
        if (caret.Line < TopLine)
        {
            TopLine = caret.Line;
        }
        else if (caret.Line >= TopLine + _text.Height)
        {
            TopLine = caret.Line - _text.Height + 1;
        }

        var column = model.DisplayColumn(caret);
        if (column < LeftColumn)
        {
            LeftColumn = column;
        }
        else if (column >= LeftColumn + _text.Width)
        {
            LeftColumn = column - _text.Width + 1;
        }
    }

    private static void DrawTitle(CellCanvas canvas, EditorChrome chrome)
    {
        canvas.Fill(new CellRect(0, 0, canvas.Columns, 1), ' ', _bar);
        canvas.Write(1, 0, "AsciiCanvas Editor - " + chrome.Title, _bar);
    }

    private void DrawGutter(CellCanvas canvas, int line, int y, bool isCaretLine)
    {
        var digits = _gutterWidth - 2;
        var number = (line + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits);
        canvas.Write(0, y, number, isCaretLine ? _gutterCurrent : _gutter);
        canvas.SetCell(digits, y, '│', _gutter);
    }

    private void DrawLine(CellCanvas canvas, EditorModel model, int lineIndex, int y)
    {
        var text = model.Buffer.GetLine(lineIndex);
        var selected = model.HasSelection;
        var start = model.SelectionStart;
        var end = model.SelectionEnd;
        var baseStyle = lineIndex == model.Caret.Line && !selected ? _currentLine : CellStyle.Default;

        canvas.Fill(new CellRect(_text.X, y, _text.Width, 1), ' ', baseStyle);

        var run = new StringBuilder();
        var runStyle = baseStyle;
        var runX = _text.X;
        var column = 0;
        var i = 0;

        void Flush()
        {
            if (run.Length > 0)
            {
                canvas.Write(runX, y, run.ToString(), runStyle);
                run.Clear();
            }
        }

        while (i < text.Length && column < LeftColumn + _text.Width)
        {
            var (rune, length, width) = TextMeasure.GlyphAt(text, i, column, model.TabSize);
            var visible = width > 0 && column >= LeftColumn && column + width <= LeftColumn + _text.Width;
            if (visible)
            {
                var position = new TextPosition(lineIndex, i);
                var style = selected && position >= start && position < end ? _selection : baseStyle;
                if (style != runStyle || run.Length == 0)
                {
                    Flush();
                    runStyle = style;
                    runX = _text.X + column - LeftColumn;
                }

                run.Append(rune.Value == '\t' ? new string(' ', width) : rune.ToString());
            }

            column += width;
            i += length;
        }

        Flush();

        // A selection that continues onto the next line shows its line break as one highlighted cell.
        if (selected && lineIndex >= start.Line && lineIndex < end.Line && column >= LeftColumn && column < LeftColumn + _text.Width)
        {
            canvas.SetCell(_text.X + column - LeftColumn, y, ' ', _selection);
        }
    }

    private void DrawScrollBar(CellCanvas canvas, EditorModel model)
    {
        var x = canvas.Columns - 1;
        var lines = model.Buffer.LineCount;
        canvas.VLine(x, _text.Y, _text.Height, BoxStyle.SingleLine, _scrollTrack);

        var thumb = Math.Clamp(_text.Height * _text.Height / Math.Max(lines, 1), 1, _text.Height);
        var travel = _text.Height - thumb;
        var top = lines <= _text.Height ? 0 : travel * TopLine / Math.Max(1, lines - _text.Height);
        canvas.Fill(new CellRect(x, _text.Y + Math.Clamp(top, 0, travel), 1, thumb), '█', _scrollThumb);
    }

    private static void DrawStatus(CellCanvas canvas, EditorModel model, EditorChrome chrome)
    {
        var y = canvas.Rows - 1;
        canvas.Fill(new CellRect(0, y, canvas.Columns, 1), ' ', _bar);

        if (chrome.Prompt is not null)
        {
            canvas.Write(1, y, chrome.Prompt + chrome.PromptInput, _bar);
            return;
        }

        var caret = model.Caret;
        var left = chrome.Message
            ?? string.Create(CultureInfo.InvariantCulture, $"Ln {caret.Line + 1}, Col {model.DisplayColumn(caret) + 1}")
            + (model.HasSelection ? string.Create(CultureInfo.InvariantCulture, $"  ({model.SelectedText.Length} selected)") : "");
        canvas.Write(1, y, left, _bar);

        var right = string.Create(CultureInfo.InvariantCulture, $"Tab {model.TabSize}   UTF-8   {chrome.LineEnding} ");
        canvas.Write(Math.Max(canvas.Columns - CharWidth.Of(right), CharWidth.Of(left) + 3), y, right, _bar);
    }

    private void PlaceCursor(CellCanvas canvas, EditorModel model, EditorChrome chrome)
    {
        if (chrome.Prompt is not null)
        {
            canvas.CursorVisible = true;
            canvas.SetCursor(1 + CharWidth.Of(chrome.Prompt) + CharWidth.Of(chrome.PromptInput), canvas.Rows - 1);
            return;
        }

        var x = _text.X + model.DisplayColumn(model.Caret) - LeftColumn;
        var y = _text.Y + model.Caret.Line - TopLine;
        var inside = x >= _text.X && x < _text.Right && y >= _text.Y && y < _text.Bottom;
        canvas.CursorVisible = inside;
        if (inside)
        {
            canvas.SetCursor(x, y);
        }
    }
}
