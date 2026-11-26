using Avalonia;

namespace AsciiCanvas.Avalonia;

/// <summary>Runs a program against a canvas in its own window.</summary>
public static class CanvasApp
{
    /// <summary>
    /// Opens a window and runs <paramref name="program"/> on a background thread with the window's canvas.
    /// Blocks until the window closes. The program should stop when <see cref="CellCanvas.Closed"/> is canceled;
    /// reading from <see cref="CellCanvas.Input"/> already throws <see cref="OperationCanceledException"/> then.
    /// </summary>
    /// <param name="program">The program to run.</param>
    /// <param name="options">Window and canvas settings; defaults are used if <see langword="null"/>.</param>
    /// <param name="args">Command-line arguments passed to Avalonia.</param>
    /// <returns>The exit code of the application.</returns>
    /// <exception cref="Exception">The program threw; the exception is rethrown after the window closes.</exception>
    public static int Run(Func<CellCanvas, Task> program, CanvasAppOptions? options = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        var application = new CanvasHostApplication(program, options ?? new CanvasAppOptions());
        var exitCode = AppBuilder.Configure(() => application).UsePlatformDetect().StartWithClassicDesktopLifetime(args ?? []);

        application.Shutdown();
        return exitCode;
    }

    /// <summary>Like <see cref="Run"/> but hands the program a console-like <see cref="TextTerminal"/>.</summary>
    /// <param name="program">The program to run.</param>
    /// <param name="options">Window and canvas settings; defaults are used if <see langword="null"/>.</param>
    /// <param name="args">Command-line arguments passed to Avalonia.</param>
    /// <returns>The exit code of the application.</returns>
    public static int RunTerminal(Func<TextTerminal, Task> program, CanvasAppOptions? options = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        return Run(canvas => program(new TextTerminal(canvas)), options, args);
    }
}
