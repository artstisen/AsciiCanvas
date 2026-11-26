using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

public sealed class CanvasAppOptions
{
    public string Title { get; set; } = "AsciiCanvas";

    public int Columns { get; set; } = 80;

    public int Rows { get; set; } = 25;

    public CanvasSizing Sizing { get; set; } = CanvasSizing.ScaleToFit;

    public bool PreserveAspectRatio { get; set; } = true;

    public double FontSize { get; set; } = 18;

    public FontFamily? FontFamily { get; set; }

    public ColorPalette Palette { get; set; } = ColorPalette.Campbell;

    /// <summary>Close the window as soon as the program returns. When false the final screen stays until the user closes the window.</summary>
    public bool CloseWindowWhenProgramEnds { get; set; }
}

/// <summary>Runs a program against a canvas in its own window.</summary>
public static class CanvasApp
{
    /// <summary>
    /// Opens a window and runs <paramref name="program"/> on a background thread with the window's canvas.
    /// Blocks until the window closes. The program should stop when <see cref="CellCanvas.Closed"/> is canceled;
    /// reading from <see cref="CellCanvas.Input"/> already throws <see cref="OperationCanceledException"/> then.
    /// </summary>
    public static int Run(Func<CellCanvas, Task> program, CanvasAppOptions? options = null, string[]? args = null)
    {
        Host.Program = program;
        Host.Options = options ?? new CanvasAppOptions();
        Host.Failure = null;

        var exitCode = AppBuilder.Configure<Host>().UsePlatformDetect().StartWithClassicDesktopLifetime(args ?? []);

        Host.Failure?.Throw();
        return exitCode;
    }

    /// <summary>Like <see cref="Run(Func{CellCanvas, Task}, CanvasAppOptions?, string[]?)"/> but hands the program a console-like <see cref="TextTerminal"/>.</summary>
    public static int RunTerminal(Func<TextTerminal, Task> program, CanvasAppOptions? options = null, string[]? args = null) =>
        Run(canvas => program(new TextTerminal(canvas)), options, args);

    private sealed class Host : Application
    {
        public static Func<CellCanvas, Task> Program { get; set; } = _ => Task.CompletedTask;

        public static CanvasAppOptions Options { get; set; } = new();

        public static ExceptionDispatchInfo? Failure { get; set; }

        public override void Initialize() => Styles.Add(new FluentTheme());

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var options = Options;
                var canvas = new CellCanvas(options.Columns, options.Rows);
                var view = new CanvasView
                {
                    Canvas = canvas,
                    Sizing = options.Sizing,
                    PreserveAspectRatio = options.PreserveAspectRatio,
                    FontSize = options.FontSize,
                    Palette = options.Palette,
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
                    Background = new SolidColorBrush(Color.FromUInt32(0xFF000000 | options.Palette.DefaultBackground)),
                };
                window.Closed += (_, _) => canvas.Close();
                desktop.MainWindow = window;

                _ = Task.Run(() => RunProgramAsync(canvas, window, options));
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task RunProgramAsync(CellCanvas canvas, Window window, CanvasAppOptions options)
        {
            try
            {
                await Program(canvas).ConfigureAwait(false);
                canvas.Present();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Failure = ExceptionDispatchInfo.Capture(ex);
                options.CloseWindowWhenProgramEnds = true;
            }

            if (options.CloseWindowWhenProgramEnds)
            {
                Dispatcher.UIThread.Post(window.Close);
            }
        }
    }
}
