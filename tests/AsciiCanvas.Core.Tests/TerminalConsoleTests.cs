namespace AsciiCanvas.Core.Tests;

// The shim has static state, so these tests share one attached terminal and run one after the other (the tests of a class do).
public sealed class TerminalConsoleTests : IDisposable
{
    private readonly CellCanvas _canvas = new(20, 5);
    private readonly TextTerminal _terminal;

    public TerminalConsoleTests()
    {
        _terminal = new TextTerminal(_canvas);
        TerminalConsole.Attach(_terminal);
    }

    public void Dispose()
    {
        TerminalConsole.Detach(_terminal);
        _canvas.Dispose();
    }

    [Fact]
    public void Write_WithFormatArguments_WritesAtTheCursor()
    {
        TerminalConsole.Write("a{0}", 5);
        TerminalConsole.WriteLine("b");
        TerminalConsole.Write(15L);

        Assert.Equal("a5b", RowText(0));
        Assert.Equal("15", RowText(1));
    }

    [Fact]
    public void WriteLine_WithALineLongerThanTheWindow_Wraps()
    {
        TerminalConsole.WriteLine(new string('x', 25));

        Assert.Equal(new string('x', 20), RowText(0));
        Assert.Equal(new string('x', 5), RowText(1));
        Assert.Equal(2, TerminalConsole.CursorTop);
    }

    [Theory]
    [InlineData(ConsoleColor.Black, 0)]
    [InlineData(ConsoleColor.DarkBlue, 4)]
    [InlineData(ConsoleColor.DarkRed, 1)]
    [InlineData(ConsoleColor.DarkYellow, 3)]
    [InlineData(ConsoleColor.Gray, 7)]
    [InlineData(ConsoleColor.DarkGray, 8)]
    [InlineData(ConsoleColor.Blue, 12)]
    [InlineData(ConsoleColor.Red, 9)]
    [InlineData(ConsoleColor.Yellow, 11)]
    [InlineData(ConsoleColor.White, 15)]
    public void ForegroundColor_WhenSet_ColorsTheTextAndReadsBack(ConsoleColor color, int expectedIndex)
    {
        TerminalConsole.ForegroundColor = color;
        TerminalConsole.Write("x");

        Assert.Equal(expectedIndex, _canvas.GetCell(0, 0).Style.Foreground.Index);
        Assert.Equal(color, TerminalConsole.ForegroundColor);
    }

    [Fact]
    public void BackgroundColor_WhenSetAndReset_ColorsTheTextUntilReset()
    {
        TerminalConsole.BackgroundColor = ConsoleColor.DarkGreen;
        TerminalConsole.ForegroundColor = ConsoleColor.White;
        TerminalConsole.Write("a");
        TerminalConsole.ResetColor();
        TerminalConsole.Write("b");

        Assert.Equal(CellColor.Green, _canvas.GetCell(0, 0).Style.Background);
        Assert.True(_canvas.GetCell(1, 0).Style.Background.IsDefault);
        Assert.Equal(ConsoleColor.Gray, TerminalConsole.ForegroundColor);
        Assert.Equal(ConsoleColor.Black, TerminalConsole.BackgroundColor);
    }

