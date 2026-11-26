using System.Text;

namespace AsciiCanvas.Tests;

public class CharWidthTests
{
    [Theory]
    [InlineData('a', 1)]
    [InlineData('日', 2)]
    [InlineData('\u0301', 0)]
    [InlineData('\n', 0)]
    [InlineData('─', 1)]
    public void Of_Character_ReturnsColumns(char c, int expected) => Assert.Equal(expected, CharWidth.Of(c));

    [Fact]
    public void Of_Emoji_ReturnsTwoColumns() => Assert.Equal(2, CharWidth.Of(new Rune(0x1F600)));

    [Fact]
    public void Of_MixedText_SumsColumns() => Assert.Equal(5, CharWidth.Of("日本a".AsSpan()));
}
