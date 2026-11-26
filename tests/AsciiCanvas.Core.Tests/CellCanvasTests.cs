using System.Text;

namespace AsciiCanvas.Tests;

public class CellCanvasTests
{
    [Fact]
    public void Write_TextInsideCanvas_DrawsTextAndReturnsEndColumn()
    {
        using var canvas = new CellCanvas(10, 2);

        var end = canvas.Write(2, 1, "hi");

        Assert.Equal(4, end);
        Assert.Equal("  hi      ", RowText(canvas, 1));
    }

    [Fact]
    public void Write_TextRunningPastEdge_ClipsAtCanvasEdge()
    {
        using var canvas = new CellCanvas(5, 1);

        var end = canvas.Write(3, 0, "abcdef");

        Assert.Equal(9, end);
        Assert.Equal("   ab", RowText(canvas, 0));
    }

    [Fact]
    public void Write_WideCharacter_TakesTwoCells()
    {
        using var canvas = new CellCanvas(6, 1);

        var end = canvas.Write(0, 0, "日a");

        Assert.Equal(3, end);
        Assert.Equal(CellWidth.WideLead, canvas.GetCell(0, 0).Width);
        Assert.Equal(CellWidth.WideTrail, canvas.GetCell(1, 0).Width);
        Assert.Equal('a', (char)canvas.GetCell(2, 0).Rune.Value);
    }

    [Fact]
    public void SetCell_OverHalfOfWideCharacter_BlanksTheOtherHalf()
    {
        using var canvas = new CellCanvas(6, 1);
        canvas.Write(0, 0, "日");

        canvas.SetCell(1, 0, 'x');

        Assert.Equal(CellWidth.Narrow, canvas.GetCell(0, 0).Width);
        Assert.Equal(' ', (char)canvas.GetCell(0, 0).Rune.Value);
        Assert.Equal('x', (char)canvas.GetCell(1, 0).Rune.Value);
    }

    [Fact]
    public void Write_WideCharacterInLastColumn_IsNotDrawn()
    {
        using var canvas = new CellCanvas(3, 1);

        canvas.Write(2, 0, "日");

        Assert.Equal(' ', (char)canvas.GetCell(2, 0).Rune.Value);
        Assert.Equal(CellWidth.Narrow, canvas.GetCell(2, 0).Width);
    }

    [Fact]
    public void PushClip_WhileActive_RestrictsDrawingAndRestoresOnDispose()
    {
        using var canvas = new CellCanvas(10, 3);

        using (canvas.PushClip(new CellRect(2, 1, 3, 1)))
        {
            canvas.Fill(canvas.Bounds, '#');
        }

        canvas.SetCell(0, 0, 'z');

        Assert.Equal("z         ", RowText(canvas, 0));
        Assert.Equal("  ###     ", RowText(canvas, 1));
    }

    [Fact]
    public void Box_WithTitle_DrawsFrameWithTitle()
    {
        using var canvas = new CellCanvas(10, 3);

        canvas.Box(new CellRect(0, 0, 10, 3), BoxStyle.SingleLine, title: "Hi");

        Assert.Equal("┌ Hi ────┐", RowText(canvas, 0));
        Assert.Equal("│        │", RowText(canvas, 1));
        Assert.Equal("└────────┘", RowText(canvas, 2));
    }

    [Fact]
    public void Scroll_UpByOneRow_MovesContentAndClearsVacatedRow()
    {
        using var canvas = new CellCanvas(3, 3);
        canvas.Write(0, 0, "aaa");
        canvas.Write(0, 1, "bbb");
        canvas.Write(0, 2, "ccc");

        canvas.Scroll(canvas.Bounds, 0, -1);

        Assert.Equal("bbb", RowText(canvas, 0));
        Assert.Equal("ccc", RowText(canvas, 1));
        Assert.Equal("   ", RowText(canvas, 2));
    }

    [Fact]
    public void Blit_CapturedSpriteWithSkipBlanks_KeepsCanvasUnderBlankCells()
    {
        using var canvas = new CellCanvas(6, 2);
        canvas.Write(0, 0, "a b");
        var sprite = canvas.Capture(new CellRect(0, 0, 3, 1));
        canvas.Fill(canvas.Bounds, '.');

        canvas.Blit(sprite, 2, 1, skipBlanks: true);

        Assert.Equal("......", RowText(canvas, 0));
        Assert.Equal("..a.b.", RowText(canvas, 1));
    }

    [Fact]
    public void Resize_SmallerCanvas_KeepsContentAndPostsResizeEvent()
    {
        using var canvas = new CellCanvas(4, 2);
        canvas.Write(0, 0, "abcd");

        canvas.Resize(2, 3);

        Assert.Equal(2, canvas.Columns);
        Assert.Equal(3, canvas.Rows);
        Assert.Equal("ab", RowText(canvas, 0));
        Assert.True(canvas.Input.TryRead(out var input));
        Assert.Equal(new ResizeEvent(2, 3), input);
    }

