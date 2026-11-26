using AsciiCanvas.Avalonia;
using AsciiCanvas.ConsolePort;

// The window is the only new code. ClassicProgram is an ordinary console program that knows nothing about the canvas.
return CanvasApp.RunTerminal(
    _ =>
    {
        ClassicProgram.Run();
        return Task.CompletedTask;
    },
    new CanvasAppOptions
    {
        Title = "Console program on a canvas",
        Columns = 100,
        Rows = 32,
        Sizing = CanvasSizing.FillWithCells,
        FontSize = 16,
        CloseWindowWhenProgramEnds = true,
    },
    args);
