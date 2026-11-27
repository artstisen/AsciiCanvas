namespace AsciiCanvas.Tests;

public class TerminalRegionTests
{
    [Fact]
    public void WriteLine_WithABottomMargin_ScrollsOnlyTheRowsAbove_AndKeepsThemInTheScrollback()
    {
        using var canvas = new CellCanvas(5, 4) { ScrollbackLimit = 10 };
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        canvas.Write(0, 3, "input");

        output.WriteLine("one");
        output.WriteLine("two");
        output.WriteLine("three");
        output.WriteLine("four");

        Assert.Equal(["three", "four ", "     ", "input"], Rows(canvas));
        Assert.Equal(2, canvas.ScrollbackCount);
        Assert.Equal(2, output.CursorRow);
    }

    [Fact]
    public void Write_WithALeftMargin_StartsEachLineAfterIt_AlsoWhereALongLineWraps()
    {
        using var canvas = new CellCanvas(6, 3);
        var output = new TextTerminal(canvas) { MarginLeft = 2 };

        output.WriteLine("ab");
        output.Write("cdefgh");

        Assert.Equal(["  ab  ", "  cdef", "  gh  "], Rows(canvas));
        Assert.Equal(4, output.CursorColumn);
    }

    [Fact]
    public void Write_WithALeftMargin_ReturnsAndTabsFromTheMargin()
    {
        using var canvas = new CellCanvas(12, 1);
        var output = new TextTerminal(canvas) { MarginLeft = 1 };

        // (\r goes back to the margin; the tab stop is 8 columns from it)
        output.Write("abc\rX\tY");

        Assert.Equal([" X       Y  "], Rows(canvas));
    }

    [Fact]
    public void Clear_WithALeftMargin_PutsTheCursorAfterIt_AndTheScrollbackKeepsTheGutterBlank()
    {
        using var canvas = new CellCanvas(4, 2) { ScrollbackLimit = 10 };
        var output = new TextTerminal(canvas) { MarginLeft = 1 };

        output.WriteLine("a");
        output.WriteLine("b");
        output.WriteLine("c");
        Assert.Equal(2, canvas.ScrollbackCount);
        output.Clear();

        Assert.Equal((1, 0), (output.CursorColumn, output.CursorRow));
    }

    [Fact]
    public void MarginLeft_WiderThanTheCanvas_LeavesOneColumn()
    {
        using var canvas = new CellCanvas(3, 2);
        var output = new TextTerminal(canvas) { MarginLeft = 10 };

        output.Write("xy");

        Assert.Equal(["  x", "  y"], Rows(canvas));
    }

    [Fact]
    public void WriteLine_WithATopMarginBeyondTheCanvas_StaysOnTheLastRow_AndLeavesTheRowsAbove()
    {
        using var canvas = new CellCanvas(5, 3) { ScrollbackLimit = 10 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        canvas.Write(0, 0, "out");

        input.Write("ls");
        Assert.Equal(["out  ", "     ", "ls   "], Rows(canvas));

        input.WriteLine();

        Assert.Equal(["out  ", "     ", "     "], Rows(canvas));
        Assert.Equal(2, input.CursorRow);
        Assert.Equal(0, canvas.ScrollbackCount);
    }

    [Fact]
    public void Clear_WithMargins_ClearsOnlyTheRowsBetweenThem()
    {
        using var canvas = new CellCanvas(3, 3);
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        output.WriteLine("aaa");
        canvas.Write(0, 2, "bot");

        output.Clear();

        Assert.Equal(["   ", "   ", "bot"], Rows(canvas));
        Assert.Equal((0, 0), (output.CursorColumn, output.CursorRow));
    }

    [Fact]
    public void Pen_GivesATerminalItsOwnStyle_WithoutChangingTheCanvasPen()
    {
        using var canvas = new CellCanvas(5, 2);
        var green = new CellStyle(CellColor.BrightGreen);
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue, Pen = green };

        output.Write("out");
        input.Write("in");
        input.Style = input.Style with { Attributes = CellAttributes.Bold };

        Assert.Equal(CellColor.Default, canvas.GetCell(0, 0).Style.Foreground);
        Assert.Equal(CellColor.BrightGreen, canvas.GetCell(0, 1).Style.Foreground);
        Assert.Equal(CellAttributes.Bold, input.Pen?.Attributes);
        Assert.Equal(CellAttributes.None, canvas.Pen.Attributes);
    }

