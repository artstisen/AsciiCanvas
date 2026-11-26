using System.Text;

namespace AsciiCanvas;

/// <summary>One character position on the grid.</summary>
/// <param name="Rune">The character shown in the cell.</param>
/// <param name="Style">Colors and attributes of the cell.</param>
/// <param name="Width">Whether the cell is part of a double-width character.</param>
public readonly record struct Cell(Rune Rune, CellStyle Style, CellWidth Width = CellWidth.Narrow)
{
    /// <summary>True if the cell shows nothing (a space, an unset cell or the trailing half of a wide character).</summary>
    public bool IsBlank => Width == CellWidth.WideTrail || Rune.Value is 0 or ' ';

    /// <summary>Creates an empty cell with the given style.</summary>
    /// <param name="style">The style of the blank cell.</param>
    public static Cell Blank(CellStyle style = default) => new(new Rune(' '), style);
}
