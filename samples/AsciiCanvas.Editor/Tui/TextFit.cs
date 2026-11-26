using System.Text;

namespace AsciiCanvas.Editor.Tui;

/// <summary>Fits text into a number of grid columns, counting wide characters as two.</summary>
internal static class TextFit
{
    /// <summary>Shortens text to fit, marking the cut with an ellipsis.</summary>
    /// <param name="text">The text.</param>
    /// <param name="width">The available columns.</param>
    /// <param name="fromStart">True to cut the start (good for paths), false to cut the end.</param>
    public static string Truncate(string text, int width, bool fromStart = false)
    {
        if (width <= 0)
        {
            return "";
        }

        if (CharWidth.Of(text) <= width)
        {
            return text;
        }

        var room = width - 1;
        var runes = text.EnumerateRunes().ToList();
        var kept = new StringBuilder();
        var used = 0;

        if (fromStart)
        {
            for (var i = runes.Count - 1; i >= 0; i--)
            {
                var w = CharWidth.Of(runes[i]);
                if (used + w > room)
                {
                    break;
                }

                used += w;
                kept.Insert(0, runes[i].ToString());
            }

            return "…" + kept;
        }

        foreach (var rune in runes)
        {
            var w = CharWidth.Of(rune);
            if (used + w > room)
            {
                break;
            }

            used += w;
            kept.Append(rune.ToString());
        }

        return kept + "…";
    }

    /// <summary>Breaks text into lines of at most <paramref name="width"/> columns, at spaces where possible.</summary>
    /// <param name="text">The text; <c>\n</c> starts a new line.</param>
    /// <param name="width">The available columns.</param>
    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var separator = line.Length == 0 ? 0 : 1;
                if (line.Length > 0 && CharWidth.Of(line.ToString()) + separator + CharWidth.Of(word) > width)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    separator = 0;
                }

                if (separator == 1)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }

            lines.Add(line.ToString());
        }

        return lines;
    }
}
