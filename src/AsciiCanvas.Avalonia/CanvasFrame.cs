namespace AsciiCanvas.Avalonia;

/// <summary>Whether the window of <see cref="CanvasApp"/> has the frame of the operating system.</summary>
public enum CanvasFrame
{
    /// <summary>The window has the title bar and border of the operating system.</summary>
    System,

    /// <summary>
    /// The window has no title bar and no border: the whole window is the canvas, and the program draws its own frame, title bar
    /// and buttons. Use <see cref="CellCanvas.WindowDragRegions"/> to make cells move the window and <see cref="CellCanvas.WindowState"/>
    /// to minimize, maximize and restore it. A few pixels along each edge of the window resize it.
    /// </summary>
    None,
}
