using System.Text;

namespace AsciiCanvas.Tests;

public class LayoutAndWidthTests
{
    [Theory]
    [InlineData('a', 1)]
    [InlineData('日', 2)]
    [InlineData('\u0301', 0)]
    [InlineData('\n', 0)]
    [InlineData('─', 1)]
    public void CharWidth_Of(char c, int expected) => Assert.Equal(expected, CharWidth.Of(c));

    [Fact]
    public void CharWidth_Emoji_IsWide() => Assert.Equal(2, CharWidth.Of(new Rune(0x1F600)));

    [Fact]
    public void Fit_PreservingAspect_CentersGrid()
    {
        // 10x5 cells of aspect 0.5 (w/h) in a 200x200 area: width-limited, cell 20x40 -> 200x200? check both limits.
        var layout = GridLayout.Fit(200, 200, 10, 5, 0.5, preserveAspect: true);

        Assert.Equal(20, layout.CellWidth, 6);
        Assert.Equal(40, layout.CellHeight, 6);
        Assert.Equal(0, layout.OffsetX, 6);
        Assert.Equal(0, layout.OffsetY, 6);

        var wide = GridLayout.Fit(400, 100, 10, 5, 0.5, preserveAspect: true);
        Assert.Equal(10, wide.CellWidth, 6);
        Assert.Equal(20, wide.CellHeight, 6);
        Assert.Equal(150, wide.OffsetX, 6);
    }

    [Fact]
    public void Fit_Stretching_FillsArea()
    {
        var layout = GridLayout.Fit(300, 100, 10, 5, 0.5, preserveAspect: false);

        Assert.Equal(30, layout.CellWidth, 6);
        Assert.Equal(20, layout.CellHeight, 6);
    }

    [Fact]
    public void CountFor_FloorsAndNeverReturnsZero()
    {
        Assert.Equal((10, 4), GridLayout.CountFor(105, 99, 10, 24));
        Assert.Equal((1, 1), GridLayout.CountFor(3, 3, 10, 24));
    }

    [Fact]
    public void CellAt_MapsPixelsAndClamps()
    {
        var layout = new GridLayout(10, 20, 5, 0);

        Assert.Equal((0, 0), layout.CellAt(2, 3, 80, 25));
        Assert.Equal((2, 1), layout.CellAt(27, 25, 80, 25));
        Assert.Equal((79, 24), layout.CellAt(5000, 5000, 80, 25));
    }

    [Fact]
    public void BoxDrawing_DescribesArms()
    {
        Assert.True(BoxDrawing.TryGetSegments(new Rune('┌'), out var corner));
        Assert.Equal(new LineSegments(0, 1, 0, 1), corner);
        Assert.True(BoxDrawing.TryGetSegments(new Rune('╬'), out var cross));
        Assert.Equal(new LineSegments(3, 3, 3, 3), cross);
        Assert.False(BoxDrawing.TryGetSegments(new Rune('a'), out _));
    }
}
