using AsciiCanvas.Avalonia;
using AsciiCanvas.Editor;

// Usage: AsciiCanvas.Editor [--frameless] [file]
var path = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
var frameless = args.Contains("--frameless");

return CanvasApp.Run(
    canvas => EditorApp.RunAsync(canvas, path, frameless),
    new CanvasAppOptions
    {
        Title = "AsciiCanvas Editor",
        Columns = 100,
        Rows = 32,
        Sizing = CanvasSizing.FillWithCells,
        FontSize = 16,
        Frame = frameless ? CanvasFrame.None : CanvasFrame.System,
        AllowDiagnosticsToggle = true,
    },
    args);
