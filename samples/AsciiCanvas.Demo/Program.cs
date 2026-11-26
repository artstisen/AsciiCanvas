using System.Globalization;
using AsciiCanvas.Avalonia;
using AsciiCanvas.Demo;

var fill = args.Contains("--fill");
var jetBrains = args.Contains("--jetbrains");
var diagnostics = args.Contains("--diagnostics");

// --font-size=8 gives smaller cells, and so more of them in the same window.
var fontSize = args
    .Where(a => a.StartsWith("--font-size=", StringComparison.Ordinal))
    .Select(a => double.Parse(a["--font-size=".Length..], CultureInfo.InvariantCulture))
    .DefaultIfEmpty(18)
    .Last();

return CanvasApp.Run(
    DemoApp.RunAsync,
    new CanvasAppOptions
    {
        Title = $"AsciiCanvas demo ({(fill ? "fixed cell size" : "scaled grid")}, {(jetBrains ? "JetBrains Mono" : "Cascadia Mono")})",
        Columns = 80,
        Rows = 30,
        Sizing = fill ? CanvasSizing.FillWithCells : CanvasSizing.ScaleToFit,
        FontSize = fontSize,
        FontFamily = jetBrains ? CanvasFonts.JetBrainsMono : CanvasFonts.CascadiaMono,
        ShowDiagnostics = diagnostics,
        AllowDiagnosticsToggle = true,
        CloseWindowWhenProgramEnds = true,
    },
    args);
