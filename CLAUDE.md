# AsciiCanvas

> Project memory for Claude Code.
> Shared .NET rules are loaded automatically from `.claude/rules/csharp-coding-kit/` (installed with `install.ps1` from https://github.com/artstisen/csharp-coding-kit, version in `.claude/csharp-coding-kit/kit.json`). Don't edit them; put project-specific decisions here.

## Project context

- **What it is:** A grid of character cells for building ASCII-based UIs. Not a terminal emulator: no escape sequences, no processes. Programs draw on a `CellCanvas` and read input from its `CanvasInput`.
- **Type:** Class libraries (`AsciiCanvas.Core`, `AsciiCanvas.Avalonia`) plus a demo app and tests.
- **Target:** net10.0 / C# 14
- **Architecture:** `Core` has no UI dependency (headless testable); `Avalonia` is a thin host (`CanvasView` control, `CanvasApp` window runner). Other hosts can be added next to it.
- **Database:** none

## Solution layout

```
src/
  AsciiCanvas.Core/        cells, canvas, drawing, input events, TextTerminal, layout math
  AsciiCanvas.Avalonia/    CanvasView control, CanvasApp host, input mapping
samples/
  AsciiCanvas.Demo/        demo app (run with --fill for fixed cell size)
  AsciiCanvas.Editor/      text editor demo (TUI: menu bar, file browser, dialogs): EditorModel (pure text logic), EditorView, EditorApp, Tui/
tests/
  AsciiCanvas.Core.Tests/    xUnit v3
  AsciiCanvas.Editor.Tests/  xUnit v3: text model, file browser, text file reading, menu geometry
```

## Commands

```powershell
dotnet build
dotnet test                              # xUnit v3 runs on Microsoft.Testing.Platform (opted in via global.json)
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
dotnet run --project samples/AsciiCanvas.Demo
dotnet tool restore                      # once: installs the pinned ReSharper command-line tools
dotnet jb inspectcode AsciiCanvas.slnx --no-build --format=Xml --severity=WARNING --output=inspect.xml   # build first; do not commit inspect.xml
```

## Style enforcement

- `.editorconfig` turns the kit's style rules into build errors (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel` latest-recommended). Fix the code instead of lowering a severity.
- `dotnet format --verify-no-changes` also checks naming; run it before committing.
- Packages are versioned in `Directory.Packages.props` only.
- `Core` is `IsAotCompatible`: no unguarded reflection there.
- **Accepted ReSharper findings** (ReSharper sees only this solution, so it misjudges public API): `UnusedMember.Global`, `MemberCanBePrivate.Global`, `PropertyCanBeMadeInitOnly.Global` on `CanvasView`, `NotAccessedPositionalProperty` on the event records and `GlyphKey` (a dictionary key), `UnusedMethodReturnValue` on `CellGrid.Put`, `RedundantArgumentDefaultValue` where the default is stated on purpose, `RedundantAssignment` on `color` in the paint demo (a local function reads it), and `MethodHasAsyncOverload` on `TextWriter.WriteLine`. The `var` hints (`SuggestVarOrType_*`) follow ReSharper's defaults, not the kit. Anything else is worth fixing.

## Project conventions and overrides

- **Palette is 16/256 colors only** (`CellColor`), by decision. No RGB cells.
- **One rune per cell**; East Asian wide characters and emoji take two cells (`CellWidth.WideLead` / `WideTrail`). No grapheme clusters yet.
- **`CellCanvas` is thread-safe** (one lock, back buffer published by `Present()`); `CellGrid` is not.
- **Expected failures are not exceptions**: out-of-range drawing is clipped, not thrown. Exceptions are for contract violations (bad arguments).
- **Result pattern is not used**: the library has no operations with expected failure outcomes.
- **No `Console` use in the libraries**; `TextTerminal` is the console-like layer and exposes a `TextWriter` (`Out`).
- **`CancellationToken` parameters are named `cancellationToken`**, and library awaits use `ConfigureAwait(false)`.
- **Two fonts are embedded**: Cascadia Mono (default) and JetBrains Mono, both SIL OFL 1.1, under `src/AsciiCanvas.Avalonia/Assets/Fonts` and exposed through `CanvasFonts`. They are used through `avares://` URIs, and Avalonia resolves those families only from the embedded collection, so a wrong family name in `CanvasFonts` crashes at the first render. Keep each OFL text and `THIRD-PARTY-NOTICES.md` with the fonts; do not modify the font files.
- **`Key` enum members are not individually documented** (CS1591 suppressed for that enum only): the member names are the key names.
- **Not a NuGet package yet**, so package metadata (license, repository URL) is not set. Add it, with SourceLink and symbol packages, when publishing.

## Domain notes

- `Present()` copies the back buffer to the front buffer; hosts render from the front buffer via `CopyFrame`. Drawing without `Present()` is never shown.
- Box-drawing (`U+2500` block) and block elements (`U+2580`) are painted procedurally by the Avalonia host, not from font glyphs, so they join at any scale. `BoxDrawing` describes the line arms.
- `CanvasSizing.ScaleToFit` keeps columns/rows and scales cells; `FillWithCells` keeps cell size and changes columns/rows (posts a `ResizeEvent`).
- Typed text (`TextEvent`) and key presses (`KeyEvent`) are separate; a printable key press arrives as both. `TextTerminal.ReadKeyAsync` skips the key-down for printable keys and returns the text.
- **Diagnostics are a feature of the grid**: `CanvasView.ShowDiagnostics` shows Avalonia's FPS/render graphs plus our own overlay (cells, draw ms, renders/s, from `RenderStatistics`); `AllowDiagnosticsToggle` makes F3 switch it at run time. Keep both off by default and keep F3 opt-in, so programs receive every key.
- **The stress test is the benchmark** (demo menu item 6, run with `--fill --font-size=N`; measure in Release only). Baseline in the README: about 10 µs per cell per draw. Re-measure after changing `CellRenderer`, `RenderResources` or `CanvasView.Render`. Every `Present()` redraws the whole grid, so cost depends on grid size, not on how much changed. The glyph cache size is not the bottleneck (tested); the next candidate is drawing a run of cells as one glyph run instead of one text item per cell.
- **A blink must not redraw an invisible cursor**: the blink timer only invalidates when the last frame had a visible cursor and the control has focus (a hidden cursor used to cost 38% of a core on a 29,000-cell grid).
- **Paste and clipboard**: the host turns Ctrl+V, Cmd+V and Shift+Insert into a `PasteEvent` (`InterceptPasteShortcut`, default on); programs copy through `CellCanvas.Clipboard` (`ICanvasClipboard`), which can throw `ExternalException` when another program holds the clipboard, so callers catch it. `KeyModifiers.Meta` is Command; shortcuts should accept Control or Meta.
- **`CanvasApp.Run` runs the window on an STA thread on Windows**: the Windows clipboard throws `CoInitialize has not been called` otherwise, and top-level statements cannot carry `[STAThread]`. The `Application` object must be created on that same thread (Avalonia binds its dispatcher to the creating thread), which is why `Run` builds it inside the thread's start function.
- **The editor sample's model types are public** (`EditorModel`, `TextBuffer`, `TextMeasure`, `TextPosition`) only so `AsciiCanvas.Editor.Tests` can reach them without `InternalsVisibleTo`; the sample is not a library. Editing logic belongs in the model, drawing in `EditorView`, input handling in `EditorApp`.
- **Known editor limits**: combining characters are separate cursor stops, no search.
- **The editor is a TUI built on the canvas, with no native controls**: `MenuBar` (layout, hit-testing, drawing), `Tui/MenuLoop`, `Tui/MessageDialog` and `Tui/FileDialog` each run their own modal input loop and draw over the editor through `ModalHost.DrawBackground` (the editor drawn without presenting). `EditorApp.ExecuteAsync(EditorCommand)` is the one place where menu entries, shortcuts and close requests end up. Dialogs treat a `CloseRequestedEvent` as "cancel" and re-post it, so the editor's own handler runs. Colors for menus and dialogs are in `Tui/UiStyles`.
- **Closing**: `canvas.InterceptClose = true` makes the host cancel the window's close and send `CloseRequestedEvent`; the program calls `canvas.Exit()` to really close. The host lets the window close freely once the program has ended, so a finished program can never trap the window.
- **Single-file publish** of the editor: `dotnet publish samples/AsciiCanvas.Editor -c Release -r win-x64`. The csproj turns on self-contained single-file only when a runtime identifier is given (so builds and tests are unaffected) and deletes the `.pdb`/`.xml` side files after publishing. Do not enable trimming: Avalonia relies on reflection. Native libraries (Skia, HarfBuzz, ANGLE) are bundled and unpacked to `%TEMP%\.net` on first start; `-p:IncludeNativeLibrariesForSelfExtract=false` keeps them beside the exe instead (verified: nothing unpacked, starts faster, 3 DLLs next to the exe).
- The editor sets `InvariantGlobalization` (no ICU / `libicu` needed). Keep the library and the editor culture-independent: no `new CultureInfo("xx")`, no culture-specific formatting or comparison. The demo is not invariant (its `date` command uses the current culture).
- **Windows PowerShell 5.1 reads UTF-8 files without a BOM as ANSI**: never rewrite source files containing non-ASCII characters (box-drawing, CJK, `█`) with `Get-Content`/`Set-Content` there; use the edit tool.

## Reference files (read on demand, not loaded at start)

- `.claude/csharp-coding-kit/reference/antipatterns.md` — BAD/GOOD catalog; use when reviewing code
- `.claude/csharp-coding-kit/reference/snippets.md` — Result, ProblemDetails mapping, exception handler, endpoint groups, test fixture
- `.claude/csharp-coding-kit/reference/architecture-advisor.md` — architecture questionnaire and decision matrix

## Corrections learned

<!-- When Claude gets something wrong and you correct it, add a one-line rule here. -->
