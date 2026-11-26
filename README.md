# AsciiCanvas

A grid of character cells for building ASCII user interfaces in .NET 10 / C# 14. It is not a terminal emulator:
there are no escape sequences or processes, just a canvas programs draw on and an input stream they read from.

| Project | Purpose |
|---|---|
| `src/AsciiCanvas.Core` | UI-independent model: `CellCanvas` (drawing, cursor, `CanvasInput`), `TextTerminal` (console-like output and `ReadLineAsync`), colors, box drawing, layout math |
| `src/AsciiCanvas.Avalonia` | `CanvasView` control and `CanvasApp` (one-call window host) built on Avalonia |
| `samples/AsciiCanvas.Demo` | Demo with colors, boxes, a terminal, mouse paint, Unicode and a stress test |
| `samples/AsciiCanvas.Editor` | A cross-platform text editor drawn on the canvas like a TUI: menu bar, file browser, dialogs, selection, clipboard, undo; builds as a single executable |
| `tests/AsciiCanvas.Core.Tests` | xUnit v3 tests for the core |
| `tests/AsciiCanvas.Editor.Tests` | xUnit v3 tests for the editor's text model, file browser and menu |

```csharp
CanvasApp.Run(async canvas =>
{
    canvas.Box(canvas.Bounds, BoxStyle.DoubleLine, title: "Hello");
    canvas.Write(2, 2, "Press a key", new CellStyle(CellColor.BrightGreen));
    canvas.Present();

    await canvas.Input.ReadAsync(canvas.Closed);
});
```

**Building a program on AsciiCanvas? Start with the [usage guide](docs/USAGE.md):** the drawing methods, input events, closing,
sizing, console-style programs, hosting `CanvasView` in your own Avalonia app, and testing without a window.

* **Drawing** goes to a back buffer; `Present()` publishes it, so partial frames are never shown. All members are thread-safe.
* **Window title and icon**: `CanvasAppOptions.Title` and `CanvasAppOptions.Icon` (PNG or ICO bytes) set the initial ones, and `canvas.Title = "..."` / `canvas.Icon = bytes` change them at any time. On Windows, `<ApplicationIcon>` in the program's project also sets the window icon when no `Icon` is given.
* **Input** arrives as `KeyEvent`, `TextEvent` (layout-translated text), `PasteEvent`, `MouseEvent` (cell coordinates), `ResizeEvent` and `CloseRequestedEvent`.
* **Resize and close events**: `ResizeEvent` is sent whenever the grid changes size, which with `CanvasSizing.FillWithCells` happens whenever the window is resized (with `ScaleToFit` the grid keeps its size, so there is nothing to report). `CloseRequestedEvent` is sent when the user tries to close the window, but only if the program has set `canvas.InterceptClose = true`; the window then stays open until the program calls `canvas.Exit()`, so it can ask about unsaved changes first. A program that sets `InterceptClose` must keep reading input.
* **Paste and clipboard**: Ctrl+V, Cmd+V and Shift+Insert make the host read the clipboard and send the text as one `PasteEvent` (multi-line text included, line breaks as `\n`); set `CanvasView.InterceptPasteShortcut` / `CanvasAppOptions.InterceptPasteShortcut` to false to get the raw key press instead. Programs copy and cut through `canvas.Clipboard` (`ICanvasClipboard`: `GetTextAsync`, `SetTextAsync`), which does not depend on the UI framework. `KeyModifiers.Meta` is the Command key, so a Mac program can accept `Control` or `Meta` for its shortcuts. `TextTerminal.ReadLineAsync` accepts pasted text.
* **Scaling**: `CanvasSizing.ScaleToFit` keeps the column/row count and scales cells and font; `CanvasSizing.FillWithCells` keeps the cell size and changes the column/row count (the program receives a `ResizeEvent`). Use `canvas.Resize(columns, rows)` to change the grid size from the program.
* **Colors** are the 16/256 palette (`CellColor`), mapped to RGB by a `ColorPalette` (`Campbell` by default).
* **Fonts**: two monospaced fonts are embedded in `AsciiCanvas.Avalonia` (SIL OFL), so the canvas looks the same on every platform: Cascadia Mono (default) and JetBrains Mono. Choose with `CanvasFonts.CascadiaMono` / `CanvasFonts.JetBrainsMono` on `CanvasView.FontFamily` or `CanvasAppOptions.FontFamily`, or set any other monospaced font. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
* **Text**: one rune per cell; East Asian wide characters and emoji take two cells. Box-drawing and block characters are painted procedurally, so they join without gaps at any scale.

Run the demo with `dotnet run --project samples/AsciiCanvas.Demo` (add `-- --fill` for fixed cell size, `-- --jetbrains` for JetBrains Mono).

## Text editor demo

`samples/AsciiCanvas.Editor` shows the canvas used for a real tool, drawn entirely with cells, like a text-mode (TUI) program. No native controls are involved: the window holds one canvas.

