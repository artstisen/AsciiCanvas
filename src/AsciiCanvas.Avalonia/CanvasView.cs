using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

/// <summary>How the grid relates to the size of the control.</summary>
public enum CanvasSizing
{
    /// <summary>The grid keeps its column and row count; cells and font scale to fit the control.</summary>
    ScaleToFit,

    /// <summary>Cells keep the size given by <see cref="CanvasView.FontSize"/>; the canvas gains or loses columns and rows as the control is resized.</summary>
    FillWithCells,
}

/// <summary>Avalonia control that displays a <see cref="CellCanvas"/> and feeds keyboard, text and mouse input back to it.</summary>
public sealed class CanvasView : Control
{
    private static readonly FontFamily DefaultFontFamily = new("Cascadia Mono, Cascadia Code, Consolas, Menlo, DejaVu Sans Mono, Courier New");

    public static readonly StyledProperty<CellCanvas> CanvasProperty =
        AvaloniaProperty.Register<CanvasView, CellCanvas>(nameof(Canvas));

    public static readonly StyledProperty<CanvasSizing> SizingProperty =
        AvaloniaProperty.Register<CanvasView, CanvasSizing>(nameof(Sizing), CanvasSizing.ScaleToFit);

    public static readonly StyledProperty<bool> PreserveAspectRatioProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(PreserveAspectRatio), true);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<CanvasView, FontFamily>(nameof(FontFamily), DefaultFontFamily);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CanvasView, double>(nameof(FontSize), 16d);

    public static readonly StyledProperty<ColorPalette> PaletteProperty =
        AvaloniaProperty.Register<CanvasView, ColorPalette>(nameof(Palette), ColorPalette.Campbell);

    public static readonly StyledProperty<bool> CursorBlinkProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(CursorBlink), true);

    private readonly CellGrid _frame = new(1, 1);
    private readonly Dictionary<uint, IBrush> _brushes = [];
    private readonly Dictionary<GlyphKey, FormattedText> _glyphs = [];
    private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(530) };

    private (double Advance, double Height)? _fontMetrics;
    private double _glyphFontSize;
    private int _invalidatePending;
    private bool _blinkOn = true;
    private bool _attached;
    private (int Column, int Row) _lastMouseCell = (-1, -1);

    static CanvasView()
    {
        FocusableProperty.OverrideDefaultValue<CanvasView>(true);
        AffectsRender<CanvasView>(CanvasProperty, SizingProperty, PreserveAspectRatioProperty, FontFamilyProperty, FontSizeProperty, PaletteProperty);
        AffectsMeasure<CanvasView>(SizingProperty, FontFamilyProperty, FontSizeProperty);
    }

    public CanvasView()
    {
        Canvas = new CellCanvas();
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkOn = !_blinkOn;
            InvalidateVisual();
        };
    }

    /// <summary>The canvas shown by this control. Defaults to a new 80 x 25 canvas.</summary>
    public CellCanvas Canvas
    {
        get => GetValue(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    public CanvasSizing Sizing
    {
        get => GetValue(SizingProperty);
        set => SetValue(SizingProperty, value);
    }

    /// <summary>With <see cref="CanvasSizing.ScaleToFit"/>: keep cells at the font's natural proportions (and center the grid) instead of stretching them to fill.</summary>
    public bool PreserveAspectRatio
    {
        get => GetValue(PreserveAspectRatioProperty);
        set => SetValue(PreserveAspectRatioProperty, value);
    }

    /// <summary>Must be a monospaced font.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Font size of the cells with <see cref="CanvasSizing.FillWithCells"/>, and the natural size used for measuring.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public ColorPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    public bool CursorBlink
    {
        get => GetValue(CursorBlinkProperty);
        set => SetValue(CursorBlinkProperty, value);
    }

    // ---- Lifetime / property changes -----------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Canvas.Presented += OnPresented;
        UpdateBlinkTimer();
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Canvas.Presented -= OnPresented;
        _blinkTimer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CanvasProperty)
        {
            if (_attached)
            {
                if (change.OldValue is CellCanvas old)
                {
                    old.Presented -= OnPresented;
                }

                Canvas.Presented += OnPresented;
            }

            ApplyFillSizing(Bounds.Size);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            _fontMetrics = null;
            _glyphs.Clear();
            ApplyFillSizing(Bounds.Size);
        }
        else if (change.Property == SizingProperty)
        {
            ApplyFillSizing(Bounds.Size);
        }
        else if (change.Property == CursorBlinkProperty)
        {
            UpdateBlinkTimer();
        }
    }

    private void OnPresented()
    {
        if (Interlocked.Exchange(ref _invalidatePending, 1) == 0)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    Volatile.Write(ref _invalidatePending, 0);
                    InvalidateVisual();
                },
                DispatcherPriority.Render);
        }
    }

    private void UpdateBlinkTimer()
    {
        _blinkOn = true;
        if (_attached && CursorBlink)
        {
            _blinkTimer.Start();
        }
        else
        {
            _blinkTimer.Stop();
        }
    }

    private void RestartBlink()
    {
        if (CursorBlink && !_blinkOn)
        {
            _blinkOn = true;
            InvalidateVisual();
        }

        if (_attached && CursorBlink)
        {
            _blinkTimer.Stop();
            _blinkTimer.Start();
        }
    }

    // ---- Layout --------------------------------------------------------------------------------------------

    private double RenderScaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;

    private (double Advance, double Height) FontMetrics => _fontMetrics ??= MeasureFont();

    private (double Advance, double Height) MeasureFont()
    {
        // Measure at a large size so rounding inside the text engine does not skew the ratios.
        var sample = CreateText("M", 100, Brushes.White, bold: false, italic: false);
        return (sample.Width, sample.Height);
    }

    private FormattedText CreateText(string text, double fontSize, IBrush brush, bool bold, bool italic) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            fontSize,
            brush);

    /// <summary>The size needed to show a grid of the given dimensions at <see cref="FontSize"/>.</summary>
    public Size MeasureGrid(int columns, int rows)
    {
        var (cellWidth, cellHeight) = NaturalCellSize();
        return new Size(columns * cellWidth, rows * cellHeight);
    }

    private (double Width, double Height) NaturalCellSize()
    {
        var scale = RenderScaling;
        var (advance, height) = FontMetrics;
        return (
            Math.Ceiling(advance * FontSize / 100 * scale) / scale,
            Math.Ceiling(height * FontSize / 100 * scale) / scale);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = MeasureGrid(Canvas.Columns, Canvas.Rows);
        return new Size(Math.Min(natural.Width, availableSize.Width), Math.Min(natural.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ApplyFillSizing(finalSize);
        return finalSize;
    }

    private void ApplyFillSizing(Size size)
    {
        if (Sizing != CanvasSizing.FillWithCells || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var (cellWidth, cellHeight) = NaturalCellSize();
        var (columns, rows) = GridLayout.CountFor(size.Width, size.Height, cellWidth, cellHeight);
        if (columns != Canvas.Columns || rows != Canvas.Rows)
        {
            Canvas.Resize(columns, rows);
        }
    }

    private GridLayout ComputeLayout(int columns, int rows, out double fontSize)
    {
        var (advance, height) = FontMetrics;
        if (Sizing == CanvasSizing.FillWithCells)
        {
            var (cellWidth, cellHeight) = NaturalCellSize();
            fontSize = FontSize;
            return new GridLayout(cellWidth, cellHeight, 0, 0);
        }

        var layout = GridLayout.Fit(Bounds.Width, Bounds.Height, columns, rows, advance / height, PreserveAspectRatio);
        fontSize = layout.CellWidth <= 0 ? FontSize : Math.Min(layout.CellWidth * 100 / advance, layout.CellHeight * 100 / height);
        return layout;
    }

    // ---- Rendering -----------------------------------------------------------------------------------------

    private readonly record struct GlyphKey(int Rune, uint Color, bool Bold, bool Italic);

    private IBrush BrushFor(uint rgb)
    {
        if (!_brushes.TryGetValue(rgb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            _brushes[rgb] = brush;
        }

        return brush;
    }

    private static uint Mix(uint from, uint to, double amount)
    {
        static uint Channel(uint a, uint b, double t) => (uint)Math.Round(a + (b - a) * t);
        return Channel(from >> 16 & 0xFF, to >> 16 & 0xFF, amount) << 16
            | Channel(from >> 8 & 0xFF, to >> 8 & 0xFF, amount) << 8
            | Channel(from & 0xFF, to & 0xFF, amount);
    }

    private void ResolveColors(ColorPalette palette, CellStyle style, out uint foreground, out uint background)
    {
        foreground = palette.Resolve(style.Foreground, foreground: true);
        background = palette.Resolve(style.Background, foreground: false);
        if ((style.Attributes & CellAttributes.Dim) != 0)
        {
            foreground = Mix(foreground, background, 0.5);
        }

        if ((style.Attributes & CellAttributes.Reverse) != 0)
        {
            (foreground, background) = (background, foreground);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var info = Canvas.CopyFrame(_frame);
        var palette = Palette;
        var columns = _frame.Columns;
        var rows = _frame.Rows;
        var bounds = new Rect(Bounds.Size);

        context.FillRectangle(BrushFor(palette.DefaultBackground), bounds);

        var layout = ComputeLayout(columns, rows, out var fontSize);
        if (layout.CellWidth <= 0 || layout.CellHeight <= 0)
        {
            return;
        }

        if (Math.Abs(fontSize - _glyphFontSize) > 0.001 || _glyphs.Count > 8192)
        {
            _glyphs.Clear();
            _glyphFontSize = fontSize;
        }

        var scale = RenderScaling;
        var renderer = new CellRenderer(this, context, palette, layout, fontSize, scale);

        using (context.PushClip(bounds))
        {
            for (var y = 0; y < rows; y++)
            {
                var row = _frame.Row(y);

                for (var x = 0; x < columns;)
                {
                    ResolveColors(palette, row[x].Style, out _, out var background);
                    var end = x + 1;
                    while (end < columns)
                    {
                        ResolveColors(palette, row[end].Style, out _, out var next);
                        if (next != background)
                        {
                            break;
                        }

                        end++;
                    }

                    if (background != palette.DefaultBackground)
                    {
                        renderer.FillCells(background, x, y, end - x);
                    }

                    x = end;
                }

                for (var x = 0; x < columns; x++)
                {
                    var cell = row[x];
                    if (cell.Width != CellWidth.WideTrail)
                    {
                        renderer.DrawForeground(cell, x, y, invert: false);
                    }
                }
            }

            if (info.CursorVisible && (_blinkOn || !CursorBlink || !IsFocused))
            {
                var cursorX = Math.Clamp(info.CursorColumn, 0, columns - 1);
                var cursorY = Math.Clamp(info.CursorRow, 0, rows - 1);
                renderer.DrawCursor(_frame[cursorX, cursorY], cursorX, cursorY, info.CursorShape, IsFocused);
            }
        }
    }

    private sealed class CellRenderer(
        CanvasView view,
        DrawingContext context,
        ColorPalette palette,
        GridLayout layout,
        double fontSize,
        double scale)
    {
        private double SnapX(double column) => Math.Round((layout.OffsetX + column * layout.CellWidth) * scale) / scale;

        private double SnapY(double row) => Math.Round((layout.OffsetY + row * layout.CellHeight) * scale) / scale;

        private Rect CellBounds(int column, int row, int span = 1) =>
            new(SnapX(column), SnapY(row), SnapX(column + span) - SnapX(column), SnapY(row + 1) - SnapY(row));

        public void FillCells(uint rgb, int column, int row, int span) =>
            context.FillRectangle(view.BrushFor(rgb), CellBounds(column, row, span));

        /// <summary>Draws glyph and decorations of one cell. With <paramref name="invert"/> the cell is also painted in swapped colors (for the cursor).</summary>
        public void DrawForeground(Cell cell, int column, int row, bool invert)
        {
            view.ResolveColors(palette, cell.Style, out var foreground, out var background);
            if (invert)
            {
                (foreground, background) = (background, foreground);
                if (cell.Width == CellWidth.Single || cell.Width == CellWidth.WideLead)
                {
                    FillCells(background, column, row, cell.Width == CellWidth.WideLead ? 2 : 1);
                }
            }

            var attributes = cell.Style.Attributes;
            var rect = CellBounds(column, row, cell.Width == CellWidth.WideLead ? 2 : 1);
            var brush = view.BrushFor(foreground);
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

            var thickness = Math.Max(1 / scale, Math.Round(rect.Height / 14 * scale) / scale);
            if ((attributes & CellAttributes.Underline) != 0)
            {
                context.FillRectangle(brush, new Rect(rect.X, rect.Bottom - thickness * 2, rect.Width, thickness));
            }

            if ((attributes & CellAttributes.Strikethrough) != 0)
            {
                context.FillRectangle(brush, new Rect(rect.X, rect.Y + (rect.Height - thickness) / 2, rect.Width, thickness));
            }
        }

        public void DrawCursor(Cell cell, int column, int row, CursorShape shape, bool focused)
        {
            var rect = CellBounds(column, row);
            if (!focused)
            {
                view.ResolveColors(palette, cell.Style, out var fg, out var bg);
                var edge = Math.Max(1 / scale, Math.Round(rect.Height / 14 * scale) / scale);
                var pen = new Pen(view.BrushFor(fg), edge);
                context.DrawRectangle(null, pen, rect.Deflate(edge / 2));
                return;
            }

            switch (shape)
            {
                case CursorShape.Block:
                    DrawForeground(cell, column, row, invert: true);
                    break;
                case CursorShape.Underline:
                case CursorShape.Bar:
                    view.ResolveColors(palette, cell.Style, out var foreground, out _);
                    var size = Math.Max(2 / scale, Math.Round(rect.Height / 8 * scale) / scale);
                    var bar = shape == CursorShape.Underline
                        ? new Rect(rect.X, rect.Bottom - size, rect.Width, size)
                        : new Rect(rect.X, rect.Y, Math.Min(size, rect.Width), rect.Height);
                    context.FillRectangle(view.BrushFor(palette.DefaultForeground), bar);
                    break;
            }
        }

        private void DrawText(Rune rune, Rect rect, uint foreground, CellAttributes attributes)
        {
            var bold = (attributes & CellAttributes.Bold) != 0;
            var italic = (attributes & CellAttributes.Italic) != 0;
            var key = new GlyphKey(rune.Value, foreground, bold, italic);
            if (!view._glyphs.TryGetValue(key, out var text))
            {
                text = view.CreateText(rune.ToString(), fontSize, view.BrushFor(foreground), bold, italic);
                view._glyphs[key] = text;
            }

            context.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2, rect.Y + (rect.Height - text.Height) / 2));
        }

        // Box-drawing is painted from rectangles so neighbouring cells join without gaps at any scale.
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
            var heavy = light * 2 + (light > 1 ? 0 : 1);
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
                var y0 = cy - t / 2;
                if (sign > 0)
                {
                    Fill(cx - t / 2, y0, right, y0 + t);
                }
                else
                {
                    Fill(left, y0, cx - t / 2 + t, y0 + t);
                }
            }

            void Vertical(int weight, int sign)
            {
                var t = weight == 2 ? heavy : light;
                var x0 = cx - t / 2;
                if (sign > 0)
                {
                    Fill(x0, cy - t / 2, x0 + t, bottom);
                }
                else
                {
                    Fill(x0, top, x0 + t, cy - t / 2 + t);
                }
            }

            // Double arms: two parallel lines; where a perpendicular arm exists on a side, the line stops at its inner line.
            void DoubleHorizontal(int sign)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var y0 = cy + side * offset - light / 2;
                    var sameSide = side < 0 ? segments.Up != 0 : segments.Down != 0;
                    var otherSide = side < 0 ? segments.Down != 0 : segments.Up != 0;
                    int reach = sameSide ? offset - light / 2 : otherSide ? -offset - light / 2 : -light;
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
                foreach (var side in new[] { -1, 1 })
                {
                    var x0 = cx + side * offset - light / 2;
                    var sameSide = side < 0 ? segments.Left != 0 : segments.Right != 0;
                    var otherSide = side < 0 ? segments.Right != 0 : segments.Left != 0;
                    int reach = sameSide ? offset - light / 2 : otherSide ? -offset - light / 2 : -light;
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

            if (segments.Right == 3) DoubleHorizontal(1); else if (segments.Right != 0) Horizontal(segments.Right, 1);
            if (segments.Left == 3) DoubleHorizontal(-1); else if (segments.Left != 0) Horizontal(segments.Left, -1);
            if (segments.Down == 3) DoubleVertical(1); else if (segments.Down != 0) Vertical(segments.Down, 1);
            if (segments.Up == 3) DoubleVertical(-1); else if (segments.Up != 0) Vertical(segments.Up, -1);
        }

        // Block elements U+2580-U+259F.
        private bool TryDrawBlock(int code, Rect cell, uint foreground, uint background, IBrush brush)
        {
            if (code < 0x2580 || code > 0x259F)
            {
                return false;
            }

            Rect Part(double l, double t, double r, double b) =>
                new(cell.X + cell.Width * l, cell.Y + cell.Height * t, cell.Width * (r - l), cell.Height * (b - t));

            void Quadrants(bool upperLeft, bool upperRight, bool lowerLeft, bool lowerRight)
            {
                if (upperLeft) context.FillRectangle(brush, Part(0, 0, 0.5, 0.5));
                if (upperRight) context.FillRectangle(brush, Part(0.5, 0, 1, 0.5));
                if (lowerLeft) context.FillRectangle(brush, Part(0, 0.5, 0.5, 1));
                if (lowerRight) context.FillRectangle(brush, Part(0.5, 0.5, 1, 1));
            }

            switch (code)
            {
                case 0x2580: context.FillRectangle(brush, Part(0, 0, 1, 0.5)); break;
                case >= 0x2581 and <= 0x2588: context.FillRectangle(brush, Part(0, 1 - (code - 0x2580) / 8.0, 1, 1)); break;
                case >= 0x2589 and <= 0x258F: context.FillRectangle(brush, Part(0, 0, 1 - (code - 0x2588) / 8.0, 1)); break;
                case 0x2590: context.FillRectangle(brush, Part(0.5, 0, 1, 1)); break;
                case 0x2591: context.FillRectangle(view.BrushFor(Mix(background, foreground, 0.25)), cell); break;
                case 0x2592: context.FillRectangle(view.BrushFor(Mix(background, foreground, 0.5)), cell); break;
                case 0x2593: context.FillRectangle(view.BrushFor(Mix(background, foreground, 0.75)), cell); break;
                case 0x2594: context.FillRectangle(brush, Part(0, 0, 1, 1 / 8.0)); break;
                case 0x2595: context.FillRectangle(brush, Part(7 / 8.0, 0, 1, 1)); break;
                case 0x2596: Quadrants(false, false, true, false); break;
                case 0x2597: Quadrants(false, false, false, true); break;
                case 0x2598: Quadrants(true, false, false, false); break;
                case 0x2599: Quadrants(true, false, true, true); break;
                case 0x259A: Quadrants(true, false, false, true); break;
                case 0x259B: Quadrants(true, true, true, false); break;
                case 0x259C: Quadrants(true, true, false, true); break;
                case 0x259D: Quadrants(false, true, false, false); break;
                case 0x259E: Quadrants(false, true, true, false); break;
                case 0x259F: Quadrants(false, true, true, true); break;
            }

            return true;
        }
    }

    // ---- Input ---------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var key = InputMapping.MapKey(e.Key);
        if (key == Key.Unknown)
        {
            return;
        }

        var modifiers = InputMapping.MapModifiers(e.KeyModifiers);
        RestartBlink();
        Canvas.Input.Post(new KeyEvent(key, modifiers));
        if (InputMapping.ShouldHandle(key, modifiers))
        {
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        var text = InputMapping.FilterText(e.Text);
        if (text.Length > 0)
        {
            RestartBlink();
            Canvas.Input.Post(new TextEvent(text));
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        var button = InputMapping.MapButton(point.Properties.PointerUpdateKind);
        if (button == MouseButton.None)
        {
            return;
        }

        e.Pointer.Capture(this);
        PostMouse(point.Position, button, MouseAction.Press, 0, e.KeyModifiers);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetCurrentPoint(this);
        var button = InputMapping.MapButton(point.Properties.PointerUpdateKind);
        if (button == MouseButton.None)
        {
            return;
        }

        e.Pointer.Capture(null);
        PostMouse(point.Position, button, MouseAction.Release, 0, e.KeyModifiers);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        var cell = CellAt(point.Position);
        if (cell == _lastMouseCell)
        {
            return;
        }

        _lastMouseCell = cell;
        var properties = point.Properties;
        var button = properties.IsLeftButtonPressed ? MouseButton.Left
            : properties.IsMiddleButtonPressed ? MouseButton.Middle
            : properties.IsRightButtonPressed ? MouseButton.Right
            : MouseButton.None;
        Canvas.Input.Post(new MouseEvent(cell.Column, cell.Row, button, MouseAction.Move, 0, InputMapping.MapModifiers(e.KeyModifiers)));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var notches = (int)Math.Round(e.Delta.Y);
        if (notches == 0 && e.Delta.Y != 0)
        {
            notches = Math.Sign(e.Delta.Y);
        }

        if (notches != 0)
        {
            PostMouse(e.GetPosition(this), MouseButton.None, MouseAction.Wheel, notches, e.KeyModifiers);
            e.Handled = true;
        }
    }

    private (int Column, int Row) CellAt(Point point)
    {
        var canvas = Canvas;
        var layout = ComputeLayout(canvas.Columns, canvas.Rows, out _);
        return layout.CellAt(point.X, point.Y, canvas.Columns, canvas.Rows);
    }

    private void PostMouse(Point point, MouseButton button, MouseAction action, int wheel, global::Avalonia.Input.KeyModifiers modifiers)
    {
        var (column, row) = CellAt(point);
        _lastMouseCell = (column, row);
        Canvas.Input.Post(new MouseEvent(column, row, button, action, wheel, InputMapping.MapModifiers(modifiers)));
    }
}
