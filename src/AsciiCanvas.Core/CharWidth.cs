using System.Globalization;
using System.Text;

namespace AsciiCanvas;

/// <summary>Number of grid columns a character occupies: 0 (combining/control), 1 or 2 (East Asian wide and emoji).</summary>
public static class CharWidth
{
    // Sorted, non-overlapping inclusive ranges of double-width code points.
    private static readonly (int Start, int End)[] _wideRanges =
    [
        (0x1100, 0x115F), (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0),
        (0x23F3, 0x23F3), (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2648, 0x2653), (0x267F, 0x267F),
        (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5),
        (0x26CE, 0x26CE), (0x26D4, 0x26D4), (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5),
        (0x26FA, 0x26FA), (0x26FD, 0x26FD), (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728),
        (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755), (0x2757, 0x2757), (0x2795, 0x2797),
        (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C), (0x2B50, 0x2B50), (0x2B55, 0x2B55),
        (0x2E80, 0x303E), (0x3041, 0x33FF), (0x3400, 0x4DBF), (0x4E00, 0xA4CF), (0xA960, 0xA97F),
        (0xAC00, 0xD7A3), (0xF900, 0xFAFF), (0xFE10, 0xFE19), (0xFE30, 0xFE6F), (0xFF01, 0xFF60),
        (0xFFE0, 0xFFE6), (0x1F004, 0x1F004), (0x1F0CF, 0x1F0CF), (0x1F18E, 0x1F18E), (0x1F191, 0x1F19A),
        (0x1F200, 0x1F2FF), (0x1F300, 0x1F64F), (0x1F680, 0x1F6FF), (0x1F900, 0x1F9FF), (0x1FA70, 0x1FAFF),
        (0x20000, 0x2FFFD), (0x30000, 0x3FFFD),
    ];

    /// <summary>Gets the number of columns a character occupies.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>0 for control and combining characters, 2 for wide characters, otherwise 1.</returns>
    public static int Of(Rune rune)
    {
        var v = rune.Value;
        if (v is < 0x20 or (>= 0x7F and < 0xA0))
        {
            return 0;
        }

        if (v < 0x300)
        {
            return 1;
        }

        switch (Rune.GetUnicodeCategory(rune))
        {
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.Format:
            case UnicodeCategory.Control:
                return 0;
        }

        return IsWide(v) ? 2 : 1;
    }

    /// <summary>Gets the number of columns a UTF-16 character occupies.</summary>
    /// <param name="c">The character.</param>
    public static int Of(char c) => Of(new Rune(c));

    /// <summary>Gets the total column width of a string.</summary>
    /// <param name="text">The text to measure.</param>
    public static int Of(ReadOnlySpan<char> text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += Of(rune);
        }

        return width;
    }

    private static bool IsWide(int codePoint)
    {
        var lo = 0;
        var hi = _wideRanges.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            var (start, end) = _wideRanges[mid];
            if (codePoint < start)
            {
                hi = mid - 1;
            }
            else if (codePoint > end)
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
