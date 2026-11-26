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
    public void WriteLine_TerminalOutputPastTheBottom_CanBeScrolledBackTo()
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
        AddRows(canvas, "r1", "r2", "r3", "r4");

        canvas.ScrollbackOffset = int.MaxValue;

        Assert.Equal(2, canvas.ScrollbackOffset);
        Assert.Equal(["r3"], View(canvas));
    }

    [Fact]
    public void ScrollbackLimit_Lowered_KeepsTheNewestRows()
    {
        using var canvas = WithScrollback(2, 1, 10);
        AddRows(canvas, "r1", "r2", "r3");

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
        AddRows(canvas, "ab");
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
    public void Scroll_ManyRowsWithoutPresent_KeepsOnlyTheNewestRowsUpToTheLimit()
    {
        using var canvas = WithScrollback(2, 1, 3);
        for (var i = 0; i < 10; i++)
        {
            canvas.Write(0, 0, $"r{i}");
            canvas.Scroll(canvas.Bounds, 0, -1);
        }

        canvas.Present();
        canvas.ScrollBack(int.MaxValue);

        Assert.Equal((3, 3), (canvas.ScrollbackCount, canvas.ScrollbackOffset));
        Assert.Equal(["r7"], View(canvas));
    }

    [Fact]
    public void Present_ScrolledBackWhileTheFullScrollbackDropsRows_KeepsTheViewOnTheSameContent()
    {
        using var canvas = WithScrollback(2, 1, 3);
        AddRows(canvas, "r1", "r2", "r3");
        canvas.ScrollBack(2);
        Assert.Equal(["r2"], View(canvas));

        AddRows(canvas, "r4");

        Assert.Equal(3, canvas.ScrollbackOffset);
        Assert.Equal(["r2"], View(canvas));
    }

    [Fact]
    public void Present_ScrolledBackRowDropped_ShowsTheOldestRowKept()
    {
        using var canvas = WithScrollback(2, 1, 2);
        AddRows(canvas, "r1", "r2");
        canvas.ScrollBack(2);

        AddRows(canvas, "r3", "r4");

        Assert.Equal(2, canvas.ScrollbackOffset);
        Assert.Equal(["r3"], View(canvas));
    }

    [Theory]
    [InlineData(0, 0, 3, 2, 1)] // a clip covering the whole width keeps the row
    [InlineData(0, 0, 2, 2, 0)] // a narrower clip makes the scrolled area narrower than the canvas
    public void Scroll_InsideAClip_UsesTheClippedArea(int x, int y, int width, int height, int expectedRows)
    {
        using var canvas = WithScrollback(3, 2, 10);
        Lines(canvas, "aaa", "bbb");

        using (canvas.PushClip(new CellRect(x, y, width, height)))
        {
            canvas.Scroll(canvas.Bounds, 0, -1);
        }

        canvas.Present();

        Assert.Equal(expectedRows, canvas.ScrollbackCount);
    }

    [Fact]
    public void CopyFrame_CanvasGrewTallerWhileScrolledBack_ShowsScrollbackAboveTheTallerCanvas()
    {
        using var canvas = WithScrollback(2, 1, 10);
        AddRows(canvas, "r1", "r2");
        canvas.Write(0, 0, "nw");
        canvas.Present();
        canvas.ScrollBack(1);

        canvas.Resize(2, 3);

        Assert.Equal(["r2", "nw", "  "], View(canvas));
    }

    [Fact]
    public void ScrollbackLimit_LoweredWhileRowsArePending_PublishesOnlyTheNewestRows()
    {
        using var canvas = WithScrollback(2, 1, 10);
        foreach (var text in new[] { "r1", "r2", "r3" })
        {
            canvas.Write(0, 0, text);
            canvas.Scroll(canvas.Bounds, 0, -1);
        }

        canvas.ScrollbackLimit = 1;
        canvas.Present();
        canvas.ScrollBack(1);

        Assert.Equal(1, canvas.ScrollbackCount);
        Assert.Equal(["r3"], View(canvas));
    }

    [Fact]
    public void ScrollBack_ByRows_MovesRelativeToTheViewAndClamps()
    {
        using var canvas = WithScrollback(2, 1, 10);
        AddRows(canvas, "r1", "r2", "r3");

        canvas.ScrollBack(2);
        var back = canvas.ScrollbackOffset;
        canvas.ScrollBack(-1);
        var forward = canvas.ScrollbackOffset;
        canvas.ScrollBack(int.MinValue);
        var bottom = canvas.ScrollbackOffset;
        canvas.ScrollBack(int.MaxValue);

        Assert.Equal((2, 1, 0, 3), (back, forward, bottom, canvas.ScrollbackOffset));
    }

    [Fact]
    public void ScrollbackLimit_Huge_DoesNotReserveMemoryUpFront()
    {
        using var canvas = WithScrollback(2, 1, int.MaxValue);

        AddRows(canvas, "r1");

        Assert.Equal(1, canvas.ScrollbackCount);
    }

    [Fact]
    public void ScrollbackLimit_Negative_Throws()
    {
        using var canvas = new CellCanvas();

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.ScrollbackLimit = -1);
    }

    [Fact]
    public void Resize_ShorterThanTheCursorRow_KeepingTheCursorRow_MovesTheTopRowsIntoTheScrollback()
    {
        using var canvas = WithScrollback(3, 4, 10);
        canvas.KeepCursorRowOnShrink = true;
        Lines(canvas, "aaa", "bbb", "ccc", "dd>");
        canvas.SetCursor(2, 3);

        canvas.Resize(3, 2);

        Assert.Equal(["ccc", "dd>"], View(canvas));
        Assert.Equal(1, canvas.CursorRow);
        Assert.Equal(2, canvas.ScrollbackCount);
        canvas.ScrollbackOffset = 2;
        Assert.Equal(["aaa", "bbb"], View(canvas));
    }

    [Fact]
    public void Resize_ShorterButTheCursorRowStillFits_KeepingTheCursorRow_KeepsTheContentAtTheTop()
    {
        using var canvas = WithScrollback(3, 4, 10);
        canvas.KeepCursorRowOnShrink = true;
        Lines(canvas, "aaa", "bb>");
        canvas.SetCursor(2, 1);

        canvas.Resize(3, 2);

        Assert.Equal(["aaa", "bb>"], View(canvas));
        Assert.Equal(0, canvas.ScrollbackCount);
    }

    [Fact]
    public void Resize_ShorterThanTheCursorRow_WithoutKeepingTheCursorRow_CutsTheBottomRows()
    {
        using var canvas = WithScrollback(3, 4, 10);
        Lines(canvas, "aaa", "bbb", "ccc", "dd>");
        canvas.SetCursor(2, 3);

        canvas.Resize(3, 2);

        Assert.Equal(["aaa", "bbb"], View(canvas));
        Assert.Equal(0, canvas.ScrollbackCount);
    }

    private static CellCanvas WithScrollback(int columns, int rows, int limit)
    {
        var canvas = new CellCanvas(columns, rows);
        canvas.ScrollbackLimit = limit;
        return canvas;
    }

    // On a one-row canvas: writes each text and scrolls it into the scrollback, presenting each time.
    private static void AddRows(CellCanvas canvas, params string[] texts)
    {
        foreach (var text in texts)
        {
            canvas.Write(0, 0, text);
            canvas.Scroll(canvas.Bounds, 0, -1);
            canvas.Present();
        }
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
