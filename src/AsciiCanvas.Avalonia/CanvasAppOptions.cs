using Avalonia.Media;

namespace AsciiCanvas.Avalonia;

/// <summary>Settings for the window opened by <see cref="CanvasApp"/>.</summary>
public sealed class CanvasAppOptions
{
    /// <summary>The window title.</summary>
    public string Title { get; init; } = "AsciiCanvas";

    /// <summary>The initial number of columns.</summary>
    public int Columns { get; init; } = 80;

    /// <summary>The initial number of rows.</summary>
    public int Rows { get; init; } = 25;

    /// <summary>Whether the grid scales to the window or the window decides the number of columns and rows.</summary>
    public CanvasSizing Sizing { get; init; } = CanvasSizing.ScaleToFit;

    /// <summary>With <see cref="CanvasSizing.ScaleToFit"/>: keep cell proportions instead of stretching the grid to fill the window.</summary>
    public bool PreserveAspectRatio { get; init; } = true;

    /// <summary>The font size of the cells (the natural size, used for the initial window size).</summary>
    public double FontSize { get; init; } = 18;

    /// <summary>The cell font, which must be monospaced. Defaults to <see cref="CanvasFonts.Default"/>; <see cref="CanvasFonts"/> lists the embedded fonts.</summary>
    public FontFamily? FontFamily { get; init; }

    /// <summary>Maps the cell colors to RGB.</summary>
    public ColorPalette Palette { get; init; } = ColorPalette.Campbell;

    /// <summary>Close the window as soon as the program returns. When false the final screen stays until the user closes the window.</summary>
    public bool CloseWindowWhenProgramEnds { get; init; }

    /// <summary>Shows performance diagnostics over the grid at start-up. See <see cref="CanvasView.ShowDiagnostics"/>.</summary>
    public bool ShowDiagnostics { get; init; }

    /// <summary>Lets F3 switch the diagnostics on and off. The key is then not passed on to the program. See <see cref="CanvasView.AllowDiagnosticsToggle"/>.</summary>
    public bool AllowDiagnosticsToggle { get; init; }
}
