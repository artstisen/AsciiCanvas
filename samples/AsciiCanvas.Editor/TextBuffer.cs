namespace AsciiCanvas.Editor;

/// <summary>The text of a document as a list of lines. Line breaks are always <c>\n</c> inside the buffer.</summary>
public sealed class TextBuffer
{
    private readonly List<string> _lines = [""];

    /// <summary>The number of lines; at least 1.</summary>
    public int LineCount => _lines.Count;

    /// <summary>The whole text, with <c>\n</c> between lines.</summary>
    public string Text => string.Join('\n', _lines);

    /// <summary>The position after the last character.</summary>
    public TextPosition End => new(_lines.Count - 1, _lines[^1].Length);

    /// <summary>Replaces line breaks of any kind with <c>\n</c>.</summary>
    /// <param name="text">The text to normalize.</param>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ReplaceLineEndings("\n");
    }

    /// <summary>Gets the position reached by writing <paramref name="text"/> at <paramref name="start"/>.</summary>
    /// <param name="start">Where the text starts.</param>
    /// <param name="text">The text, with <c>\n</c> line breaks.</param>
    public static TextPosition Advance(TextPosition start, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lastBreak = text.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            return new TextPosition(start.Line, start.Column + text.Length);
        }

        return new TextPosition(start.Line + text.Count(c => c == '\n'), text.Length - lastBreak - 1);
    }

    /// <summary>Gets one line, without its line break.</summary>
    /// <param name="line">The zero-based line.</param>
    public string GetLine(int line) => _lines[line];

    /// <summary>Replaces the whole text.</summary>
    /// <param name="text">The new text; any kind of line break is accepted.</param>
    public void SetText(string text)
    {
        _lines.Clear();
        _lines.AddRange(Normalize(text).Split('\n'));
    }

    /// <summary>Moves a position to the nearest valid one.</summary>
    /// <param name="position">The position to check.</param>
    public TextPosition Clamp(TextPosition position)
    {
        var line = Math.Clamp(position.Line, 0, _lines.Count - 1);
        return new TextPosition(line, Math.Clamp(position.Column, 0, _lines[line].Length));
    }

    /// <summary>Gets the text between two positions.</summary>
    /// <param name="from">The start; must not be after <paramref name="to"/>.</param>
    /// <param name="to">The end.</param>
    public string GetText(TextPosition from, TextPosition to)
    {
        if (from.Line == to.Line)
        {
            return _lines[from.Line][from.Column..to.Column];
        }

        var parts = new List<string> { _lines[from.Line][from.Column..] };
        for (var line = from.Line + 1; line < to.Line; line++)
        {
            parts.Add(_lines[line]);
        }

        parts.Add(_lines[to.Line][..to.Column]);
        return string.Join('\n', parts);
    }

    /// <summary>Inserts text, which may contain line breaks.</summary>
    /// <param name="at">Where to insert.</param>
    /// <param name="text">The text to insert.</param>
    /// <returns>The position after the inserted text.</returns>
    public TextPosition Insert(TextPosition at, string text)
    {
        var parts = Normalize(text).Split('\n');
        var line = _lines[at.Line];
        var tail = line[at.Column..];

        if (parts.Length == 1)
        {
            _lines[at.Line] = line[..at.Column] + parts[0] + tail;
            return new TextPosition(at.Line, at.Column + parts[0].Length);
        }

        _lines[at.Line] = line[..at.Column] + parts[0];
        _lines.InsertRange(at.Line + 1, parts[1..^1]);
        _lines.Insert(at.Line + parts.Length - 1, parts[^1] + tail);
        return new TextPosition(at.Line + parts.Length - 1, parts[^1].Length);
    }

    /// <summary>Deletes the text between two positions.</summary>
    /// <param name="from">The start; must not be after <paramref name="to"/>.</param>
    /// <param name="to">The end.</param>
    /// <returns>The deleted text.</returns>
    public string Delete(TextPosition from, TextPosition to)
    {
        var removed = GetText(from, to);
        _lines[from.Line] = _lines[from.Line][..from.Column] + _lines[to.Line][to.Column..];
        _lines.RemoveRange(from.Line + 1, to.Line - from.Line);
        return removed;
    }
}
