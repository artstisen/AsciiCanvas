namespace AsciiCanvas;

/// <summary>Finds which resize edge of a window without a frame lies under a pointer. Pure math, shared by hosts.</summary>
public static class ResizeEdges
{
    /// <summary>
    /// Finds the edge under a position. A band of <paramref name="grip"/> pixels along each side is an edge; near a corner
    /// (within twice the grip along the side) the band belongs to the corner.
    /// </summary>
    /// <param name="x">The horizontal position in pixels, from the left of the window.</param>
    /// <param name="y">The vertical position in pixels, from the top of the window.</param>
    /// <param name="width">The width of the window in pixels.</param>
    /// <param name="height">The height of the window in pixels.</param>
    /// <param name="grip">The thickness of the band in pixels.</param>
    /// <returns>The edge, or <see cref="ResizeEdge.None"/> if the position is not within the grip of a side.</returns>
    public static ResizeEdge At(double x, double y, double width, double height, double grip)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || grip <= 0)
        {
            return ResizeEdge.None;
        }

        var reach = grip * 2;
        var left = x < grip;
        var right = x >= width - grip;
        var top = y < grip;
        var bottom = y >= height - grip;

        var nearLeft = x < reach;
        var nearRight = x >= width - reach;
        var nearTop = y < reach;
        var nearBottom = y >= height - reach;

        if ((top && nearLeft) || (left && nearTop))
        {
            return ResizeEdge.TopLeft;
        }

        if ((top && nearRight) || (right && nearTop))
        {
            return ResizeEdge.TopRight;
        }

        if ((bottom && nearLeft) || (left && nearBottom))
        {
            return ResizeEdge.BottomLeft;
        }

        if ((bottom && nearRight) || (right && nearBottom))
        {
            return ResizeEdge.BottomRight;
        }

        if (left)
        {
            return ResizeEdge.Left;
        }

        if (right)
        {
            return ResizeEdge.Right;
        }

        if (top)
        {
            return ResizeEdge.Top;
        }

        return bottom ? ResizeEdge.Bottom : ResizeEdge.None;
    }
}
