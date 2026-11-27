namespace AsciiCanvas;

/// <summary>The state of the window that shows a canvas.</summary>
public enum CanvasWindowState
{
    /// <summary>The window has its normal size and position.</summary>
    Normal,

    /// <summary>The window is minimized to the taskbar or dock.</summary>
    Minimized,

    /// <summary>The window fills the screen (or its work area).</summary>
    Maximized,

    /// <summary>The window covers the whole screen, without its frame or the taskbar.</summary>
    FullScreen,
}
