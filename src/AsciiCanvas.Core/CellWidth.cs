namespace AsciiCanvas;

/// <summary>How a cell takes part in a double-width character.</summary>
public enum CellWidth : byte
{
    /// <summary>A normal single-column cell.</summary>
    Narrow = 0,

    /// <summary>The left half of a double-width character; holds the rune.</summary>
    WideLead = 1,

    /// <summary>The right half of a double-width character; has no glyph of its own.</summary>
    WideTrail = 2,
}
