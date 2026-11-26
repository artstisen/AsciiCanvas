using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Avalonia;

namespace AsciiCanvas.Avalonia;

/// <summary>Runs a program against a canvas in its own window.</summary>
public static class CanvasApp
{
    /// <summary>The scrollback rows that <see cref="RunTerminal"/> keeps when <see cref="CanvasAppOptions.ScrollbackLines"/> is not set.</summary>
    public const int DefaultTerminalScrollbackLines = 1000;

    /// <summary>
    /// Opens a window and runs <paramref name="program"/> on a background thread with the window's canvas.
    /// Blocks until the window closes. The program should stop when <see cref="CellCanvas.Closed"/> is canceled;
    /// reading from <see cref="CellCanvas.Input"/> already throws <see cref="OperationCanceledException"/> then.
    /// </summary>
    /// <remarks>On Windows the window runs on a single-threaded apartment thread, which the clipboard needs. This works without <c>[STAThread]</c>, so top-level statements are fine.</remarks>
    /// <param name="program">The program to run.</param>
    /// <param name="options">Window and canvas settings; defaults are used if <see langword="null"/>.</param>
    /// <param name="args">Command-line arguments passed to Avalonia.</param>
    /// <returns>The exit code of the application.</returns>
    /// <exception cref="Exception">The program threw; the exception is rethrown after the window closes.</exception>
    public static int Run(Func<CellCanvas, Task> program, CanvasAppOptions? options = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        var settings = options ?? new CanvasAppOptions();
        var arguments = args ?? [];
        CanvasHostApplication? application = null;

        // Avalonia ties its objects to the thread that first uses them, so the application must be created on the thread that runs the window.
        int StartWindow()
        {
            application = new CanvasHostApplication(program, settings);
            return AppBuilder.Configure(() => application).UsePlatformDetect().StartWithClassicDesktopLifetime(arguments);
        }

        var exitCode = OperatingSystem.IsWindows() && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA
            ? RunOnStaThread(StartWindow)
            : StartWindow();

        application?.Shutdown();
        return exitCode;
    }

    /// <summary>
    /// Like <see cref="Run"/> but hands the program a console-like <see cref="TextTerminal"/>. The terminal is also attached to
    /// <see cref="TerminalConsole"/> while the program runs, so code that calls <c>Console</c> through an alias works unchanged.
    /// Unless <see cref="CanvasAppOptions.ScrollbackLines"/> says otherwise, the window keeps <see cref="DefaultTerminalScrollbackLines"/> rows of scrollback.
    /// </summary>
    /// <param name="program">The program to run.</param>
    /// <param name="options">Window and canvas settings; defaults are used if <see langword="null"/>.</param>
    /// <param name="args">Command-line arguments passed to Avalonia.</param>
    /// <returns>The exit code of the application.</returns>
    public static int RunTerminal(Func<TextTerminal, Task> program, CanvasAppOptions? options = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        return Run(
            async canvas =>
            {
                if (options?.ScrollbackLines is null)
                {
                    canvas.ScrollbackLimit = DefaultTerminalScrollbackLines;
                }

                var terminal = new TextTerminal(canvas);
                TerminalConsole.Attach(terminal);
                try
                {
                    await program(terminal).ConfigureAwait(false);
                }
                finally
                {
                    TerminalConsole.Detach(terminal);
                }
            },
            options,
            args);
    }

    [SupportedOSPlatform("windows")]
    private static int RunOnStaThread(Func<int> run)
    {
        var exitCode = 0;
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                exitCode = run();
            }
            catch (Exception ex)
            {
                // Rethrown on the calling thread below, so the caller sees the failure as if it had run the window itself.
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
        return exitCode;
    }
}
