namespace AsciiCanvas.Tests;

public class ScrollbackTests
{
    [Fact]
    public void Scroll_WithScrollbackOff_KeepsNothing()
    {
        using var canvas = new CellCanvas(3, 2);
        Lines(canvas, "aaa", "bbb");

        canvas.Scroll(canvas.Bounds, 0, -1);
        canvas.Present();

        Assert.Equal(0, canvas.ScrollbackCount);
    }

    [Fact]
    public void Present_AfterFullWidthScrollUp_KeepsTheRowsThatLeftTheTop()
    {
        using var canvas = WithScrollback(3, 2, 10);
        Lines(canvas, "aaa", "bbb");

        canvas.Scroll(canvas.Bounds, 0, -1);
        Assert.Equal(0, canvas.ScrollbackCount);
        canvas.Present();

        Assert.Equal(1, canvas.ScrollbackCount);
        canvas.ScrollbackOffset = 1;
        Assert.Equal(["aaa", "bbb"], View(canvas));
    }

    [Theory]
    [InlineData(0, 1, 3, 1)] // starts below row 0
    [InlineData(1, 0, 2, 2)] // not full width
    public void Scroll_RegionThatIsNotTheTopOfTheScreen_KeepsNothing(int x, int y, int width, int height)
    {
        using var canvas = WithScrollback(3, 2, 10);
        Lines(canvas, "aaa", "bbb");

        canvas.Scroll(new CellRect(x, y, width, height), 0, -1);
        canvas.Present();

        Assert.Equal(0, canvas.ScrollbackCount);
    }

    [Fact]
    public void Scroll_TopRegionAboveAStatusLine_KeepsTheTopRow()
    {
        using var canvas = WithScrollback(3, 3, 10);
        Lines(canvas, "aaa", "bbb", "st:");

        canvas.Scroll(new CellRect(0, 0, 3, 2), 0, -1);
        canvas.Present();
        canvas.ScrollbackOffset = 1;

        Assert.Equal(["aaa", "bbb", "   "], View(canvas));
    }

    [Fact]
    public void TextTerminal_OutputPastTheBottom_CanBeScrolledBackTo()
    {
        using var canvas = WithScrollback(4, 2, 100);
        var terminal = new TextTerminal(canvas);

        for (var i = 1; i <= 5; i++)
        {
            terminal.WriteLine($"L{i}");
        }

        Assert.Equal(4, canvas.ScrollbackCount);
        canvas.ScrollbackOffset = 4;
        Assert.Equal(["L1  ", "L2  "], View(canvas));
        canvas.ScrollbackOffset = 1;
        Assert.Equal(["L4  ", "L5  "], View(canvas));
    }

    [Fact]
    public void ScrollbackLimit_Reached_DropsTheOldestRows()
    {
        using var canvas = WithScrollback(2, 1, 2);
        foreach (var text in new[] { "r1", "r2", "r3", "r4" })
        {
            canvas.Write(0, 0, text);
            canvas.Scroll(canvas.Bounds, 0, -1);
            canvas.Present();
        }

        canvas.ScrollbackOffset = int.MaxValue;

        Assert.Equal(2, canvas.ScrollbackOffset);
        Assert.Equal(["r3"], View(canvas));
    }

    [Fact]
    public void ScrollbackLimit_Lowered_KeepsTheNewestRows()
    {
        using var canvas = WithScrollback(2, 1, 10);
        foreach (var text in new[] { "r1", "r2", "r3" })
        {
            canvas.Write(0, 0, text);
            canvas.Scroll(canvas.Bounds, 0, -1);
            canvas.Present();
        }

        canvas.ScrollbackLimit = 1;
        canvas.ScrollbackOffset = 1;

        Assert.Equal(1, canvas.ScrollbackCount);
        Assert.Equal(["r3"], View(canvas));
    }

