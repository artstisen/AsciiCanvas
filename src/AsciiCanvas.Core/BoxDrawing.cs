using System.Text;

namespace AsciiCanvas;

public enum BoxStyle
{
    Single,
    Double,
    Rounded,
    Heavy,
    Ascii,
}

/// <summary>The characters used to draw a box in a given style.</summary>
public readonly record struct BoxChars(
    char Horizontal,
    char Vertical,
    char TopLeft,
    char TopRight,
    char BottomLeft,
    char BottomRight)
{
    public static BoxChars For(BoxStyle style) => style switch
    {
        BoxStyle.Single => new('─', '│', '┌', '┐', '└', '┘'),
        BoxStyle.Double => new('═', '║', '╔', '╗', '╚', '╝'),
        BoxStyle.Rounded => new('─', '│', '╭', '╮', '╰', '╯'),
        BoxStyle.Heavy => new('━', '┃', '┏', '┓', '┗', '┛'),
        BoxStyle.Ascii => new('-', '|', '+', '+', '+', '+'),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };
}

/// <summary>Weight of each arm of a box-drawing character: 0 none, 1 light, 2 heavy, 3 double.</summary>
public readonly record struct LineSegments(byte Up, byte Down, byte Left, byte Right);

/// <summary>
/// Describes box-drawing characters as line arms so a renderer can draw them pixel-perfectly
/// (no gaps between cells) instead of relying on font glyphs.
/// </summary>
public static class BoxDrawing
{
    private static readonly Dictionary<int, LineSegments> Table = Build();

    public static bool TryGetSegments(Rune rune, out LineSegments segments) => Table.TryGetValue(rune.Value, out segments);

    private static Dictionary<int, LineSegments> Build()
    {
        var table = new Dictionary<int, LineSegments>();

        void Add(char c, byte up, byte down, byte left, byte right) => table[c] = new LineSegments(up, down, left, right);

        // Light
        Add('─', 0, 0, 1, 1); Add('│', 1, 1, 0, 0);
        Add('┌', 0, 1, 0, 1); Add('┐', 0, 1, 1, 0); Add('└', 1, 0, 0, 1); Add('┘', 1, 0, 1, 0);
        Add('├', 1, 1, 0, 1); Add('┤', 1, 1, 1, 0); Add('┬', 0, 1, 1, 1); Add('┴', 1, 0, 1, 1); Add('┼', 1, 1, 1, 1);

        // Rounded corners are drawn as light corners.
        Add('╭', 0, 1, 0, 1); Add('╮', 0, 1, 1, 0); Add('╰', 1, 0, 0, 1); Add('╯', 1, 0, 1, 0);

        // Heavy
        Add('━', 0, 0, 2, 2); Add('┃', 2, 2, 0, 0);
        Add('┏', 0, 2, 0, 2); Add('┓', 0, 2, 2, 0); Add('┗', 2, 0, 0, 2); Add('┛', 2, 0, 2, 0);
        Add('┣', 2, 2, 0, 2); Add('┫', 2, 2, 2, 0); Add('┳', 0, 2, 2, 2); Add('┻', 2, 0, 2, 2); Add('╋', 2, 2, 2, 2);

        // Double
        Add('═', 0, 0, 3, 3); Add('║', 3, 3, 0, 0);
        Add('╔', 0, 3, 0, 3); Add('╗', 0, 3, 3, 0); Add('╚', 3, 0, 0, 3); Add('╝', 3, 0, 3, 0);
        Add('╠', 3, 3, 0, 3); Add('╣', 3, 3, 3, 0); Add('╦', 0, 3, 3, 3); Add('╩', 3, 0, 3, 3); Add('╬', 3, 3, 3, 3);

        return table;
    }
}
