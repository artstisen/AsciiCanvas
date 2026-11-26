using System.Text;

namespace AsciiCanvas.Tests;

public class BoxDrawingTests
{
    [Fact]
    public void TryGetSegments_KnownCharacters_DescribesArms()
    {
        Assert.True(BoxDrawing.TryGetSegments(new Rune('┌'), out var corner));
        Assert.Equal(new LineSegments(0, 1, 0, 1), corner);
        Assert.True(BoxDrawing.TryGetSegments(new Rune('╬'), out var cross));
        Assert.Equal(new LineSegments(3, 3, 3, 3), cross);
    }

    [Fact]
    public void TryGetSegments_OrdinaryCharacter_ReturnsFalse() =>
        Assert.False(BoxDrawing.TryGetSegments(new Rune('a'), out _));
}
