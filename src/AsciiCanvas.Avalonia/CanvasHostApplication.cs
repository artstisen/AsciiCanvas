using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

/// <summary>The Avalonia application behind <see cref="CanvasApp"/>: one window showing a canvas, and the program running against it.</summary>
internal sealed class CanvasHostApplication(Func<CellCanvas, Task> program, CanvasAppOptions options) : Application
{
    private static readonly TimeSpan _programShutdownTimeout = TimeSpan.FromSeconds(2);

    private const string DefaultIconUri = "avares://AsciiCanvas.Avalonia/Assets/Icons/AsciiCanvas.png";

    private WindowIcon? _defaultIcon;

    private CellCanvas? _canvas;
    private Task? _programTask;
    private ExceptionDispatchInfo? _failure;
    private bool _allowClose;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var canvas = new CellCanvas(options.Columns, options.Rows);
            var view = new CanvasView
            {
                Canvas = canvas,
                Sizing = options.Sizing,
                PreserveAspectRatio = options.PreserveAspectRatio,
                FontSize = options.FontSize,
                Palette = options.Palette,
                ShowDiagnostics = options.ShowDiagnostics,
                AllowDiagnosticsToggle = options.AllowDiagnosticsToggle,
                InterceptPasteShortcut = options.InterceptPasteShortcut,
            };

            if (options.FontFamily is { } family)
            {
                view.FontFamily = family;
            }

            var window = new Window
            {
                Title = options.Title,
                Content = view,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowDecorations = options.Frame == CanvasFrame.None ? WindowDecorations.None : WindowDecorations.Full,
                Background = new SolidColorBrush(Color.FromUInt32(0xFF000000 | options.Palette.DefaultBackground)),
            };
            window.Closing += (_, e) =>
            {
                // A program that intercepts closing answers the request itself, by calling Exit.
                if (!Volatile.Read(ref _allowClose) && canvas.InterceptClose)
                {
                    e.Cancel = true;
                    canvas.Input.Post(new CloseRequestedEvent());
                }
            };
            window.Closed += (_, _) => canvas.Close();
            canvas.Title = options.Title;
            canvas.TitleChanged += title => Dispatcher.UIThread.Post(() => window.Title = title);
            canvas.Icon = options.Icon;
            canvas.IconChanged += () => Dispatcher.UIThread.Post(() => ApplyIcon(window, canvas.Icon));
            ApplyIcon(window, canvas.Icon);
            canvas.WindowStateRequested += state => Dispatcher.UIThread.Post(() => window.WindowState = ToWindowState(state));
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty)
                {
                    canvas.ReportWindowState(FromWindowState(window.WindowState));
                }
            };
            canvas.ExitRequested += () => Dispatcher.UIThread.Post(() => CloseWindow(window));
            desktop.MainWindow = window;

            _canvas = canvas;

            // The program is user code that may block, so it must not run on the UI thread.
            _programTask = Task.Run(() => RunProgramAsync(canvas, window));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Waits briefly for the program to notice that the window closed, releases the canvas and rethrows a failure of the program.</summary>
    public void Shutdown()
    {
        // This is the synchronous end of CanvasApp.Run; the program was already told to stop through the canvas.
        _programTask?.Wait(_programShutdownTimeout);
        _canvas?.Dispose();
        Volatile.Read(ref _failure)?.Throw();
    }

    private async Task RunProgramAsync(CellCanvas canvas, Window window)
    {
        var failed = false;
        try
        {
            await program(canvas).ConfigureAwait(false);
            canvas.Present();
        }
        catch (OperationCanceledException) when (canvas.Closed.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Outermost boundary of the program: the failure is rethrown from CanvasApp.Run after the window closes.
            Volatile.Write(ref _failure, ExceptionDispatchInfo.Capture(ex));
            failed = true;
        }

        // Nobody is left to answer a close request, so from here on the window may close freely.
        Volatile.Write(ref _allowClose, true);
        if (failed || options.CloseWindowWhenProgramEnds)
        {
            Dispatcher.UIThread.Post(() => CloseWindow(window));
        }
    }

    private static WindowState ToWindowState(CanvasWindowState state) => state switch
    {
        CanvasWindowState.Minimized => WindowState.Minimized,
        CanvasWindowState.Maximized => WindowState.Maximized,
        _ => WindowState.Normal,
    };

    private static CanvasWindowState FromWindowState(WindowState state) => state switch
    {
        WindowState.Minimized => CanvasWindowState.Minimized,
        WindowState.Maximized or WindowState.FullScreen => CanvasWindowState.Maximized,
        _ => CanvasWindowState.Normal,
    };

    private void ApplyIcon(Window window, ReadOnlyMemory<byte> image)
    {
        if (image.IsEmpty)
        {
            window.Icon = options.UseExecutableIcon ? null : _defaultIcon ??= LoadDefaultIcon();
            return;
        }

        try
        {
            using var stream = new MemoryStream(image.ToArray());
            window.Icon = new WindowIcon(stream);
        }
        catch (Exception)
        {
            // An image the platform cannot decode must not take the window down; the current icon stays.
        }
    }

    private static WindowIcon LoadDefaultIcon()
    {
        using var stream = AssetLoader.Open(new Uri(DefaultIconUri));
        return new WindowIcon(stream);
    }

    private void CloseWindow(Window window)
    {
        Volatile.Write(ref _allowClose, true);
        window.Close();
    }
}
