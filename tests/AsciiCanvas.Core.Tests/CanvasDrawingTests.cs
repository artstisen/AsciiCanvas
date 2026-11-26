using System.Text;

namespace AsciiCanvas.Tests;

public class CanvasDrawingTests
{
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

    [Fact]
    public void Write_DrawsTextAndReturnsEndColumn()
    {
        var canvas = new CellCanvas(10, 2);
        var end = canvas.Write(2, 1, "hi");

        Assert.Equal(4, end);
        Assert.Equal("  hi      ", RowText(canvas, 1));
    }

    [Fact]
    public void Write_ClipsAtCanvasEdge()
    {
        var canvas = new CellCanvas(5, 1);
        var end = canvas.Write(3, 0, "abcdef");

        Assert.Equal(9, end);
        Assert.Equal("   ab", RowText(canvas, 0));
    }

    [Fact]
    public void Write_WideCharacterTakesTwoCells()
    {
        var canvas = new CellCanvas(6, 1);
        var end = canvas.Write(0, 0, "日a");

        Assert.Equal(3, end);
        Assert.Equal(CellWidth.WideLead, canvas.GetCell(0, 0).Width);
        Assert.Equal(CellWidth.WideTrail, canvas.GetCell(1, 0).Width);
        Assert.Equal('a', (char)canvas.GetCell(2, 0).Rune.Value);
    }

    [Fact]
    public void Overwriting_HalfOfWideCharacter_BlanksTheOtherHalf()
    {
        var canvas = new CellCanvas(6, 1);
        canvas.Write(0, 0, "日");
        canvas.SetCell(1, 0, 'x');

        Assert.Equal(CellWidth.Single, canvas.GetCell(0, 0).Width);
        Assert.Equal(' ', (char)canvas.GetCell(0, 0).Rune.Value);
        Assert.Equal('x', (char)canvas.GetCell(1, 0).Rune.Value);
    }

    [Fact]
    public void WideCharacter_AtLastColumn_IsNotDrawn()
    {
        var canvas = new CellCanvas(3, 1);
        canvas.Write(2, 0, "日");

        Assert.Equal(' ', (char)canvas.GetCell(2, 0).Rune.Value);
        Assert.Equal(CellWidth.Single, canvas.GetCell(2, 0).Width);
    }

    [Fact]
    public void Clip_RestrictsDrawing_AndRestoresOnDispose()
    {
        var canvas = new CellCanvas(10, 3);
        using (canvas.PushClip(new CellRect(2, 1, 3, 1)))
        {
            canvas.Fill(canvas.Bounds, '#');
        }

        canvas.SetCell(0, 0, 'z');

        Assert.Equal("z         ", RowText(canvas, 0));
        Assert.Equal("  ###     ", RowText(canvas, 1));
    }

    [Fact]
    public void Box_DrawsFrameWithTitle()
    {
        var canvas = new CellCanvas(10, 3);
        canvas.Box(new CellRect(0, 0, 10, 3), BoxStyle.Single, title: "Hi");

        Assert.Equal("┌ Hi ────┐", RowText(canvas, 0));
        Assert.Equal("│        │", RowText(canvas, 1));
        Assert.Equal("└────────┘", RowText(canvas, 2));
    }

    [Fact]
    public void Scroll_MovesContentAndClearsVacatedRows()
    {
        var canvas = new CellCanvas(3, 3);
        canvas.Write(0, 0, "aaa");
        canvas.Write(0, 1, "bbb");
        canvas.Write(0, 2, "ccc");

        canvas.Scroll(canvas.Bounds, 0, -1);

        Assert.Equal("bbb", RowText(canvas, 0));
        Assert.Equal("ccc", RowText(canvas, 1));
        Assert.Equal("   ", RowText(canvas, 2));
    }

    [Fact]
    public void CaptureAndBlit_RoundTripsWithSkippedBlanks()
    {
        var canvas = new CellCanvas(6, 2);
        canvas.Write(0, 0, "a b");
        var sprite = canvas.Capture(new CellRect(0, 0, 3, 1));
        canvas.Fill(canvas.Bounds, '.');

        canvas.Blit(sprite, 2, 1, skipBlanks: true);

        Assert.Equal("......", RowText(canvas, 0));
        Assert.Equal("..a.b.", RowText(canvas, 1));
    }

    [Fact]
    public void Resize_KeepsContentAndPostsResizeEvent()
    {
        var canvas = new CellCanvas(4, 2);
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
        var canvas = new CellCanvas(4, 1);
        canvas.Write(1, 0, "日");

        canvas.Resize(2, 1);

        Assert.Equal(CellWidth.Single, canvas.GetCell(1, 0).Width);
    }

    [Fact]
    public void Present_PublishesFrameOnlyOnPresent()
    {
        var canvas = new CellCanvas(4, 1);
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
}
