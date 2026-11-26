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

    public static CellColor Default => default;

    public static CellColor FromIndex(byte index) => new((ushort)(index + 1));

    /// <summary>A color from the 6x6x6 color cube; each component is 0-5.</summary>
    public static CellColor Cube(int red, int green, int blue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(red);
        ArgumentOutOfRangeException.ThrowIfNegative(green);
        ArgumentOutOfRangeException.ThrowIfNegative(blue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(red, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(green, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(blue, 5);
        return FromIndex((byte)(16 + 36 * red + 6 * green + blue));
    }

    /// <summary>A shade from the 24-step grayscale ramp (0 = darkest, 23 = lightest).</summary>
    public static CellColor Gray(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 23);
        return FromIndex((byte)(232 + level));
    }

    public bool IsDefault => _raw == 0;

    /// <summary>The palette index (0-255), or -1 for the default color.</summary>
    public int Index => _raw - 1;

    public static readonly CellColor Black = FromIndex(0);
    public static readonly CellColor Red = FromIndex(1);
    public static readonly CellColor Green = FromIndex(2);
    public static readonly CellColor Yellow = FromIndex(3);
    public static readonly CellColor Blue = FromIndex(4);
    public static readonly CellColor Magenta = FromIndex(5);
    public static readonly CellColor Cyan = FromIndex(6);
    public static readonly CellColor White = FromIndex(7);
    public static readonly CellColor BrightBlack = FromIndex(8);
    public static readonly CellColor BrightRed = FromIndex(9);
    public static readonly CellColor BrightGreen = FromIndex(10);
    public static readonly CellColor BrightYellow = FromIndex(11);
    public static readonly CellColor BrightBlue = FromIndex(12);
    public static readonly CellColor BrightMagenta = FromIndex(13);
    public static readonly CellColor BrightCyan = FromIndex(14);
    public static readonly CellColor BrightWhite = FromIndex(15);

    public bool Equals(CellColor other) => _raw == other._raw;
    public override bool Equals(object? obj) => obj is CellColor other && Equals(other);
    public override int GetHashCode() => _raw;
    public static bool operator ==(CellColor left, CellColor right) => left.Equals(right);
    public static bool operator !=(CellColor left, CellColor right) => !left.Equals(right);

    public override string ToString() => IsDefault ? "Default" : $"Color({Index})";
}
