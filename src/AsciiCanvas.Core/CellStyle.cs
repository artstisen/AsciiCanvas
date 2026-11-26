namespace AsciiCanvas;

/// <summary>Foreground, background and attributes of a cell.</summary>
/// <param name="Foreground">The text color.</param>
/// <param name="Background">The cell background color.</param>
/// <param name="Attributes">Text attributes such as bold or underline.</param>
public readonly record struct CellStyle(CellColor Foreground, CellColor Background, CellAttributes Attributes = CellAttributes.None)
{
    /// <summary>Creates a style with a foreground color and the default background.</summary>
    /// <param name="foreground">The text color.</param>
    public CellStyle(CellColor foreground)
        : this(foreground, CellColor.Default)
    {
    }

    /// <summary>The default colors with no attributes.</summary>
    public static CellStyle Default => default;
}