* **Menu bar** (click, or F10 / Alt+F / Alt+E): File (New, Open..., Save, Save As..., Exit) and Edit (Undo, Redo, Cut, Copy, Paste, Select All), with shortcuts shown and unavailable entries dimmed.
* **File browser** for Open and Save As: the folder, a scrollable list of folders and files with sizes, a name field and buttons. Click an entry to select it and click again to open it, or use the arrow keys, Enter, typing and Backspace (up a folder). On Windows, going up from a drive root lists the drives. Open accepts text files only: a file with binary data, or over 32 MB, is refused with a message.
* **Editing**: line numbers, scroll bar, selection (Shift+arrows, mouse drag), word and document movement, auto-indent, tabs, wide characters, undo and redo, and copy, cut and paste through the system clipboard. Saving keeps the file's line endings.
* **Closing**: the editor sets `canvas.InterceptClose`, so the window's close button, Exit and Ctrl+Q all ask "Save changes?" when there are unsaved changes, and the editor then calls `canvas.Exit()`.

```powershell
dotnet run --project samples/AsciiCanvas.Editor -- notes.txt
```

Shortcuts: Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S, Ctrl+Q (Cmd on a Mac), Ctrl+Z / Ctrl+Y, Ctrl+X / C / V, Ctrl+A, and F3 for the performance overlay.

### As a single executable

```powershell
dotnet publish samples/AsciiCanvas.Editor -c Release -r win-x64
```

This produces one self-contained file (`AsciiCanvas.Editor.exe`, about 50 MB with the .NET runtime, Avalonia, Skia and the embedded fonts) that runs on a machine without .NET installed. Use `-r osx-arm64` or `-r linux-x64` for the other platforms; only the Windows build has been tried.

The native graphics libraries (Skia, HarfBuzz, ANGLE) cannot be loaded from inside the file, so on first start they are unpacked to a cache folder (`%TEMP%\.net\AsciiCanvas.Editor\…` on Windows) and reused after that. To keep them next to the executable instead, so nothing is written outside the program's folder:

```powershell
dotnet publish samples/AsciiCanvas.Editor -c Release -r win-x64 -p:IncludeNativeLibrariesForSelfExtract=false
```

| | Single file (default) | Native libraries beside the exe |
|---|---|---|
| Files | `AsciiCanvas.Editor.exe` (50 MB) | `AsciiCanvas.Editor.exe` (42 MB) + `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll` (19 MB) |
| Writes to temp | Yes, once (19 MB) | No |
| First start (measured) | 1.6 s | 1.1 s |

With the second form the DLLs must stay in the same folder as the executable (on macOS and Linux they are `.dylib` and `.so` files).

The editor is built with `InvariantGlobalization`, so it does not use ICU (the `libicu` package on Linux, `icu.dll` on Windows). Nothing in the editor or the library depends on culture rules: Avalonia brings its own Unicode data and HarfBuzz does the text shaping. On Linux the executable still needs glibc (or a `linux-musl-x64` build), the X11 libraries and fontconfig, which a desktop system already has. Only Windows has been tried.

### Structure

The text logic (`EditorModel`, `TextBuffer`, `FileBrowserModel`, `TextFile`, `MenuBar`) knows nothing about the screen, so it is unit tested on its own. `EditorView` draws the text, `Tui/` holds the menu loop, dialogs and file dialog, and `EditorApp` connects them to the canvas input.

Not done: combining characters are separate cursor stops, and there is no search.

## Diagnostics and stress test

`CanvasView.ShowDiagnostics` (or `CanvasAppOptions.ShowDiagnostics`) draws performance diagnostics over the grid: Avalonia's frame rate and render-time graphs, plus an overlay in the bottom-right corner with the grid size, the cell count, and how long the control takes to draw it. It is off by default. With `AllowDiagnosticsToggle` set, **F3** switches it on and off at run time, and the key is not passed on to the program.

The demo's menu item **6, Stress test**, rewrites every cell with new text, colors and box/block characters each frame. Choose the number of cells with the window size and the font size:

```powershell
dotnet run -c Release --project samples/AsciiCanvas.Demo -- --fill --font-size=10 --diagnostics
```

Measured in a Release build with every cell changing every frame (an extreme case; a normal UI changes a few cells and costs one draw per `Present`). The draw time is how long the control spends recording the frame:

| Grid | Cells | Draw time, per-cell text (before) | Draw time, glyph runs (now) | Renders/s now |
|---|---|---|---|---|
| 100 x 29 | 2,900 | 36 ms | 1.1 ms | 58 (the stress loop's own limit) |
| 158 x 46 | 7,268 | 85 ms | 3.2 ms | 30 |
| 211 x 65 | 13,715 | 193 ms | 6.7 ms | 28 |
| 317 x 92 | 29,164 | 371 ms | 17 ms | 18 |

Neighbouring cells with the same color and font style are drawn as one glyph run, so the cost is about 0.6 µs per cell instead of 10 µs or more. Box-drawing, block, wide and fallback-font characters are still drawn one cell at a time. At the largest sizes the frame rate is now limited by Avalonia's own render thread (about 30 ms for 29,000 cells), not by the control. A screen that does not change costs nothing: no draw happens and the process is idle.

## Development

The coding rules come from the [CSharp Coding Kit](https://github.com/artstisen/csharp-coding-kit), installed in `.claude/` and enforced by `.editorconfig` and the analyzers at build time. See `CLAUDE.md` for the project conventions.

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet pack -c Release -o artifacts   # AsciiCanvas.Core and AsciiCanvas.Avalonia packages (artifacts/ is not committed)
```
