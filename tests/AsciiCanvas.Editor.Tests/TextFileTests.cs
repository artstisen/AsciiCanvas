using System.Text;

namespace AsciiCanvas.Editor.Tests;

public sealed class TextFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "asciicanvas-" + Guid.NewGuid().ToString("N"));

    public TextFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task ReadAsync_Utf8WithCrlf_ReturnsTextAndCrlf()
    {
        var path = Path.Combine(_folder, "a.txt");
        await File.WriteAllTextAsync(path, "héllo\r\nwörld\r\n", TestContext.Current.CancellationToken);

        var result = await TextFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("héllo\r\nwörld\r\n", result.Text);
        Assert.Equal("\r\n", result.LineEnding);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ReadAsync_FileWithZeroBytes_IsRefusedAsBinary()
    {
        var path = Path.Combine(_folder, "picture.bin");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x01], TestContext.Current.CancellationToken);

        var result = await TextFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("binary", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_Utf16WithByteOrderMark_IsAcceptedAsText()
    {
        var path = Path.Combine(_folder, "utf16.txt");
        await File.WriteAllTextAsync(path, "one\ntwo", Encoding.Unicode, TestContext.Current.CancellationToken);

        var result = await TextFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("one\ntwo", result.Text);
        Assert.Equal("\n", result.LineEnding);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsAnErrorInsteadOfThrowing()
    {
        var result = await TextFile.ReadAsync(Path.Combine(_folder, "nope.txt"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("a\r\nb", "\r\n")]
    [InlineData("a\nb\n", "\n")]
    public void DetectLineEnding_TextWithLineBreaks_ReturnsTheOneUsed(string text, string expected) =>
        Assert.Equal(expected, TextFile.DetectLineEnding(text));

    [Fact]
    public void DetectLineEnding_TextWithoutLineBreaks_ReturnsThePlatformDefault() =>
        Assert.Equal(Environment.NewLine, TextFile.DetectLineEnding("one line"));
}
