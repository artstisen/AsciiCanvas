using Avalonia.Media;

namespace AsciiCanvas.Avalonia;

/// <summary>Settings for the window opened by <see cref="CanvasApp"/>.</summary>
public sealed class CanvasAppOptions
{
    /// <summary>The window title.</summary>
    public string Title { get; set; } = "AsciiCanvas";

    /// <summary>The initial number of columns.</summary>
    public int Columns { get; set; } = 80;

    /// <summary>The initial number of rows.</summary>
    public int Rows { get; set; } = 25;

    /// <summary>Whether the grid scales to the window or the window decides the number of columns and rows.</summary>
    public CanvasSizing Sizing { get; set; } = CanvasSizing.ScaleToFit;

    /// <summary>With <see cref="CanvasSizing.ScaleToFit"/>: keep cell proportions instead of stretching the grid to fill the window.</summary>
    public bool PreserveAspectRatio { get; set; } = true;

    /// <summary>The font size of the cells (the natural size, used for the initial window size).</summary>
    public double FontSize { get; set; } = 18;

    /// <summary>The cell font, which must be monospaced. Defaults to the control's monospaced font list.</summary>
    public FontFamily? FontFamily { get; set; }

    /// <summary>Maps the cell colors to RGB.</summary>
    public ColorPalette Palette { get; set; } = ColorPalette.Campbell;

    /// <summary>Close the window as soon as the program returns. When false the final screen stays until the user closes the window.</summary>
    public bool CloseWindowWhenProgramEnds { get; set; }
}
