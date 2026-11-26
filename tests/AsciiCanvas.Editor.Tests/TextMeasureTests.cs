namespace AsciiCanvas.Editor.Tests;

public class TextMeasureTests
{
    [Fact]
    public void DisplayColumn_WideCharactersAndTabs_CountsCells()
    {
        const string line = "日a\tb";

        Assert.Equal(2, TextMeasure.DisplayColumn(line, 1, 4));
        Assert.Equal(3, TextMeasure.DisplayColumn(line, 2, 4));
        Assert.Equal(4, TextMeasure.DisplayColumn(line, 3, 4));
        Assert.Equal(5, TextMeasure.DisplayColumn(line, 4, 4));
    }

    [Fact]
    public void IndexAtDisplayColumn_InsideWideCharacter_ReturnsItsIndex()
    {
        const string line = "日本";

        Assert.Equal(0, TextMeasure.IndexAtDisplayColumn(line, 1, 4));
        Assert.Equal(1, TextMeasure.IndexAtDisplayColumn(line, 2, 4));
        Assert.Equal(2, TextMeasure.IndexAtDisplayColumn(line, 99, 4));
    }

    [Fact]
    public void NextAndPreviousIndex_SurrogatePair_MoveOverBothUnits()
    {
        const string line = "a😀b";

        Assert.Equal(3, TextMeasure.NextIndex(line, 1));
        Assert.Equal(1, TextMeasure.PreviousIndex(line, 3));
    }
}
