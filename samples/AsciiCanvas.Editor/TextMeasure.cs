using System.Text;

namespace AsciiCanvas.Editor;

/// <summary>Converts between indexes in a line of text and columns on the character grid.</summary>
public static class TextMeasure
{
    /// <summary>Gets the index after the character at <paramref name="index"/>, treating a surrogate pair as one character.</summary>
    /// <param name="line">The line.</param>
    /// <param name="index">The index to move from.</param>
    public static int NextIndex(string line, int index)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (index >= line.Length)
        {
            return line.Length;
        }

        var pair = char.IsHighSurrogate(line[index]) && index + 1 < line.Length && char.IsLowSurrogate(line[index + 1]);
        return index + (pair ? 2 : 1);
    }

    /// <summary>Gets the index before the character that ends at <paramref name="index"/>, treating a surrogate pair as one character.</summary>
    /// <param name="line">The line.</param>
    /// <param name="index">The index to move from.</param>
    public static int PreviousIndex(string line, int index)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (index <= 0)
        {
            return 0;
        }

        var pair = char.IsLowSurrogate(line[index - 1]) && index >= 2 && char.IsHighSurrogate(line[index - 2]);
        return index - (pair ? 2 : 1);
    }

    /// <summary>Gets the character at an index and how much room it takes on the grid.</summary>
    /// <param name="line">The line.</param>
    /// <param name="index">The index of the character; must be inside the line.</param>
    /// <param name="column">The grid column where the character starts; tabs reach the next multiple of <paramref name="tabSize"/>.</param>
    /// <param name="tabSize">The distance between tab stops.</param>
    /// <returns>The character, how many UTF-16 units it uses, and its width in grid columns (0 for control and combining characters).</returns>
    public static (Rune Rune, int Length, int Width) GlyphAt(string line, int index, int column, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        _ = Rune.DecodeFromUtf16(line.AsSpan(index), out var rune, out var consumed);
        var width = rune.Value == '\t' ? tabSize - (column % tabSize) : CharWidth.Of(rune);
        return (rune, consumed, width);
    }

    /// <summary>Gets the grid column of an index, where tabs are expanded and wide characters take two columns.</summary>
    /// <param name="line">The line.</param>
    /// <param name="index">The index in the line.</param>
    /// <param name="tabSize">The distance between tab stops.</param>
    public static int DisplayColumn(string line, int index, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        var column = 0;
        var i = 0;
        while (i < index && i < line.Length)
        {
            var (_, length, width) = GlyphAt(line, i, column, tabSize);
            column += width;
            i += length;
        }

        return column;
    }

    /// <summary>Gets the index of the character that covers a grid column; the line length if the column is past the end.</summary>
    /// <param name="line">The line.</param>
    /// <param name="column">The grid column.</param>
    /// <param name="tabSize">The distance between tab stops.</param>
    public static int IndexAtDisplayColumn(string line, int column, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        var current = 0;
        var i = 0;
        while (i < line.Length)
        {
            var (_, length, width) = GlyphAt(line, i, current, tabSize);
            if (current + width > column)
            {
                return i;
            }

            current += width;
            i += length;
        }

        return line.Length;
    }
}
