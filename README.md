# AsciiCanvas

A grid of character cells for building ASCII user interfaces in .NET 10 / C# 14. It is not a terminal emulator:
there are no escape sequences or processes, just a canvas programs draw on and an input stream they read from.

| Project | Purpose |
|---|---|
| `src/AsciiCanvas.Core` | UI-independent model: `CellCanvas` (drawing, cursor, `CanvasInput`), `TextTerminal` (console-like output and `ReadLineAsync`), colors, box drawing, layout math |
| `src/AsciiCanvas.Avalonia` | `CanvasView` control and `CanvasApp` (one-call window host) built on Avalonia |
| `samples/AsciiCanvas.Demo` | Demo with colors, boxes, a terminal, mouse paint and Unicode screens |
| `tests/AsciiCanvas.Core.Tests` | xUnit v3 tests for the core |

```csharp
CanvasApp.Run(async canvas =>
{
    canvas.Box(canvas.Bounds, BoxStyle.DoubleLine, title: "Hello");
    canvas.Write(2, 2, "Press a key", new CellStyle(CellColor.BrightGreen));
    canvas.Present();

    await canvas.Input.ReadAsync(canvas.Closed);
});
```

* **Drawing** goes to a back buffer; `Present()` publishes it, so partial frames are never shown. All members are thread-safe.
* **Input** arrives as `KeyEvent`, `TextEvent` (layout-translated text), `MouseEvent` (cell coordinates) and `ResizeEvent`.
* **Scaling**: `CanvasSizing.ScaleToFit` keeps the column/row count and scales cells and font; `CanvasSizing.FillWithCells` keeps the cell size and changes the column/row count (the program receives a `ResizeEvent`). Use `canvas.Resize(columns, rows)` to change the grid size from the program.
* **Colors** are the 16/256 palette (`CellColor`), mapped to RGB by a `ColorPalette` (`Campbell` by default).
* **Fonts**: two monospaced fonts are embedded in `AsciiCanvas.Avalonia` (SIL OFL), so the canvas looks the same on every platform: Cascadia Mono (default) and JetBrains Mono. Choose with `CanvasFonts.CascadiaMono` / `CanvasFonts.JetBrainsMono` on `CanvasView.FontFamily` or `CanvasAppOptions.FontFamily`, or set any other monospaced font. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
* **Text**: one rune per cell; East Asian wide characters and emoji take two cells. Box-drawing and block characters are painted procedurally, so they join without gaps at any scale.

Run the demo with `dotnet run --project samples/AsciiCanvas.Demo` (add `-- --fill` for fixed cell size, `-- --jetbrains` for JetBrains Mono).

## Diagnostics and stress test

`CanvasView.ShowDiagnostics` (or `CanvasAppOptions.ShowDiagnostics`) draws performance diagnostics over the grid: Avalonia's frame rate and render-time graphs, plus an overlay in the bottom-right corner with the grid size, the cell count, and how long the control takes to draw it. It is off by default. With `AllowDiagnosticsToggle` set, **F3** switches it on and off at run time, and the key is not passed on to the program.

The demo's menu item **6, Stress test**, rewrites every cell with new text, colors and box/block characters each frame. Choose the number of cells with the window size and the font size:

```powershell
dotnet run -c Release --project samples/AsciiCanvas.Demo -- --fill --font-size=10 --diagnostics
```

Measured in a Release build with every cell changing every frame (an extreme case; a normal UI changes a few cells and costs one draw per `Present`):

| Grid | Cells | Draw time | Renders/s |
|---|---|---|---|
| 80 x 30 | 2,400 | 23 ms | 23 |
| 173 x 53 | 9,169 | 90 ms | 9 |
| 211 x 65 | 13,715 | 192 ms | 4.5 |
| 317 x 92 | 29,164 | 326 ms | 2.9 |

That is about 10 µs per cell and per draw. A screen that does not change costs nothing: no draw happens and the process is idle.

## Development

The coding rules come from the [CSharp Coding Kit](https://github.com/artstisen/csharp-coding-kit), installed in `.claude/` and enforced by `.editorconfig` and the analyzers at build time. See `CLAUDE.md` for the project conventions.

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```
