using Avalonia.Media;

namespace AsciiCanvas.Avalonia;

/// <summary>Settings for the window opened by <see cref="CanvasApp"/>.</summary>
public sealed class CanvasAppOptions
{
    /// <summary>The initial window title. The program can change it later with <see cref="CellCanvas.Title"/>.</summary>
    public string Title { get; init; } = "AsciiCanvas";

    /// <summary>
    /// The initial window icon, as the bytes of an image file (PNG or ICO), for example from <c>File.ReadAllBytes</c> or an embedded resource.
    /// Empty (the default) shows the AsciiCanvas icon, or the icon of the executable with <see cref="UseExecutableIcon"/>.
    /// The program can change it later with <see cref="CellCanvas.Icon"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Icon { get; init; }

    /// <summary>
    /// With no <see cref="Icon"/>, show the icon of the executable (on Windows, the one set with <c>ApplicationIcon</c> in the project)
    /// instead of the AsciiCanvas icon. Other platforms have no executable icon for a window and show their default.
    /// </summary>
    public bool UseExecutableIcon { get; init; }

    /// <summary>Whether the window has the frame of the operating system or none, so the program draws its own. See <see cref="CanvasFrame.None"/>.</summary>
    public CanvasFrame Frame { get; init; } = CanvasFrame.System;

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

    /// <summary>Turns Ctrl+V, Cmd+V and Shift+Insert into a <see cref="PasteEvent"/>. See <see cref="CanvasView.InterceptPasteShortcut"/>.</summary>
    public bool InterceptPasteShortcut { get; init; } = true;
}
