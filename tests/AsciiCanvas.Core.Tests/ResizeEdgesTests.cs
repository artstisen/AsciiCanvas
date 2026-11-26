namespace AsciiCanvas.Core.Tests;

public sealed class ResizeEdgesTests
{
    [Theory]
    [InlineData(2, 300, ResizeEdge.Left)]
    [InlineData(798, 300, ResizeEdge.Right)]
    [InlineData(400, 2, ResizeEdge.Top)]
    [InlineData(400, 598, ResizeEdge.Bottom)]
    [InlineData(2, 2, ResizeEdge.TopLeft)]
    [InlineData(797, 3, ResizeEdge.TopRight)]
    [InlineData(2, 597, ResizeEdge.BottomLeft)]
    [InlineData(797, 597, ResizeEdge.BottomRight)]
    [InlineData(2, 10, ResizeEdge.TopLeft)]
    [InlineData(10, 2, ResizeEdge.TopLeft)]
    [InlineData(400, 300, ResizeEdge.None)]
    [InlineData(7, 300, ResizeEdge.None)]
    public void At_ForPositionsInAWindow_FindsTheEdge(double x, double y, ResizeEdge expected)
    {
        var edge = ResizeEdges.At(x, y, 800, 600, grip: 6);

        Assert.Equal(expected, edge);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(800, 10)]
    [InlineData(10, 600)]
    public void At_ForPositionsOutsideTheWindow_ReturnsNone(double x, double y)
    {
        Assert.Equal(ResizeEdge.None, ResizeEdges.At(x, y, 800, 600, grip: 6));
    }
}
