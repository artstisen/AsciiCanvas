namespace AsciiCanvas;

/// <summary>Maps <see cref="CellColor"/> values to 0xRRGGBB colors. Entries 0-15 and the default colors are themable.</summary>
public sealed class ColorPalette
{
    private readonly uint[] _colors = new uint[256];

    /// <summary>Creates a palette from the 16 standard colors; the cube and grayscale ramp are computed.</summary>
    /// <param name="standardColors">Exactly 16 colors as 0xRRGGBB.</param>
    /// <param name="defaultForeground">Color used for <see cref="CellColor.Default"/> as a foreground.</param>
    /// <param name="defaultBackground">Color used for <see cref="CellColor.Default"/> as a background.</param>
    public ColorPalette(ReadOnlySpan<uint> standardColors, uint defaultForeground, uint defaultBackground)
    {
        if (standardColors.Length != 16)
        {
            throw new ArgumentException("Exactly 16 standard colors are required.", nameof(standardColors));
        }

        standardColors.CopyTo(_colors);

        ReadOnlySpan<int> levels = [0, 95, 135, 175, 215, 255];
        for (var i = 0; i < 216; i++)
        {
            _colors[16 + i] = Rgb(levels[i / 36], levels[i / 6 % 6], levels[i % 6]);
        }

        for (var i = 0; i < 24; i++)
        {
            var v = 8 + (10 * i);
            _colors[232 + i] = Rgb(v, v, v);
        }

        DefaultForeground = defaultForeground;
        DefaultBackground = defaultBackground;
    }

    /// <summary>Windows Terminal "Campbell" scheme.</summary>
    public static ColorPalette Campbell { get; } = new(
        [
            0x0C0C0C, 0xC50F1F, 0x13A10E, 0xC19C00, 0x0037DA, 0x881798, 0x3A96DD, 0xCCCCCC,
            0x767676, 0xE74856, 0x16C60C, 0xF9F1A5, 0x3B78FF, 0xB4009E, 0x61D6D6, 0xF2F2F2,
        ],
        defaultForeground: 0xCCCCCC,
        defaultBackground: 0x0C0C0C);

    /// <summary>Classic xterm colors.</summary>
    public static ColorPalette Xterm { get; } = new(
        [
            0x000000, 0xCD0000, 0x00CD00, 0xCDCD00, 0x0000EE, 0xCD00CD, 0x00CDCD, 0xE5E5E5,
            0x7F7F7F, 0xFF0000, 0x00FF00, 0xFFFF00, 0x5C5CFF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
        ],
        defaultForeground: 0xE5E5E5,
        defaultBackground: 0x000000);

    /// <summary>The 0xRRGGBB color used for <see cref="CellColor.Default"/> as a foreground.</summary>
    public uint DefaultForeground { get; }

    /// <summary>The 0xRRGGBB color used for <see cref="CellColor.Default"/> as a background.</summary>
    public uint DefaultBackground { get; }

    /// <summary>Gets the 0xRRGGBB value for a palette index (0-255).</summary>
    /// <param name="index">The palette index.</param>
    public uint this[int index] => _colors[index];

    /// <summary>Gets the 0xRRGGBB value for a color, resolving <see cref="CellColor.Default"/> to the default foreground or background.</summary>
    /// <param name="color">The color to resolve.</param>
    /// <param name="foreground">True to resolve a default color as foreground, false as background.</param>
    public uint Resolve(CellColor color, bool foreground) =>
        color.IsDefault ? (foreground ? DefaultForeground : DefaultBackground) : _colors[color.Index];

    private static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);
}
