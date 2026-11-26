namespace AsciiCanvas;

/// <summary>An edge or corner of a window that can be dragged to resize it.</summary>
public enum ResizeEdge
{
    /// <summary>Not on an edge.</summary>
    None,

    /// <summary>The left edge.</summary>
    Left,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The bottom edge.</summary>
    Bottom,

    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,
}
