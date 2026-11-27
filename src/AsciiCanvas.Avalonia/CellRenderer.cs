using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AsciiCanvas.Avalonia;

/// <summary>Paints the cells of one frame. Box-drawing and block characters are drawn from rectangles so neighbouring cells join without gaps at any scale.</summary>
internal sealed class CellRenderer(
    RenderResources resources,
    DrawingContext context,
    ColorPalette palette,
    GridLayout layout,
    FontFamily fontFamily,
    double fontSize,
    double scale)
{
    private static readonly int[] _sides = [-1, 1];

    private readonly GlyphRunBuilder _run = new();
    private readonly GlyphTypeface?[] _faces = new GlyphTypeface?[4];
    private int _facesLoaded;

    public void FillCells(uint rgb, int column, int row, int span) =>
        context.FillRectangle(resources.BrushFor(rgb), CellBounds(column, row, span));

    /// <summary>
    /// Draws glyph and decorations of one cell. With <paramref name="invert"/> the cell is also painted in swapped colors (for the
    /// cursor), or with <paramref name="cursorColor"/> as its background and the default background as its foreground.
    /// </summary>
    public void DrawForeground(Cell cell, int column, int row, bool invert, uint? cursorColor = null)
    {
        RenderResources.ResolveColors(palette, cell.Style, out var foreground, out var background);
        if (invert)
        {
            (foreground, background) = cursorColor is { } color ? (palette.DefaultBackground, color) : (background, foreground);
            if (cell.Width is CellWidth.Narrow or CellWidth.WideLead)
            {
                FillCells(background, column, row, cell.Width == CellWidth.WideLead ? 2 : 1);
            }
        }

        var attributes = cell.Style.Attributes;
        var rect = CellBounds(column, row, cell.Width == CellWidth.WideLead ? 2 : 1);
        var brush = resources.BrushFor(foreground);
        var rune = cell.Rune;

        if (!cell.IsBlank)
        {
            if (BoxDrawing.TryGetSegments(rune, out var segments))
            {
                DrawLines(segments, rect, brush);
            }
            else if (!TryDrawBlock(rune.Value, rect, foreground, background, brush))
            {
                DrawText(rune, rect, foreground, attributes);
            }
        }

        DrawDecorations(brush, rect, attributes);
    }

    /// <summary>
    /// Draws the foreground of one row. Neighbouring plain glyphs with the same color and font style are drawn as one glyph run,
    /// which is far cheaper than one text draw per cell; box, block, wide and fallback-font cells are drawn one by one.
    /// </summary>
    public void DrawRow(ReadOnlySpan<Cell> row, int y)
    {
        var run = _run;

        for (var x = 0; x < row.Length; x++)
        {
            var cell = row[x];
            if (cell.Width == CellWidth.WideTrail)
            {
                continue;
            }

            RenderResources.ResolveColors(palette, cell.Style, out var foreground, out _);
            var attributes = cell.Style.Attributes;
            var decorated = (attributes & (CellAttributes.Underline | CellAttributes.Strikethrough)) != 0;

            if (cell.IsBlank)
            {
                // A space has no ink, so it can sit inside an open run instead of splitting it.
                if (run.Count > 0 && !run.TryAppendSpace())
                {
                    FlushRun(y);
                }

                if (decorated)
                {
                    DrawDecorations(resources.BrushFor(foreground), CellBounds(x, y), attributes);
                }

                continue;
            }

            var bold = (attributes & CellAttributes.Bold) != 0;
            var italic = (attributes & CellAttributes.Italic) != 0;
            var rune = cell.Rune;
            var face = FaceFor(bold, italic);
            ushort glyph = 0;

            if (face is null
                || cell.Width != CellWidth.Narrow
                || rune.Value is > 0xFFFF or (>= 0x2580 and <= 0x259F)
                || BoxDrawing.TryGetSegments(rune, out _)
                || !face.CharacterToGlyphMap.TryGetGlyph(rune.Value, out glyph)
                || glyph == 0)
            {
                FlushRun(y);
                DrawForeground(cell, x, y, invert: false);
                continue;
            }

            if (run.Count > 0 && !run.Accepts(foreground, bold, italic))
            {
                FlushRun(y);
            }

            if (run.Count == 0)
            {
                run.Start(x, foreground, bold, italic, face);
            }

            run.Append(glyph, (char)rune.Value);
            if (decorated)
            {
                DrawDecorations(resources.BrushFor(foreground), CellBounds(x, y), attributes);
            }
        }

        FlushRun(y);
    }

    private GlyphTypeface? FaceFor(bool bold, bool italic)
    {
        var index = (bold ? 1 : 0) | (italic ? 2 : 0);
        if ((_facesLoaded & (1 << index)) == 0)
        {
            _faces[index] = resources.FaceFor(fontFamily, bold, italic);
            _facesLoaded |= 1 << index;
        }

        return _faces[index];
    }

    private void FlushRun(int y)
    {
        var run = _run;
        run.TrimTrailingSpaces();
        if (run.Count == 0)
        {
            run.Clear();
            return;
        }

        var metrics = resources.MetricsFor(fontFamily, fontSize);
        var first = CellBounds(run.StartColumn, y);
        var infos = new GlyphInfo[run.Count];
        for (var i = 0; i < infos.Length; i++)
        {
            var width = SnapX(run.StartColumn + i + 1) - SnapX(run.StartColumn + i);
            infos[i] = new GlyphInfo(run.Glyphs[i], i, width, new Vector((width - first.Width) / 2, 0));
        }

        var origin = new Point(
            first.X + ((first.Width - metrics.Width) / 2),
            first.Y + ((first.Height - metrics.Height) / 2) + metrics.Baseline);
        var glyphRun = new GlyphRun(run.Face!, fontSize, new string(run.Characters, 0, run.Count).AsMemory(), infos, origin);
        context.DrawGlyphRun(resources.BrushFor(run.Foreground), glyphRun);
        glyphRun.Dispose();
        run.Clear();
    }

    private void DrawDecorations(IBrush brush, Rect rect, CellAttributes attributes)
    {
        if ((attributes & (CellAttributes.Underline | CellAttributes.Strikethrough)) == 0)
        {
            return;
        }

        var thickness = Math.Max(1 / scale, Math.Round(rect.Height / 14 * scale) / scale);
        if ((attributes & CellAttributes.Underline) != 0)
        {
            context.FillRectangle(brush, new Rect(rect.X, rect.Bottom - (thickness * 2), rect.Width, thickness));
        }

        if ((attributes & CellAttributes.Strikethrough) != 0)
        {
            context.FillRectangle(brush, new Rect(rect.X, rect.Y + ((rect.Height - thickness) / 2), rect.Width, thickness));
        }
    }

    public void DrawCursor(Cell cell, int column, int row, CursorShape shape, bool focused, uint? color = null)
    {
        var rect = CellBounds(column, row);
        if (!focused)
        {
            RenderResources.ResolveColors(palette, cell.Style, out var fg, out _);
            var edge = Math.Max(1 / scale, Math.Round(rect.Height / 14 * scale) / scale);
            var pen = new Pen(resources.BrushFor(color ?? fg), edge);
            context.DrawRectangle(null, pen, rect.Deflate(edge / 2));
            return;
        }

        switch (shape)
        {
            case CursorShape.Block:
                DrawForeground(cell, column, row, invert: true, color);
                break;
            case CursorShape.Underline:
            case CursorShape.Bar:
                var size = Math.Max(2 / scale, Math.Round(rect.Height / 8 * scale) / scale);
                var bar = shape == CursorShape.Underline
                    ? new Rect(rect.X, rect.Bottom - size, rect.Width, size)
                    : new Rect(rect.X, rect.Y, Math.Min(size, rect.Width), rect.Height);
                context.FillRectangle(resources.BrushFor(color ?? palette.DefaultForeground), bar);
                break;
        }
    }

    // Snapping cell edges to device pixels keeps neighbouring backgrounds from leaving seams.
    private double SnapX(double column) => Math.Round((layout.OffsetX + (column * layout.CellWidth)) * scale) / scale;

    private double SnapY(double row) => Math.Round((layout.OffsetY + (row * layout.CellHeight)) * scale) / scale;

    private Rect CellBounds(int column, int row, int span = 1) =>
        new(SnapX(column), SnapY(row), SnapX(column + span) - SnapX(column), SnapY(row + 1) - SnapY(row));

    private void DrawText(Rune rune, Rect rect, uint foreground, CellAttributes attributes)
    {
        var bold = (attributes & CellAttributes.Bold) != 0;
        var italic = (attributes & CellAttributes.Italic) != 0;
        var text = resources.GlyphFor(rune, foreground, bold, italic, fontFamily, fontSize);

        context.DrawText(text, new Point(rect.X + ((rect.Width - text.Width) / 2), rect.Y + ((rect.Height - text.Height) / 2)));
    }

    private void DrawLines(LineSegments segments, Rect cell, IBrush brush)
    {
        int Px(double v) => (int)Math.Round(v * scale);

        var left = Px(cell.X);
        var top = Px(cell.Y);
        var right = Px(cell.Right);
        var bottom = Px(cell.Bottom);
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var light = Math.Max(1, (int)Math.Round((bottom - top) / 12.0));
        var heavy = (light * 2) + (light > 1 ? 0 : 1);
        var offset = (int)Math.Ceiling(light * 1.5);

        void Fill(int l, int t, int r, int b)
        {
            if (r > l && b > t)
            {
                context.FillRectangle(brush, new Rect(l / scale, t / scale, (r - l) / scale, (b - t) / scale));
            }
        }

        // Single (light/heavy) arms: a bar of the given thickness from the cell center to the edge.
        void Horizontal(int weight, int sign)
        {
            var t = weight == 2 ? heavy : light;
            var y0 = cy - (t / 2);
            if (sign > 0)
            {
                Fill(cx - (t / 2), y0, right, y0 + t);
            }
            else
            {
                Fill(left, y0, cx - (t / 2) + t, y0 + t);
            }
        }

        void Vertical(int weight, int sign)
        {
            var t = weight == 2 ? heavy : light;
            var x0 = cx - (t / 2);
            if (sign > 0)
            {
                Fill(x0, cy - (t / 2), x0 + t, bottom);
            }
            else
            {
                Fill(x0, top, x0 + t, cy - (t / 2) + t);
            }
        }

        // Double arms: two parallel lines; where a perpendicular arm exists on a side, the line stops at its inner line.
        void DoubleHorizontal(int sign)
        {
            foreach (var side in _sides)
            {
                var y0 = cy + (side * offset) - (light / 2);
                var sameSide = side < 0 ? segments.Up != 0 : segments.Down != 0;
                var otherSide = side < 0 ? segments.Down != 0 : segments.Up != 0;
                var reach = sameSide ? offset - (light / 2) : otherSide ? -offset - (light / 2) : -light;
                if (sign > 0)
                {
                    Fill(cx + reach, y0, right, y0 + light);
                }
                else
                {
                    Fill(left, y0, cx - reach, y0 + light);
                }
            }
        }

        void DoubleVertical(int sign)
        {
            foreach (var side in _sides)
            {
                var x0 = cx + (side * offset) - (light / 2);
                var sameSide = side < 0 ? segments.Left != 0 : segments.Right != 0;
                var otherSide = side < 0 ? segments.Right != 0 : segments.Left != 0;
                var reach = sameSide ? offset - (light / 2) : otherSide ? -offset - (light / 2) : -light;
                if (sign > 0)
                {
                    Fill(x0, cy + reach, x0 + light, bottom);
                }
                else
                {
                    Fill(x0, top, x0 + light, cy - reach);
                }
            }
        }

        if (segments.Right == 3)
        {
            DoubleHorizontal(1);
        }
        else if (segments.Right != 0)
        {
            Horizontal(segments.Right, 1);
        }

        if (segments.Left == 3)
        {
            DoubleHorizontal(-1);
        }
        else if (segments.Left != 0)
        {
            Horizontal(segments.Left, -1);
        }

        if (segments.Down == 3)
        {
            DoubleVertical(1);
        }
        else if (segments.Down != 0)
        {
            Vertical(segments.Down, 1);
        }

        if (segments.Up == 3)
        {
            DoubleVertical(-1);
        }
        else if (segments.Up != 0)
        {
            Vertical(segments.Up, -1);
        }
    }

    // Block elements U+2580-U+259F.
    private bool TryDrawBlock(int code, Rect cell, uint foreground, uint background, IBrush brush)
    {
        if (code is < 0x2580 or > 0x259F)
        {
            return false;
        }

        Rect Part(double l, double t, double r, double b) =>
            new(cell.X + (cell.Width * l), cell.Y + (cell.Height * t), cell.Width * (r - l), cell.Height * (b - t));

        void Quadrants(bool upperLeft, bool upperRight, bool lowerLeft, bool lowerRight)
        {
            if (upperLeft)
            {
                context.FillRectangle(brush, Part(0, 0, 0.5, 0.5));
            }

            if (upperRight)
            {
                context.FillRectangle(brush, Part(0.5, 0, 1, 0.5));
            }

            if (lowerLeft)
            {
                context.FillRectangle(brush, Part(0, 0.5, 0.5, 1));
            }

            if (lowerRight)
            {
                context.FillRectangle(brush, Part(0.5, 0.5, 1, 1));
            }
        }

        switch (code)
        {
            case 0x2580:
                context.FillRectangle(brush, Part(0, 0, 1, 0.5));
                break;
            case >= 0x2581 and <= 0x2588:
                context.FillRectangle(brush, Part(0, 1 - ((code - 0x2580) / 8.0), 1, 1));
                break;
            case >= 0x2589 and <= 0x258F:
                context.FillRectangle(brush, Part(0, 0, 1 - ((code - 0x2588) / 8.0), 1));
                break;
            case 0x2590:
                context.FillRectangle(brush, Part(0.5, 0, 1, 1));
                break;
            case 0x2591:
                context.FillRectangle(resources.BrushFor(RenderResources.Mix(background, foreground, 0.25)), cell);
                break;
            case 0x2592:
                context.FillRectangle(resources.BrushFor(RenderResources.Mix(background, foreground, 0.5)), cell);
                break;
            case 0x2593:
                context.FillRectangle(resources.BrushFor(RenderResources.Mix(background, foreground, 0.75)), cell);
                break;
            case 0x2594:
                context.FillRectangle(brush, Part(0, 0, 1, 1 / 8.0));
                break;
            case 0x2595:
                context.FillRectangle(brush, Part(7 / 8.0, 0, 1, 1));
                break;
            case 0x2596:
                Quadrants(false, false, true, false);
                break;
            case 0x2597:
                Quadrants(false, false, false, true);
                break;
            case 0x2598:
                Quadrants(true, false, false, false);
                break;
            case 0x2599:
                Quadrants(true, false, true, true);
                break;
            case 0x259A:
                Quadrants(true, false, false, true);
                break;
            case 0x259B:
                Quadrants(true, true, true, false);
                break;
            case 0x259C:
                Quadrants(true, true, false, true);
                break;
            case 0x259D:
                Quadrants(false, true, false, false);
                break;
            case 0x259E:
                Quadrants(false, true, true, false);
                break;
            case 0x259F:
                Quadrants(false, true, true, true);
                break;
        }

        return true;
    }
}