    [Fact]
    public void Resize_ThroughWideCharacter_DoesNotLeaveHalfGlyph()
    {
        using var canvas = new CellCanvas(4, 1);
        canvas.Write(1, 0, "日");

        canvas.Resize(2, 1);

        Assert.Equal(CellWidth.Narrow, canvas.GetCell(1, 0).Width);
    }

    [Fact]
    public void Present_AfterDrawing_PublishesFrameOnlyOnPresent()
    {
        using var canvas = new CellCanvas(4, 1);
        var frame = new CellGrid(1, 1);
        var presented = 0;
        canvas.Presented += () => presented++;
        canvas.Write(0, 0, "abcd");

        canvas.CopyFrame(frame);
        Assert.Equal(' ', (char)frame[0, 0].Rune.Value);

        canvas.Present();
        var info = canvas.CopyFrame(frame);
        Assert.Equal('a', (char)frame[0, 0].Rune.Value);
        Assert.Equal(1, info.Version);

        canvas.Present();
        Assert.Equal(1, presented);
    }

    [Fact]
    public void Dispose_Always_CompletesInputAndCancelsClosed()
    {
        var canvas = new CellCanvas(4, 1);
        var closed = canvas.Closed;

        canvas.Dispose();

        Assert.True(closed.IsCancellationRequested);
        Assert.True(canvas.Input.IsCompleted);
    }

    [Fact]
    public void Exit_Always_RaisesExitRequested()
    {
        using var canvas = new CellCanvas(4, 1);
        var raised = 0;
        canvas.ExitRequested += () => raised++;

        canvas.Exit();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InterceptClose_ByDefault_IsOffAndCanBeSet()
    {
        using var canvas = new CellCanvas(4, 1);
        var byDefault = canvas.InterceptClose;

        canvas.InterceptClose = true;

        Assert.False(byDefault);
        Assert.True(canvas.InterceptClose);
    }

    [Fact]
    public void Title_WhenChanged_RaisesTitleChangedOnlyForNewValues()
    {
        using var canvas = new CellCanvas(4, 1);
        var titles = new List<string>();
        canvas.TitleChanged += titles.Add;

        canvas.Title = "notes.txt";
        canvas.Title = "notes.txt";
        canvas.Title = "notes.txt *";

        Assert.Equal(["notes.txt", "notes.txt *"], titles);
        Assert.Equal("notes.txt *", canvas.Title);
    }

    [Fact]
    public void Title_SetToNull_Throws()
    {
        using var canvas = new CellCanvas(4, 1);

        Assert.Throws<ArgumentNullException>(() => canvas.Title = null!);
    }

    [Fact]
    public void Icon_WhenChanged_RaisesIconChangedAndCopiesTheBytes()
    {
        using var canvas = new CellCanvas(4, 1);
        var raised = 0;
        canvas.IconChanged += () => raised++;
        var bytes = new byte[] { 1, 2, 3 };

        canvas.Icon = bytes;
        canvas.Icon = new byte[] { 1, 2, 3 };
        bytes[0] = 9;

        Assert.Equal(1, raised);
        Assert.Equal(new byte[] { 1, 2, 3 }, canvas.Icon.ToArray());
    }

    [Fact]
    public void WindowState_WhenSet_AsksTheHostAndKeepsTheReportedState()
    {
        using var canvas = new CellCanvas(4, 1);
        var requests = new List<CanvasWindowState>();
        canvas.WindowStateRequested += requests.Add;

        canvas.WindowState = CanvasWindowState.Maximized;

        Assert.Equal([CanvasWindowState.Maximized], requests);
        Assert.Equal(CanvasWindowState.Normal, canvas.WindowState);
    }

    [Fact]
    public void ReportWindowState_WhenChanged_PostsAnEventOnce()
    {
        using var canvas = new CellCanvas(4, 1);

        canvas.ReportWindowState(CanvasWindowState.Maximized);
        canvas.ReportWindowState(CanvasWindowState.Maximized);

        Assert.True(canvas.Input.TryRead(out var posted));
        Assert.Equal(new WindowStateEvent(CanvasWindowState.Maximized), posted);
        Assert.False(canvas.Input.TryRead(out _));
        Assert.Equal(CanvasWindowState.Maximized, canvas.WindowState);
    }

    [Fact]
    public void WindowDragRegions_WhenSet_AreCopied()
    {
        using var canvas = new CellCanvas(10, 2);
        var regions = new List<CellRect> { new(0, 0, 5, 1) };

        canvas.WindowDragRegions = regions;
        regions.Add(new CellRect(5, 0, 5, 1));

        Assert.Equal([new CellRect(0, 0, 5, 1)], canvas.WindowDragRegions);
    }

    private static string RowText(CellCanvas canvas, int y)
    {
        var sb = new StringBuilder();
        for (var x = 0; x < canvas.Columns; x++)
        {
            var cell = canvas.GetCell(x, y);
            if (cell.Width != CellWidth.WideTrail)
            {
                sb.Append(cell.Rune.ToString());
            }
        }

        return sb.ToString();
    }
}
