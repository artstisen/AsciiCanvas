namespace AsciiCanvas;

/// <summary>
/// A color from the 256-color palette, or the "default" color of the host (theme foreground/background).
/// <c>default(CellColor)</c> is <see cref="Default"/>.
/// </summary>
public readonly struct CellColor : IEquatable<CellColor>
{
    // 0 = host default, otherwise palette index + 1.
    private readonly ushort _raw;

    private CellColor(ushort raw) => _raw = raw;

    /// <summary>The host's default foreground or background color, depending on where it is used.</summary>
    public static CellColor Default => default;

    /// <summary>Palette color 0.</summary>
    public static CellColor Black { get; } = FromIndex(0);

    /// <summary>Palette color 1.</summary>
    public static CellColor Red { get; } = FromIndex(1);

    /// <summary>Palette color 2.</summary>
    public static CellColor Green { get; } = FromIndex(2);

    /// <summary>Palette color 3.</summary>
    public static CellColor Yellow { get; } = FromIndex(3);

    /// <summary>Palette color 4.</summary>
    public static CellColor Blue { get; } = FromIndex(4);

    /// <summary>Palette color 5.</summary>
    public static CellColor Magenta { get; } = FromIndex(5);

    /// <summary>Palette color 6.</summary>
    public static CellColor Cyan { get; } = FromIndex(6);

    /// <summary>Palette color 7.</summary>
    public static CellColor White { get; } = FromIndex(7);

    /// <summary>Palette color 8.</summary>
    public static CellColor BrightBlack { get; } = FromIndex(8);

    /// <summary>Palette color 9.</summary>
    public static CellColor BrightRed { get; } = FromIndex(9);

    /// <summary>Palette color 10.</summary>
    public static CellColor BrightGreen { get; } = FromIndex(10);

    /// <summary>Palette color 11.</summary>
    public static CellColor BrightYellow { get; } = FromIndex(11);

    /// <summary>Palette color 12.</summary>
    public static CellColor BrightBlue { get; } = FromIndex(12);

    /// <summary>Palette color 13.</summary>
    public static CellColor BrightMagenta { get; } = FromIndex(13);

    /// <summary>Palette color 14.</summary>
    public static CellColor BrightCyan { get; } = FromIndex(14);

    /// <summary>Palette color 15.</summary>
    public static CellColor BrightWhite { get; } = FromIndex(15);

    /// <summary>True for <see cref="Default"/>.</summary>
    public bool IsDefault => _raw == 0;

    /// <summary>The palette index (0-255), or -1 for the default color.</summary>
    public int Index => _raw - 1;

    /// <summary>Creates a color from a palette index (0-255).</summary>
    public static CellColor FromIndex(byte index) => new((ushort)(index + 1));

    /// <summary>Creates a color from the 6x6x6 color cube; each component is 0-5.</summary>
    public static CellColor Cube(int red, int green, int blue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(red);
        ArgumentOutOfRangeException.ThrowIfNegative(green);
        ArgumentOutOfRangeException.ThrowIfNegative(blue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(red, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(green, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(blue, 5);
        return FromIndex((byte)(16 + (36 * red) + (6 * green) + blue));
    }

    /// <summary>Creates a shade from the 24-step grayscale ramp (0 = darkest, 23 = lightest).</summary>
    public static CellColor Gray(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 23);
        return FromIndex((byte)(232 + level));
    }

    /// <inheritdoc/>
    public bool Equals(CellColor other) => _raw == other._raw;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CellColor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _raw;

    /// <inheritdoc/>
    public override string ToString() => IsDefault ? "Default" : $"Color({Index})";

    /// <summary>Compares two colors for equality.</summary>
    public static bool operator ==(CellColor left, CellColor right) => left.Equals(right);

    /// <summary>Compares two colors for inequality.</summary>
    public static bool operator !=(CellColor left, CellColor right) => !left.Equals(right);
}
