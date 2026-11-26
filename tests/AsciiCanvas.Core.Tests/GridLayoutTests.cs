namespace AsciiCanvas.Tests;

public class GridLayoutTests
{
    [Fact]
    public void Fit_PreserveAspect_CentersGrid()
    {
        var square = GridLayout.Fit(200, 200, 10, 5, 0.5, preserveAspect: true);
        var wide = GridLayout.Fit(400, 100, 10, 5, 0.5, preserveAspect: true);

        Assert.Equal(20, square.CellWidth, 6);
        Assert.Equal(40, square.CellHeight, 6);
        Assert.Equal(0, square.OffsetX, 6);
        Assert.Equal(0, square.OffsetY, 6);
        Assert.Equal(10, wide.CellWidth, 6);
        Assert.Equal(20, wide.CellHeight, 6);
        Assert.Equal(150, wide.OffsetX, 6);
    }

    [Fact]
    public void Fit_StretchingAllowed_FillsArea()
    {
        var layout = GridLayout.Fit(300, 100, 10, 5, 0.5, preserveAspect: false);

        Assert.Equal(30, layout.CellWidth, 6);
        Assert.Equal(20, layout.CellHeight, 6);
    }

    [Fact]
    public void CountFor_PartialCells_FloorsAndNeverReturnsZero()
    {
        Assert.Equal((10, 4), GridLayout.CountFor(105, 99, 10, 24));
        Assert.Equal((1, 1), GridLayout.CountFor(3, 3, 10, 24));
    }

    [Fact]
    public void CellAt_PixelInsideAndOutsideGrid_MapsAndClamps()
    {
        var layout = new GridLayout(10, 20, 5, 0);

        Assert.Equal((0, 0), layout.CellAt(2, 3, 80, 25));
        Assert.Equal((2, 1), layout.CellAt(27, 25, 80, 25));
        Assert.Equal((79, 24), layout.CellAt(5000, 5000, 80, 25));
    }
}
