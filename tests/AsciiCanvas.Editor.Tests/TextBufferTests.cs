namespace AsciiCanvas.Editor.Tests;

public class TextBufferTests
{
    [Fact]
    public void InsertThenDelete_MultiLineText_RestoresOriginal()
    {
        var buffer = new TextBuffer();
        buffer.SetText("ab\ncd");
        var start = new TextPosition(0, 1);

        var end = buffer.Insert(start, "X\nY\nZ");
        var afterInsert = buffer.Text;
        var removed = buffer.Delete(start, end);

        Assert.Equal("aX\nY\nZb\ncd", afterInsert);
        Assert.Equal(new TextPosition(2, 1), end);
        Assert.Equal("X\nY\nZ", removed);
        Assert.Equal("ab\ncd", buffer.Text);
    }

    [Fact]
    public void Advance_TextWithLineBreaks_ReturnsPositionAfterText()
    {
        Assert.Equal(new TextPosition(3, 4), TextBuffer.Advance(new TextPosition(3, 1), "abc"));
        Assert.Equal(new TextPosition(5, 2), TextBuffer.Advance(new TextPosition(3, 1), "a\nbc\nde"));
    }

    [Fact]
    public void SetText_MixedLineBreaks_NormalizesToSingleLineFeeds()
    {
        var buffer = new TextBuffer();

        buffer.SetText("a\r\nb\rc\nd");

        Assert.Equal(4, buffer.LineCount);
        Assert.Equal("a\nb\nc\nd", buffer.Text);
    }

    [Fact]
    public void Clamp_PositionOutsideText_MovesToNearestValidPosition()
    {
        var buffer = new TextBuffer();
        buffer.SetText("ab\nc");

        Assert.Equal(new TextPosition(1, 1), buffer.Clamp(new TextPosition(9, 9)));
        Assert.Equal(new TextPosition(0, 0), buffer.Clamp(new TextPosition(-1, -1)));
    }
}
