using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

/// <summary>Avalonia control that displays a <see cref="CellCanvas"/> and feeds keyboard, text and mouse input back to it.</summary>
public sealed class CanvasView : Control
{
    /// <summary>Identifies <see cref="Canvas"/>.</summary>
    public static readonly StyledProperty<CellCanvas> CanvasProperty =
        AvaloniaProperty.Register<CanvasView, CellCanvas>(nameof(Canvas));

    /// <summary>Identifies <see cref="Sizing"/>.</summary>
    public static readonly StyledProperty<CanvasSizing> SizingProperty =
        AvaloniaProperty.Register<CanvasView, CanvasSizing>(nameof(Sizing), CanvasSizing.ScaleToFit);

    /// <summary>Identifies <see cref="PreserveAspectRatio"/>.</summary>
    public static readonly StyledProperty<bool> PreserveAspectRatioProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(PreserveAspectRatio), true);

    /// <summary>Identifies <see cref="FontFamily"/>.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<CanvasView, FontFamily>(nameof(FontFamily), CanvasFonts.Default);

    /// <summary>Identifies <see cref="FontSize"/>.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CanvasView, double>(nameof(FontSize), 16d);

    /// <summary>Identifies <see cref="Palette"/>.</summary>
    public static readonly StyledProperty<ColorPalette> PaletteProperty =
        AvaloniaProperty.Register<CanvasView, ColorPalette>(nameof(Palette), ColorPalette.Campbell);

    /// <summary>Identifies <see cref="CursorBlink"/>.</summary>
    public static readonly StyledProperty<bool> CursorBlinkProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(CursorBlink), true);

    private readonly CellGrid _frame = new(1, 1);
    private readonly RenderResources _resources = new();
    private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(530) };

    private (double Advance, double Height)? _fontMetrics;
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

    /// <summary>Creates a view showing a new 80 x 25 canvas.</summary>
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

    /// <summary>Whether the grid scales to the control or the control decides the number of columns and rows.</summary>
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

    /// <summary>The font of the cells. Must be monospaced. Defaults to <see cref="CanvasFonts.Default"/>; <see cref="CanvasFonts"/> lists the embedded fonts.</summary>
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

    /// <summary>Maps the cell colors to RGB.</summary>
    public ColorPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>Whether the cursor blinks while the control has focus.</summary>
    public bool CursorBlink
    {
        get => GetValue(CursorBlinkProperty);
        set => SetValue(CursorBlinkProperty, value);
    }

    private double RenderScaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;

    private (double Advance, double Height) FontMetrics => _fontMetrics ??= MeasureFont();

    /// <summary>Gets the size needed to show a grid of the given dimensions at <see cref="FontSize"/>.</summary>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    public Size MeasureGrid(int columns, int rows)
    {
        var (cellWidth, cellHeight) = NaturalCellSize();
        return new Size(columns * cellWidth, rows * cellHeight);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var info = Canvas.CopyFrame(_frame);
        var palette = Palette;
        var columns = _frame.Columns;
        var rows = _frame.Rows;
        var bounds = new Rect(Bounds.Size);

        context.FillRectangle(_resources.BrushFor(palette.DefaultBackground), bounds);

        var layout = ComputeLayout(columns, rows, out var fontSize);
        if (layout.CellWidth <= 0 || layout.CellHeight <= 0)
        {
            return;
        }

        var renderer = new CellRenderer(_resources, context, palette, layout, FontFamily, fontSize, RenderScaling);

        using (context.PushClip(bounds))
        {
            for (var y = 0; y < rows; y++)
            {
                var row = _frame.Row(y);
                FillBackgrounds(renderer, palette, row, y);

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

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Canvas.Presented += OnPresented;
        UpdateBlinkTimer();
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Canvas.Presented -= OnPresented;
        _blinkTimer.Stop();
    }

    /// <inheritdoc/>
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
            _resources.ClearGlyphs();
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

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = MeasureGrid(Canvas.Columns, Canvas.Rows);
        return new Size(Math.Min(natural.Width, availableSize.Width), Math.Min(natural.Height, availableSize.Height));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        ApplyFillSizing(finalSize);
        return finalSize;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    private static void FillBackgrounds(CellRenderer renderer, ColorPalette palette, ReadOnlySpan<Cell> row, int y)
    {
        // Runs of equal background are painted as one rectangle.
        for (var x = 0; x < row.Length;)
        {
            RenderResources.ResolveColors(palette, row[x].Style, out _, out var background);
            var end = x + 1;
            while (end < row.Length)
            {
                RenderResources.ResolveColors(palette, row[end].Style, out _, out var next);
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

    // Measured at a large size so rounding inside the text engine does not skew the ratios.
    private (double Advance, double Height) MeasureFont()
    {
        var sample = RenderResources.CreateText("M", FontFamily, 100, Brushes.White, bold: false, italic: false);
        return (sample.Width, sample.Height);
    }

    private (double Width, double Height) NaturalCellSize()
    {
        var scale = RenderScaling;
        var (advance, height) = FontMetrics;
        return (
            Math.Ceiling(advance * FontSize / 100 * scale) / scale,
            Math.Ceiling(height * FontSize / 100 * scale) / scale);
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