    [Fact]
    public async Task ReadKeyAsync_AfterTheCanvasIsResized_RaisesResized()
    {
        using var canvas = new CellCanvas(5, 3);
        var terminal = new TextTerminal(canvas);
        var resized = 0;
        terminal.Resized += () => resized++;
        canvas.Resize(6, 4);
        canvas.Input.Post(new TextEvent("a"));

        var key = await terminal.ReadKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("a", key.Text);
        Assert.Equal(1, resized);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenCloseIsRequested_RaisesCloseRequested_AndReadsOn()
    {
        using var canvas = new CellCanvas(5, 3);
        var terminal = new TextTerminal(canvas);
        var requested = 0;
        terminal.CloseRequested += () => requested++;
        canvas.Input.Post(new CloseRequestedEvent());
        canvas.Input.Post(new TextEvent("a"));

        var key = await terminal.ReadKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("a", key.Text);
        Assert.Equal(1, requested);
    }

    [Fact]
    public void KeyAvailable_WhenCloseIsRequested_RaisesCloseRequested()
    {
        using var canvas = new CellCanvas(5, 3);
        var terminal = new TextTerminal(canvas);
        var requested = 0;
        terminal.CloseRequested += () => requested++;
        canvas.Input.Post(new CloseRequestedEvent());

        Assert.False(terminal.KeyAvailable);
        Assert.Equal(1, requested);
    }

    [Fact]
    public void Write_AfterACanvasMadeShorterMovedTheContentUp_ContinuesOnTheSameLine()
    {
        using var canvas = new CellCanvas(5, 6) { ScrollbackLimit = 10 };
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        output.WriteLine("one");
        output.Write("two");
        input.Write(">");

        canvas.Resize(5, 5);
        output.Write("!");

        Assert.Equal(["two! ", "     ", "     ", "     ", ">    "], Rows(canvas));
        Assert.Equal(1, canvas.ScrollbackCount);
    }

    [Fact]
    public void CopyFrame_ScrolledBackWithAPinnedRow_KeepsThatRowAndTheCursorInIt()
    {
        using var canvas = new CellCanvas(3, 3) { ScrollbackLimit = 10, ScrollbackPinnedRows = 1 };
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        output.WriteLine("aaa");
        output.WriteLine("bbb");
        output.WriteLine("ccc");
        input.Write("> ");

        canvas.ScrollbackOffset = 2;
        var grid = new CellGrid(1, 1);
        var frame = canvas.CopyFrame(grid);

        Assert.Equal(["aaa", "bbb", ">"], ViewRows(grid));
        Assert.Equal((2, 2, true), (frame.CursorColumn, frame.CursorRow, frame.CursorVisible));
    }

    [Fact]
    public void Resize_Smaller_WithPinnedRows_TakesTheBlankRowsAboveThemFirst()
    {
        using var canvas = new CellCanvas(3, 7) { ScrollbackLimit = 10, ScrollbackPinnedRows = 2 };
        var output = new TextTerminal(canvas) { MarginBottom = 2 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        output.WriteLine("aaa");
        output.WriteLine("bbb");
        canvas.Write(0, 5, "---");
        input.Write(">");

        canvas.Resize(3, 5);

        Assert.Equal(["aaa", "bbb", "   ", "---", ">  "], Rows(canvas));
        Assert.Equal(0, canvas.ScrollbackCount);
        Assert.Equal(4, canvas.CursorRow);
    }

    [Fact]
    public void Resize_Smaller_WithPinnedRows_ThenMovesTheTopRowsIntoTheScrollback_LeavingARowForTheNextLine()
    {
        using var canvas = new CellCanvas(3, 6) { ScrollbackLimit = 10, ScrollbackPinnedRows = 1 };
        var output = new TextTerminal(canvas) { MarginBottom = 1 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        output.WriteLine("aaa");
        output.WriteLine("bbb");
        output.WriteLine("ccc");
        input.Write(">");

        canvas.Resize(3, 3);
        output.Write("d");

        Assert.Equal(["ccc", "d  ", ">  "], Rows(canvas));
        Assert.Equal(2, canvas.ScrollbackCount);
    }

    [Fact]
    public void Resize_Larger_WithPinnedRows_MovesThemToTheNewBottom()
    {
        using var canvas = new CellCanvas(3, 4) { ScrollbackPinnedRows = 2 };
        var input = new TextTerminal(canvas) { MarginTop = int.MaxValue };
        canvas.Write(0, 0, "aaa");
        canvas.Write(0, 2, "---");
        input.Write(">");

        canvas.Resize(3, 6);
        input.Write("x");

        Assert.Equal(["aaa", "   ", "   ", "   ", "---", ">x "], Rows(canvas));
        Assert.Equal(5, canvas.CursorRow);
    }

    private static string[] Rows(CellCanvas canvas)
    {
        canvas.Present();
        var rows = new string[canvas.Rows];
        for (var y = 0; y < canvas.Rows; y++)
        {
            var chars = new char[canvas.Columns];
            for (var x = 0; x < chars.Length; x++)
            {
                var rune = canvas.GetCell(x, y).Rune.Value;
                chars[x] = rune == 0 ? ' ' : (char)rune;
            }

            rows[y] = new string(chars);
        }

        return rows;
    }

    private static string[] ViewRows(CellGrid grid)
    {
        var rows = new string[grid.Rows];
        for (var y = 0; y < grid.Rows; y++)
        {
            var chars = new char[grid.Columns];
            for (var x = 0; x < chars.Length; x++)
            {
                var rune = grid[x, y].Rune.Value;
                chars[x] = rune == 0 ? ' ' : (char)rune;
            }

            rows[y] = new string(chars).TrimEnd();
        }

        return rows;
    }
}
