using AsciiCanvas.Avalonia;
using AsciiCanvas.Editor;

// Usage: AsciiCanvas.Editor [file]
var path = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

return CanvasApp.Run(
    canvas => EditorApp.RunAsync(canvas, path),
    new CanvasAppOptions
    {
        Title = "AsciiCanvas Editor",
        Columns = 100,
        Rows = 32,
        Sizing = CanvasSizing.FillWithCells,
        FontSize = 16,
        AllowDiagnosticsToggle = true,
        CloseWindowWhenProgramEnds = true,
    },
    args);
