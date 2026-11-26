using System.Collections.Frozen;
using System.Text;

namespace AsciiCanvas;

/// <summary>
/// Describes box-drawing characters as line arms so a renderer can draw them pixel-perfectly
/// (no gaps between cells) instead of relying on font glyphs.
/// </summary>
public static class BoxDrawing
{
    private static readonly FrozenDictionary<int, LineSegments> _table = Build();

    /// <summary>Looks up the arms of a box-drawing character.</summary>
    /// <param name="rune">The character.</param>
    /// <param name="segments">The arms, if the character is a known box-drawing character.</param>
    /// <returns>True if <paramref name="rune"/> is a known box-drawing character.</returns>
    public static bool TryGetSegments(Rune rune, out LineSegments segments) => _table.TryGetValue(rune.Value, out segments);

    private static FrozenDictionary<int, LineSegments> Build()
    {
        var table = new Dictionary<int, LineSegments>();

        void Add(char c, byte up, byte down, byte left, byte right) => table[c] = new LineSegments(up, down, left, right);

        // Light
        Add('─', 0, 0, 1, 1);
        Add('│', 1, 1, 0, 0);
        Add('┌', 0, 1, 0, 1);
        Add('┐', 0, 1, 1, 0);
        Add('└', 1, 0, 0, 1);
        Add('┘', 1, 0, 1, 0);
        Add('├', 1, 1, 0, 1);
        Add('┤', 1, 1, 1, 0);
        Add('┬', 0, 1, 1, 1);
        Add('┴', 1, 0, 1, 1);
        Add('┼', 1, 1, 1, 1);

        // Rounded corners are drawn as light corners.
        Add('╭', 0, 1, 0, 1);
        Add('╮', 0, 1, 1, 0);
        Add('╰', 1, 0, 0, 1);
        Add('╯', 1, 0, 1, 0);

        // Heavy
        Add('━', 0, 0, 2, 2);
        Add('┃', 2, 2, 0, 0);
        Add('┏', 0, 2, 0, 2);
        Add('┓', 0, 2, 2, 0);
        Add('┗', 2, 0, 0, 2);
        Add('┛', 2, 0, 2, 0);
        Add('┣', 2, 2, 0, 2);
        Add('┫', 2, 2, 2, 0);
        Add('┳', 0, 2, 2, 2);
        Add('┻', 2, 0, 2, 2);
        Add('╋', 2, 2, 2, 2);

        // Double
        Add('═', 0, 0, 3, 3);
        Add('║', 3, 3, 0, 0);
        Add('╔', 0, 3, 0, 3);
        Add('╗', 0, 3, 3, 0);
        Add('╚', 3, 0, 0, 3);
        Add('╝', 3, 0, 3, 0);
        Add('╠', 3, 3, 0, 3);
        Add('╣', 3, 3, 3, 0);
        Add('╦', 0, 3, 3, 3);
        Add('╩', 3, 0, 3, 3);
        Add('╬', 3, 3, 3, 3);

        return table.ToFrozenDictionary();
    }
}