    [Fact]
    public void Present_WhileScrolledBack_KeepsTheViewOnTheSameContent()
    {
        using var canvas = WithScrollback(3, 2, 10);
        var terminal = new TextTerminal(canvas);
        terminal.WriteLine("one");
        terminal.WriteLine("two");
        terminal.WriteLine("thr");
        canvas.ScrollbackOffset = 2;
        var before = View(canvas);

        terminal.WriteLine("fou");

        Assert.Equal(3, canvas.ScrollbackOffset);
        Assert.Equal(before, View(canvas));
    }

    [Fact]
    public void CopyFrame_ScrolledBack_MovesTheCursorDownOrHidesIt()
    {
        using var canvas = WithScrollback(3, 3, 10);
        var terminal = new TextTerminal(canvas);
        terminal.Write("a\nb\nc\nd\ne");
        terminal.SetCursorPosition(0, 1);
        var grid = new CellGrid(1, 1);

        canvas.ScrollbackOffset = 1;
        var moved = canvas.CopyFrame(grid);
        canvas.ScrollbackOffset = 2;
        var hidden = canvas.CopyFrame(grid);

        Assert.Equal((2, true), (moved.CursorRow, moved.CursorVisible));
        Assert.False(hidden.CursorVisible);
    }

    [Fact]
    public void ScrollbackOffset_Changed_RaisesPresentedOnlyWhenTheViewMoves()
    {
        using var canvas = WithScrollback(2, 1, 10);
        canvas.Scroll(canvas.Bounds, 0, -1);
        canvas.Present();
        var raised = 0;
        canvas.Presented += () => raised++;

        canvas.ScrollbackOffset = 1;
        canvas.ScrollbackOffset = 5;
        canvas.ScrollbackOffset = 0;

        Assert.Equal(2, raised);
    }

    [Fact]
    public void ClearScrollback_ScrolledBack_EmptiesItAndShowsTheCanvas()
    {
        using var canvas = WithScrollback(2, 1, 10);
        canvas.Write(0, 0, "ab");
        canvas.Scroll(canvas.Bounds, 0, -1);
        canvas.Present();
        canvas.ScrollbackOffset = 1;

        canvas.ClearScrollback();

        Assert.Equal((0, 0), (canvas.ScrollbackCount, canvas.ScrollbackOffset));
        Assert.Equal(["  "], View(canvas));
    }

    [Fact]
    public void CopyFrame_RowFromAWiderCanvas_IsCutWithoutHalfAWideCharacter()
    {
        using var canvas = WithScrollback(4, 1, 10);
        canvas.Write(0, 0, "a界b");
        canvas.Scroll(canvas.Bounds, 0, -1);
        canvas.Present();
        canvas.Resize(2, 1);

        canvas.ScrollbackOffset = 1;

        Assert.Equal(["a "], View(canvas));
    }

    [Fact]
    public void ScrollbackLimit_Negative_Throws()
    {
        using var canvas = new CellCanvas();

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.ScrollbackLimit = -1);
    }

    private static CellCanvas WithScrollback(int columns, int rows, int limit)
    {
        var canvas = new CellCanvas(columns, rows);
        canvas.ScrollbackLimit = limit;
        return canvas;
    }

    private static void Lines(CellCanvas canvas, params string[] lines)
    {
        for (var y = 0; y < lines.Length; y++)
        {
            canvas.Write(0, y, lines[y]);
        }

        canvas.Present();
    }

    private static string[] View(CellCanvas canvas)
    {
        var grid = new CellGrid(1, 1);
        canvas.CopyFrame(grid);
        var rows = new string[grid.Rows];
        for (var y = 0; y < grid.Rows; y++)
        {
            var chars = new char[grid.Columns];
            for (var x = 0; x < chars.Length; x++)
            {
                var rune = grid[x, y].Rune.Value;
                chars[x] = rune == 0 ? ' ' : (char)rune;
            }

            rows[y] = new string(chars);
        }

        return rows;
    }
}
