namespace AsciiCanvas;

/// <summary>Converts between the 16 <see cref="ConsoleColor"/> values and <see cref="CellColor"/>.</summary>
internal static class ColorMap
{
    // ConsoleColor puts blue in the lowest bit and red in the third, ANSI the other way round; swapping the two bits converts either way.
    private static int SwapRedAndBlue(int index) => (index & 8) | ((index & 1) << 2) | (index & 2) | ((index & 4) >> 2);

    /// <summary>Gets the cell color for a console color.</summary>
    /// <param name="color">One of the 16 console colors.</param>
    public static CellColor ToCellColor(ConsoleColor color)
    {
        var value = (int)color;
        ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(color));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 15, nameof(color));
        return CellColor.FromIndex((byte)SwapRedAndBlue(value));
    }

    /// <summary>Gets the console color for a cell color, or <paramref name="fallback"/> for the default color and colors outside the first 16.</summary>
    /// <param name="color">The cell color.</param>
    /// <param name="fallback">What to return when there is no matching console color.</param>
    public static ConsoleColor ToConsoleColor(CellColor color, ConsoleColor fallback) =>
        color.IsDefault || color.Index > 15 ? fallback : (ConsoleColor)SwapRedAndBlue(color.Index);
}