    [Fact]
    public void ForegroundColor_SetToAnUndefinedColor_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TerminalConsole.ForegroundColor = (ConsoleColor)16);
    }

    [Fact]
    public void SetCursorPosition_WithinAndOutsideTheWindow_MovesAndClamps()
    {
        TerminalConsole.SetCursorPosition(5, 2);
        TerminalConsole.Write("z");
        var inside = TerminalConsole.GetCursorPosition();

        TerminalConsole.SetCursorPosition(99, 99);

        Assert.Equal('z', _canvas.GetCell(5, 2).Rune.Value);
        Assert.Equal((6, 2), inside);
        Assert.Equal((19, 4), TerminalConsole.GetCursorPosition());
    }

    [Fact]
    public void WindowSize_ReportsTheCanvas()
    {
        Assert.Equal(20, TerminalConsole.WindowWidth);
        Assert.Equal(5, TerminalConsole.WindowHeight);
        Assert.Equal(20, TerminalConsole.BufferWidth);

        TerminalConsole.WindowWidth = 30;

        Assert.Equal(30, _canvas.Columns);
    }

    [Fact]
    public void ReadLine_AfterTypingAndEnter_ReturnsTheLine()
    {
        _canvas.Input.Post(new TextEvent("hi"));
        _canvas.Input.Post(new KeyEvent(Key.Enter));
        TerminalConsole.Write("> ");

        var line = TerminalConsole.ReadLine();

        Assert.Equal("hi", line);
        Assert.Equal("> hi", RowText(0));
        Assert.Equal(1, TerminalConsole.CursorTop);
    }

    [Fact]
    public void In_ReadThenReadLine_ReturnsTheRestOfTheLine()
    {
        _canvas.Input.Post(new TextEvent("abc"));
        _canvas.Input.Post(new KeyEvent(Key.Enter));

        var first = TerminalConsole.In.Read();
        var peeked = TerminalConsole.In.Peek();
        var rest = TerminalConsole.In.ReadLine();

        Assert.Equal('a', first);
        Assert.Equal('b', peeked);
        Assert.Equal("bc", rest);
    }

    [Fact]
    public void ReadKey_ForTypedText_ReturnsOneCharacterAtATimeAndEchoesIt()
    {
        _canvas.Input.Post(new TextEvent("Ab"));

        var first = TerminalConsole.ReadKey();
        var second = TerminalConsole.ReadKey(intercept: true);

        Assert.Equal('A', first.KeyChar);
        Assert.Equal(ConsoleKey.A, first.Key);
        Assert.True((first.Modifiers & ConsoleModifiers.Shift) != 0);
        Assert.Equal('b', second.KeyChar);
        Assert.Equal(ConsoleKey.B, second.Key);
        Assert.Equal("A", RowText(0));
    }

    [Fact]
    public void ReadKey_ForMultiLinePaste_ReturnsEnterForEachLineBreak()
    {
        _canvas.Input.Post(new PasteEvent("a\nb"));

        var keys = new[] { TerminalConsole.ReadKey(true), TerminalConsole.ReadKey(true), TerminalConsole.ReadKey(true) };

        Assert.Equal([ConsoleKey.A, ConsoleKey.Enter, ConsoleKey.B], keys.Select(k => k.Key));
        Assert.Equal('\r', keys[1].KeyChar);
        Assert.False(TerminalConsole.KeyAvailable);
    }

    [Theory]
    [InlineData(Key.Up, ConsoleKey.UpArrow, '\0')]
    [InlineData(Key.Enter, ConsoleKey.Enter, '\r')]
    [InlineData(Key.Escape, ConsoleKey.Escape, '\u001b')]
    [InlineData(Key.Back, ConsoleKey.Backspace, '\b')]
    [InlineData(Key.F5, ConsoleKey.F5, '\0')]
    [InlineData(Key.PageDown, ConsoleKey.PageDown, '\0')]
    public void ReadKey_ForActionKeys_ReturnsTheMatchingConsoleKey(Key key, ConsoleKey expected, char expectedChar)
    {
        _canvas.Input.Post(new KeyEvent(key));

        var info = TerminalConsole.ReadKey(intercept: true);

        Assert.Equal(expected, info.Key);
        Assert.Equal(expectedChar, info.KeyChar);
    }

    [Fact]
    public void ReadKey_ForCtrlLetter_ReturnsTheControlCharacter()
    {
        _canvas.Input.Post(new KeyEvent(Key.C, KeyModifiers.Control));

        var info = TerminalConsole.ReadKey(intercept: true);

        Assert.Equal(ConsoleKey.C, info.Key);
        Assert.Equal('\u0003', info.KeyChar);
        Assert.True((info.Modifiers & ConsoleModifiers.Control) != 0);
    }

    [Fact]
    public void KeyAvailable_WhenAKeyIsWaiting_IsTrueAndDoesNotLoseTheKey()
    {
        var before = TerminalConsole.KeyAvailable;
        _canvas.Input.Post(new KeyEvent(Key.Left));

        var during = TerminalConsole.KeyAvailable;
        var info = TerminalConsole.ReadKey(intercept: true);
        var after = TerminalConsole.KeyAvailable;

        Assert.False(before);
        Assert.True(during);
        Assert.Equal(ConsoleKey.LeftArrow, info.Key);
        Assert.False(after);
    }

    [Fact]
    public void Title_WhenSet_ChangesTheCanvasTitle()
    {
        TerminalConsole.Title = "Ported";

        Assert.Equal("Ported", _canvas.Title);
    }

    [Fact]
    public void Terminal_WhenNothingIsAttached_Throws()
    {
        TerminalConsole.Detach(_terminal);

        Assert.Throws<InvalidOperationException>(() => TerminalConsole.WindowWidth);
    }

    private string RowText(int row)
    {
        var text = new System.Text.StringBuilder();
        for (var x = 0; x < _canvas.Columns; x++)
        {
            text.Append(_canvas.GetCell(x, row).Rune.ToString());
        }

        return text.ToString().TrimEnd();
    }
}
