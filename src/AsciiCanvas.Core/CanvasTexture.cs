namespace AsciiCanvas;

/// <summary>
/// A still texture that the host paints behind the cells (<see cref="CellCanvas.Texture"/>): a frozen plasma pattern, one shade per
/// cell, that is strongest at the right edge and fades out toward the left. It shows only in cells with the default background,
/// so text and colored cells are drawn over it, and it stays in place while the content scrolls.
/// <para>
/// The shades are mixed from the palette's default background toward <see cref="Tint"/> in RGB, so they are finer than the
/// 256-color palette of the cells allows. The pattern is measured from the right edge, so a wider window shows more of it on the left.
/// </para>
/// </summary>
public sealed record CanvasTexture
{
    /// <summary>The highest <see cref="Strength"/>.</summary>
    public const int MaxStrength = 8;

    // How far each step of strength mixes the background toward the tint: 10/255, so with a white tint on a near-black
    // background the steps are close to those of the palette's gray ramp.
    private const double StepAmount = 10.0 / 255;

    private const double Time = 2.4;

    private static readonly int[] _bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>The color the shades are mixed toward, as 0xRRGGBB. White (the default) gives grays.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is above 0xFFFFFF.</exception>
    public uint Tint
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 0xFFFFFFu);
            field = value;
        }
    } = 0xFFFFFF;

    /// <summary>The number of shades above the background, 1 to <see cref="MaxStrength"/>; the strongest is used at the right edge. Default 7.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1 to <see cref="MaxStrength"/>.</exception>
    public int Strength
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxStrength);
            field = value;
        }
    } = 7;

    /// <summary>How much of the width, in percent from the right edge, the texture fades out over (10 to 100). Default 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 10 to 100.</exception>
    public int Fade
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 10);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            field = value;
        }
    } = 60;

    /// <summary>True (the default) to dither between neighboring shades, which turns the steps into a fine grain; false shows bands.</summary>
    public bool Dither { get; init; } = true;

    /// <summary>True (the default) to paint the texture on all rows; false leaves the bottom <see cref="CellCanvas.ScrollbackPinnedRows"/> plain.</summary>
    public bool CoverPinnedRows { get; init; } = true;

    /// <summary>The shade of one cell: 0 is the background, up to <see cref="Strength"/>.</summary>
    /// <param name="column">The column of the cell.</param>
    /// <param name="row">The row of the cell.</param>
    /// <param name="columns">The width of the canvas, which the pattern and the fade are measured from.</param>
    /// <returns>The shade, 0 to <see cref="Strength"/>.</returns>
    public int ShadeAt(int column, int row, int columns)
    {
        if (columns <= 0 || column < 0 || column >= columns || row < 0)
        {
            return 0;
        }

        // From the right edge, so the pattern stays anchored there when the width changes.
        var x = columns - 1 - column;
        var v = Math.Sin((x * 0.09) + Time)
            + Math.Sin((row * 0.17) - (Time * 1.3))
            + Math.Sin(((x + row) * 0.05) + (Time * 0.7))
            + Math.Sin((Math.Sqrt(((x * x) / 4.0) + (row * row)) * 0.2) - Time);
        var n = (v + 4) / 8;

        var weight = Math.Clamp(1 - ((double)x / columns / (Fade / 100.0)), 0, 1);
        var level = n * weight * weight * Strength;
        var shade = Dither
            ? (int)level + (level - (int)level > (_bayer[((row & 3) * 4) + (column & 3)] + 0.5) / 16 ? 1 : 0)
            : (int)Math.Round(level);
        return Math.Clamp(shade, 0, Strength);
    }

    /// <summary>The color of a shade: <paramref name="background"/> mixed toward <see cref="Tint"/>.</summary>
    /// <param name="shade">The shade, 0 to <see cref="MaxStrength"/>; 0 is the background itself.</param>
    /// <param name="background">The default background of the palette, as 0xRRGGBB.</param>
    /// <returns>The color as 0xRRGGBB.</returns>
    public uint ColorOf(int shade, uint background)
    {
        var amount = Math.Clamp(shade, 0, MaxStrength) * StepAmount;
        return (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);

        uint Channel(int shift)
        {
            var from = (background >> shift) & 0xFF;
            var to = (Tint >> shift) & 0xFF;
            return (uint)Math.Round(from + ((to - (double)from) * amount));
        }
    }
}
