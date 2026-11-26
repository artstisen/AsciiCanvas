namespace AsciiCanvas;

/// <summary>The line style of a box or line drawn with <see cref="CellCanvas"/>.</summary>
public enum BoxStyle
{
    /// <summary>A thin single line.</summary>
    SingleLine,

    /// <summary>A double line.</summary>
    DoubleLine,

    /// <summary>A thin line with rounded corners.</summary>
    Rounded,

    /// <summary>A thick single line.</summary>
    Heavy,

    /// <summary>Plain ASCII characters (<c>+ - |</c>).</summary>
    Ascii,
}
