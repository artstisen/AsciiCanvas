using System.Text;

namespace AsciiCanvas;

[Flags]
public enum CellAttributes : byte
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Strikethrough = 16,
    Reverse = 32,
}

/// <summary>Foreground, background and attributes of a cell.</summary>
public readonly record struct CellStyle(CellColor Foreground, CellColor Background, CellAttributes Attributes = CellAttributes.None)
{
    public static CellStyle Default => default;

    public CellStyle(CellColor foreground) : this(foreground, CellColor.Default)
    {
    }
}

/// <summary>How a cell takes part in a double-width character.</summary>
public enum CellWidth : byte
{
    /// <summary>A normal single-column cell.</summary>
    Single = 0,

    /// <summary>The left half of a double-width character; holds the rune.</summary>
    WideLead = 1,

    /// <summary>The right half of a double-width character; has no glyph of its own.</summary>
    WideTrail = 2,
}

/// <summary>One character position on the grid.</summary>
public readonly record struct Cell(Rune Rune, CellStyle Style, CellWidth Width = CellWidth.Single)
{
    public static Cell Blank(CellStyle style = default) => new(new Rune(' '), style);

    /// <summary>True if the cell shows nothing (a space, an unset cell or the trailing half of a wide character).</summary>
    public bool IsBlank => Width == CellWidth.WideTrail || Rune.Value is 0 or ' ';
}
