using AsciiCanvas;
using AsciiCanvas.Avalonia;
using AsciiCanvas.Demo;

var fill = args.Contains("--fill");

return CanvasApp.Run(
    DemoApp.RunAsync,
    new CanvasAppOptions
    {
        Title = fill ? "AsciiCanvas demo (fixed cell size)" : "AsciiCanvas demo (scaled grid)",
        Columns = 80,
        Rows = 30,
        Sizing = fill ? CanvasSizing.FillWithCells : CanvasSizing.ScaleToFit,
        CloseWindowWhenProgramEnds = true,
    },
    args);
