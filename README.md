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
* **Font**: Cascadia Mono (SIL OFL) is embedded in `AsciiCanvas.Avalonia`, so the canvas looks the same on every platform. Set `CanvasView.FontFamily` or `CanvasAppOptions.FontFamily` to use another monospaced font. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
* **Text**: one rune per cell; East Asian wide characters and emoji take two cells. Box-drawing and block characters are painted procedurally, so they join without gaps at any scale.

Run the demo with `dotnet run --project samples/AsciiCanvas.Demo` (add `-- --fill` for fixed cell size).

## Development

The coding rules come from the [CSharp Coding Kit](https://github.com/artstisen/csharp-coding-kit), installed in `.claude/` and enforced by `.editorconfig` and the analyzers at build time. See `CLAUDE.md` for the project conventions.

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```
