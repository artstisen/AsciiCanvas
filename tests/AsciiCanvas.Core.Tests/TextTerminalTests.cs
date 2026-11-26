namespace AsciiCanvas.Tests;

public class TextTerminalTests
{
    [Fact]
    public void Write_TextLongerThanScreen_WrapsAndScrollsAtBottom()
    {
        using var canvas = new CellCanvas(4, 2);
        var terminal = new TextTerminal(canvas);

        terminal.Write("abcdefghij");

        Assert.Equal("efgh", RowText(canvas, 0));
        Assert.Equal("ij  ", RowText(canvas, 1));
        Assert.Equal(2, terminal.CursorColumn);
        Assert.Equal(1, terminal.CursorRow);
    }

    [Fact]
    public void Write_ControlCharacters_MovesCursorAndHandlesTabAndCarriageReturn()
    {
        using var canvas = new CellCanvas(10, 3);
        var terminal = new TextTerminal(canvas);

        terminal.WriteLine("one");
        terminal.Write("a\tb\rX");

        Assert.Equal("one       ", RowText(canvas, 0));
        Assert.Equal("X       b ", RowText(canvas, 1));
    }

    [Fact]
    public void Out_WriteLineAndWriteChar_WritesToCanvas()
    {
        using var canvas = new CellCanvas(10, 2);
        var terminal = new TextTerminal(canvas);

        terminal.Out.WriteLine("hello");
        terminal.Out.Write('x');

        Assert.Equal("hello     ", RowText(canvas, 0));
        Assert.Equal("x         ", RowText(canvas, 1));
    }

    [Fact]
    public async Task ReadLineAsync_EditedInput_ReturnsEditedText()
    {
        using var canvas = new CellCanvas(20, 3);
        var terminal = new TextTerminal(canvas);
        Type(canvas, "helo");
        Press(canvas, Key.Left);
        Type(canvas, "l");
        Press(canvas, Key.End);
        Type(canvas, "!");
        Press(canvas, Key.Back);
        Press(canvas, Key.Enter);

        var line = await terminal.ReadLineAsync("> ", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello", line);
        Assert.StartsWith("> hello", RowText(canvas, 0), StringComparison.Ordinal);
        Assert.Equal(1, terminal.CursorRow);
        Assert.Equal(0, terminal.CursorColumn);
    }

    [Fact]
    public async Task ReadLineAsync_HistoryAndMask_RecallsHistoryAndMasksEcho()
    {
        using var canvas = new CellCanvas(20, 4);
        var terminal = new TextTerminal(canvas);
        var ct = TestContext.Current.CancellationToken;

        Type(canvas, "first");
        Press(canvas, Key.Enter);
        Assert.Equal("first", await terminal.ReadLineAsync(cancellationToken: ct));

        Press(canvas, Key.Up);
        Press(canvas, Key.Enter);
        Assert.Equal("first", await terminal.ReadLineAsync(cancellationToken: ct));

        Type(canvas, "secret");
        Press(canvas, Key.Enter);
        Assert.Equal("secret", await terminal.ReadLineAsync(mask: '*', cancellationToken: ct));
        Assert.StartsWith("******", RowText(canvas, 2), StringComparison.Ordinal);
        Assert.Equal(["first"], terminal.History);
    }

    [Fact]
    public async Task ReadLineAsync_LineWrapsAtBottom_ScrollsScreen()
    {
        using var canvas = new CellCanvas(5, 2);
        var terminal = new TextTerminal(canvas);
        terminal.WriteLine("top");
        Type(canvas, "abcdefgh");
        Press(canvas, Key.Enter);

        var line = await terminal.ReadLineAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("abcdefgh", line);

        // Enter moves to a new line, which scrolls the full screen once more.
        Assert.Equal("fgh  ", RowText(canvas, 0));
        Assert.Equal("     ", RowText(canvas, 1));
        Assert.Equal(1, terminal.CursorRow);
    }

    [Fact]
    public async Task ReadKeyAsync_MixedInput_SkipsPrintableKeyDownsAndReturnsTextAndActionKeys()
    {
        using var canvas = new CellCanvas(10, 2);
        var terminal = new TextTerminal(canvas);
        var ct = TestContext.Current.CancellationToken;
        Press(canvas, Key.A);
        Type(canvas, "a");
        Press(canvas, Key.Up);
        Press(canvas, Key.C, KeyModifiers.Control);

        var text = await terminal.ReadKeyAsync(ct);
        var up = await terminal.ReadKeyAsync(ct);
        var ctrlC = await terminal.ReadKeyAsync(ct);

        Assert.Equal("a", text.Text);
        Assert.Equal(Key.Up, up.Key);
        Assert.Equal((Key.C, KeyModifiers.Control), (ctrlC.Key, ctrlC.Modifiers));
    }

    [Fact]
    public async Task ReadKeyAsync_CanvasClosed_ThrowsOperationCanceledException()
    {
        using var canvas = new CellCanvas(10, 2);
        var terminal = new TextTerminal(canvas);
        canvas.Close();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await terminal.ReadKeyAsync(TestContext.Current.CancellationToken));
    }

    private static string RowText(CellCanvas canvas, int y)
    {
        var chars = new char[canvas.Columns];
        for (var x = 0; x < chars.Length; x++)
        {
            chars[x] = (char)canvas.GetCell(x, y).Rune.Value;
        }

        return new string(chars);
    }

    private static void Type(CellCanvas canvas, string text) => canvas.Input.Post(new TextEvent(text));

    private static void Press(CellCanvas canvas, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        canvas.Input.Post(new KeyEvent(key, modifiers));
}
