namespace AsciiCanvas;

/// <summary>The characters used to draw a box in a given style.</summary>
internal readonly record struct BoxChars(
    char Horizontal,
    char Vertical,
    char TopLeft,
    char TopRight,
    char BottomLeft,
    char BottomRight)
{
    public static BoxChars For(BoxStyle style) => style switch
    {
        BoxStyle.SingleLine => new('─', '│', '┌', '┐', '└', '┘'),
        BoxStyle.DoubleLine => new('═', '║', '╔', '╗', '╚', '╝'),
        BoxStyle.Rounded => new('─', '│', '╭', '╮', '╰', '╯'),
        BoxStyle.Heavy => new('━', '┃', '┏', '┓', '┗', '┛'),
        BoxStyle.Ascii => new('-', '|', '+', '+', '+', '+'),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };
}
