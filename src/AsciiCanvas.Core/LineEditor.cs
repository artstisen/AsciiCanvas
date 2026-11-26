using System.Text;

namespace AsciiCanvas;

/// <summary>The text being edited by <see cref="TextTerminal.ReadLineAsync"/>: runes, a caret and where the line starts on screen.</summary>
internal sealed class LineEditor(int startColumn, int startRow, char? mask)
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

    /// <summary>The text as shown on screen: the mask character repeated, or the text itself.</summary>
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
