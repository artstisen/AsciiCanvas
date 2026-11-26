using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

/// <summary>Avalonia control that displays a <see cref="CellCanvas"/> and feeds keyboard, text and mouse input back to it.</summary>
public sealed class CanvasView : Control
{
    // The thickness in pixels of the band along each window edge that resizes a window without a frame.
    private const double ResizeGrip = 6;

    // Rows of scrollback per wheel notch, the usual system default.
    private const int WheelScrollbackLines = 3;

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

    /// <summary>Identifies <see cref="ShowDiagnostics"/>.</summary>
    public static readonly StyledProperty<bool> ShowDiagnosticsProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(ShowDiagnostics));

    /// <summary>Identifies <see cref="AllowDiagnosticsToggle"/>.</summary>
    public static readonly StyledProperty<bool> AllowDiagnosticsToggleProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(AllowDiagnosticsToggle));

    /// <summary>Identifies <see cref="InterceptPasteShortcut"/>.</summary>
    public static readonly StyledProperty<bool> InterceptPasteShortcutProperty =
        AvaloniaProperty.Register<CanvasView, bool>(nameof(InterceptPasteShortcut), true);

    private readonly CellGrid _frame = new(1, 1);
    private readonly RenderResources _resources = new();
    private readonly RenderStatistics _statistics = new(TimeProvider.System);
    private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(530) };

    private (double Advance, double Height)? _fontMetrics;
    private int _invalidatePending;
    private bool _blinkOn = true;
    private bool _cursorDrawn;
    private bool _attached;
    private (int Column, int Row) _lastMouseCell = (-1, -1);
    private ResizeEdge _cursorEdge;

    static CanvasView()
    {
        FocusableProperty.OverrideDefaultValue<CanvasView>(true);
        AffectsRender<CanvasView>(CanvasProperty, SizingProperty, PreserveAspectRatioProperty, FontFamilyProperty, FontSizeProperty, PaletteProperty, ShowDiagnosticsProperty);
        AffectsMeasure<CanvasView>(SizingProperty, FontFamilyProperty, FontSizeProperty);
    }

    /// <summary>Creates a view showing a new 80 x 25 canvas.</summary>
    public CanvasView()
    {
        Canvas = new CellCanvas();
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkOn = !_blinkOn;

            // A blink only changes the picture when a cursor is drawn; otherwise it would re-render the whole grid for nothing.
            if (_cursorDrawn && IsFocused)
            {
                InvalidateVisual();
            }
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

    /// <summary>
    /// Shows performance diagnostics over the grid: Avalonia's frame rate and render-time graphs, and an overlay with the
    /// grid size and how long the control takes to draw it. Meant for testing; off by default.
    /// </summary>
    public bool ShowDiagnostics
    {
        get => GetValue(ShowDiagnosticsProperty);
        set => SetValue(ShowDiagnosticsProperty, value);
    }

    /// <summary>
    /// When true, F3 switches <see cref="ShowDiagnostics"/> on and off while the control has focus, and the key is not
    /// passed on to the program. Off by default, so the program receives every key.
    /// </summary>
    public bool AllowDiagnosticsToggle
    {
        get => GetValue(AllowDiagnosticsToggleProperty);
        set => SetValue(AllowDiagnosticsToggleProperty, value);
    }

    /// <summary>
    /// When true (the default), Ctrl+V, Cmd+V and Shift+Insert read the clipboard and send the text to the program as a
    /// <see cref="PasteEvent"/>; the key press itself is not sent. Set it to false to receive the raw key press instead.
    /// </summary>
    public bool InterceptPasteShortcut
    {
        get => GetValue(InterceptPasteShortcutProperty);
        set => SetValue(InterceptPasteShortcutProperty, value);
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

        var started = TimeProvider.System.GetTimestamp();
        DrawFrame(context);
        _statistics.Record(TimeProvider.System.GetElapsedTime(started));

        if (ShowDiagnostics)
        {
            DrawDiagnostics(context);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Canvas.Presented += OnPresented;
        Canvas.Clipboard = new AvaloniaCanvasClipboard(this);
        UpdateBlinkTimer();
        ApplyRendererOverlays();
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Canvas.Presented -= OnPresented;
        Canvas.Clipboard = null;
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
                    old.Clipboard = null;
                }

                Canvas.Presented += OnPresented;
                Canvas.Clipboard = new AvaloniaCanvasClipboard(this);
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
        else if (change.Property == ShowDiagnosticsProperty)
        {
            ApplyRendererOverlays();
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
        if (AllowDiagnosticsToggle && key == Key.F3 && modifiers == KeyModifiers.None)
        {
            ShowDiagnostics = !ShowDiagnostics;
            e.Handled = true;
            return;
        }

        if (Canvas.ScrollbackLimit > 0 && modifiers == KeyModifiers.Shift && key is Key.PageUp or Key.PageDown)
        {
            Canvas.ScrollbackOffset += key == Key.PageUp ? Canvas.Rows : -Canvas.Rows;
            e.Handled = true;
            return;
        }

        if (InterceptPasteShortcut && InputMapping.IsPasteShortcut(key, modifiers))
        {
            e.Handled = true;
            PasteFromClipboard();
            return;
        }

        // As in a terminal, typing brings the view back to where the input goes.
        Canvas.ScrollbackOffset = 0;
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
            Canvas.ScrollbackOffset = 0;
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
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && TryStartWindowAction(e, point.Position))
        {
            e.Handled = true;
            return;
        }

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
        UpdateCursor(e);
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

        if (notches != 0 && Canvas.ScrollbackLimit > 0)
        {
            Canvas.ScrollbackOffset += notches * WheelScrollbackLines;
            e.Handled = true;
        }
        else if (notches != 0)
        {
            PostMouse(e.GetPosition(this), MouseButton.None, MouseAction.Wheel, notches, e.KeyModifiers);
            e.Handled = true;
        }
    }

    // For a window without a frame the control moves and resizes the window itself, because only the OS can run a drag.
    private Window? FramelessWindow =>
        TopLevel.GetTopLevel(this) is Window { WindowDecorations: not WindowDecorations.Full } window ? window : null;

    private static ResizeEdge EdgeAt(Window window, PointerEventArgs e)
    {
        if (window.WindowState != WindowState.Normal || !window.CanResize)
        {
            return ResizeEdge.None;
        }

        var position = e.GetPosition(window);
        return ResizeEdges.At(position.X, position.Y, window.Bounds.Width, window.Bounds.Height, ResizeGrip);
    }

    private bool TryStartWindowAction(PointerPressedEventArgs e, Point position)
    {
        if (FramelessWindow is not { } window)
        {
            return false;
        }

        var edge = EdgeAt(window, e);
        if (edge != ResizeEdge.None)
        {
            window.BeginResizeDrag(ToWindowEdge(edge), e);
            return true;
        }

        if (!IsOverDragRegion(position))
        {
            return false;
        }

        if (e.ClickCount == 2)
        {
            if (window.CanResize)
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }
        else
        {
            window.BeginMoveDrag(e);
        }

        return true;
    }

    private bool IsOverDragRegion(Point position)
    {
        var regions = Canvas.WindowDragRegions;
        if (regions.Count == 0)
        {
            return false;
        }

        var layout = ComputeLayout(Canvas.Columns, Canvas.Rows, out _);
        var x = position.X - layout.OffsetX;
        var y = position.Y - layout.OffsetY;
        if (x < 0 || y < 0 || x >= layout.CellWidth * Canvas.Columns || y >= layout.CellHeight * Canvas.Rows)
        {
            return false;
        }

        var (column, row) = layout.CellAt(position.X, position.Y, Canvas.Columns, Canvas.Rows);
        return regions.Any(region => region.Contains(column, row));
    }

    private void UpdateCursor(PointerEventArgs e)
    {
        var edge = FramelessWindow is { } window ? EdgeAt(window, e) : ResizeEdge.None;
        if (edge == _cursorEdge)
        {
            return;
        }

        _cursorEdge = edge;
        Cursor = edge == ResizeEdge.None ? null : new Cursor(ToCursorType(edge));
    }

    private static WindowEdge ToWindowEdge(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left => WindowEdge.West,
        ResizeEdge.Right => WindowEdge.East,
        ResizeEdge.Top => WindowEdge.North,
        ResizeEdge.Bottom => WindowEdge.South,
        ResizeEdge.TopLeft => WindowEdge.NorthWest,
        ResizeEdge.TopRight => WindowEdge.NorthEast,
        ResizeEdge.BottomLeft => WindowEdge.SouthWest,
        _ => WindowEdge.SouthEast,
    };

    private static StandardCursorType ToCursorType(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left or ResizeEdge.Right => StandardCursorType.SizeWestEast,
        ResizeEdge.Top or ResizeEdge.Bottom => StandardCursorType.SizeNorthSouth,
        ResizeEdge.TopLeft => StandardCursorType.TopLeftCorner,
        ResizeEdge.TopRight => StandardCursorType.TopRightCorner,
        ResizeEdge.BottomLeft => StandardCursorType.BottomLeftCorner,
        _ => StandardCursorType.BottomRightCorner,
    };

    private void DrawFrame(DrawingContext context)
    {
        var info = Canvas.CopyFrame(_frame);
        _cursorDrawn = info.CursorVisible;
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
                renderer.DrawRow(row, y);
            }

            if (info.CursorVisible && (_blinkOn || !CursorBlink || !IsFocused))
            {
                var cursorX = Math.Clamp(info.CursorColumn, 0, columns - 1);
                var cursorY = Math.Clamp(info.CursorRow, 0, rows - 1);
                renderer.DrawCursor(_frame[cursorX, cursorY], cursorX, cursorY, info.CursorShape, IsFocused);
            }
        }
    }

    // Bottom-right, so it stays clear of Avalonia's own overlays at the top.
    private void DrawDiagnostics(DrawingContext context)
    {
        var cells = _frame.Columns * _frame.Rows;
        var text = RenderResources.CreateText(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{_frame.Columns} x {_frame.Rows} = {cells:N0} cells\nDraw {_statistics.AverageMilliseconds:F1} ms avg, {_statistics.MaxMilliseconds:F1} ms max\n{_statistics.RendersPerSecond:F1} renders/s"),
            FontFamily,
            12,
            Brushes.White,
            bold: false,
            italic: false);

        var box = new Rect(Bounds.Width - text.Width - 22, Bounds.Height - text.Height - 20, text.Width + 12, text.Height + 8);
        context.FillRectangle(new ImmutableSolidColorBrush(Color.FromArgb(210, 0, 0, 0)), box);
        context.DrawText(text, new Point(box.X + 6, box.Y + 4));
    }

    private void ApplyRendererOverlays()
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.RendererDiagnostics.DebugOverlays = ShowDiagnostics
                ? RendererDebugOverlays.Fps | RendererDebugOverlays.RenderTimeGraph
                : RendererDebugOverlays.None;
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

    // An event handler cannot await, so this is async void; it must not throw.
    private async void PasteFromClipboard()
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            var text = clipboard is null ? null : await clipboard.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                Canvas.ScrollbackOffset = 0;
                RestartBlink();
                Canvas.Input.Post(new PasteEvent(text.ReplaceLineEndings("\n")));
            }
        }
        catch (Exception)
        {
            // Another process can hold the clipboard; a paste that cannot be read simply does nothing.
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
