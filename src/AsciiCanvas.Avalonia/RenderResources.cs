using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AsciiCanvas.Avalonia;

/// <summary>Brushes and text layouts reused between frames. Both caches are bounded and simply restart when full.</summary>
internal sealed class RenderResources
{
    private const int MaxBrushes = 4096;
    private const int MaxGlyphs = 8192;

    private readonly Dictionary<uint, IBrush> _brushes = [];
    private readonly Dictionary<GlyphKey, FormattedText> _glyphs = [];
    private double _glyphFontSize;

    private readonly record struct GlyphKey(int Rune, uint Color, bool Bold, bool Italic);

    public static FormattedText CreateText(string text, FontFamily fontFamily, double fontSize, IBrush brush, bool bold, bool italic) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            fontSize,
            brush);

    /// <summary>Linear blend of two 0xRRGGBB colors.</summary>
    public static uint Mix(uint from, uint to, double amount)
    {
        static uint Channel(uint a, uint b, double t) => (uint)Math.Round(a + ((b - a) * t));
        return (Channel((from >> 16) & 0xFF, (to >> 16) & 0xFF, amount) << 16)
            | (Channel((from >> 8) & 0xFF, (to >> 8) & 0xFF, amount) << 8)
            | Channel(from & 0xFF, to & 0xFF, amount);
    }

    /// <summary>Resolves the final foreground and background of a style, applying dim and reverse.</summary>
    public static void ResolveColors(ColorPalette palette, CellStyle style, out uint foreground, out uint background)
    {
        foreground = palette.Resolve(style.Foreground, foreground: true);
        background = palette.Resolve(style.Background, foreground: false);
        if ((style.Attributes & CellAttributes.Dim) != 0)
        {
            foreground = Mix(foreground, background, 0.5);
        }

        if ((style.Attributes & CellAttributes.Reverse) != 0)
        {
            (foreground, background) = (background, foreground);
        }
    }

    public IBrush BrushFor(uint rgb)
    {
        if (!_brushes.TryGetValue(rgb, out var brush))
        {
            if (_brushes.Count >= MaxBrushes)
            {
                _brushes.Clear();
            }

            brush = new ImmutableSolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            _brushes[rgb] = brush;
        }

        return brush;
    }

    public FormattedText GlyphFor(Rune rune, uint foreground, bool bold, bool italic, FontFamily fontFamily, double fontSize)
    {
        if (Math.Abs(fontSize - _glyphFontSize) > 0.001 || _glyphs.Count >= MaxGlyphs)
        {
            _glyphs.Clear();
            _glyphFontSize = fontSize;
        }

        var key = new GlyphKey(rune.Value, foreground, bold, italic);
        if (!_glyphs.TryGetValue(key, out var text))
        {
            text = CreateText(rune.ToString(), fontFamily, fontSize, BrushFor(foreground), bold, italic);
            _glyphs[key] = text;
        }

        return text;
    }

    public void ClearGlyphs() => _glyphs.Clear();
}
