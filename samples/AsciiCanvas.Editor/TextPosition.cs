namespace AsciiCanvas.Editor;

/// <summary>A position in a text: a line, and a UTF-16 index inside that line.</summary>
/// <param name="Line">The zero-based line.</param>
/// <param name="Column">The zero-based UTF-16 index in the line, between 0 and the line length.</param>
public readonly record struct TextPosition(int Line, int Column) : IComparable<TextPosition>
{
    /// <summary>Compares two positions in reading order.</summary>
    public static bool operator <(TextPosition left, TextPosition right) => left.CompareTo(right) < 0;

    /// <summary>Compares two positions in reading order.</summary>
    public static bool operator >(TextPosition left, TextPosition right) => left.CompareTo(right) > 0;

    /// <summary>Compares two positions in reading order.</summary>
    public static bool operator <=(TextPosition left, TextPosition right) => left.CompareTo(right) <= 0;

    /// <summary>Compares two positions in reading order.</summary>
    public static bool operator >=(TextPosition left, TextPosition right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public int CompareTo(TextPosition other) => Line != other.Line ? Line.CompareTo(other.Line) : Column.CompareTo(other.Column);
}
