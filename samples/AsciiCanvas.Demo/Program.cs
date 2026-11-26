using AsciiCanvas.Avalonia;
using AsciiCanvas.Demo;

var fill = args.Contains("--fill");
var jetBrains = args.Contains("--jetbrains");

return CanvasApp.Run(
    DemoApp.RunAsync,
    new CanvasAppOptions
    {
        Title = $"AsciiCanvas demo ({(fill ? "fixed cell size" : "scaled grid")}, {(jetBrains ? "JetBrains Mono" : "Cascadia Mono")})",
        Columns = 80,
        Rows = 30,
        Sizing = fill ? CanvasSizing.FillWithCells : CanvasSizing.ScaleToFit,
        FontFamily = jetBrains ? CanvasFonts.JetBrainsMono : CanvasFonts.CascadiaMono,
        CloseWindowWhenProgramEnds = true,
    },
    args);
