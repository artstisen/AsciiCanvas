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

- **Palette is 16/256 colors only** (`CellColor`), by decision. No RGB cells. The one exception is the texture behind the cells (`CanvasTexture`, `CellCanvas.Texture`): it belongs to the window, not to the cells, and its shades are mixed in RGB from the default background toward a tint, because the palette has no dark steps of a color. The host paints it in `CanvasView.FillBackgrounds`, in cells with the default background only.
- **One rune per cell**; East Asian wide characters and emoji take two cells (`CellWidth.WideLead` / `WideTrail`). No grapheme clusters yet.
- **`CellCanvas` is thread-safe** (one lock, back buffer published by `Present()`); `CellGrid` is not.
- **Expected failures are not exceptions**: out-of-range drawing is clipped, not thrown. Exceptions are for contract violations (bad arguments).
- **Result pattern is not used**: the library has no operations with expected failure outcomes.
- **No `Console` use in the libraries**; `TextTerminal` is the console-like layer and exposes a `TextWriter` (`Out`).
- **`CancellationToken` parameters are named `cancellationToken`**, and library awaits use `ConfigureAwait(false)`.
- **Two fonts are embedded**: Cascadia Mono (default) and JetBrains Mono, both SIL OFL 1.1, under `src/AsciiCanvas.Avalonia/Assets/Fonts` and exposed through `CanvasFonts`. They are used through `avares://` URIs, and Avalonia resolves those families only from the embedded collection, so a wrong family name in `CanvasFonts` crashes at the first render. Keep each OFL text and `THIRD-PARTY-NOTICES.md` with the fonts; do not modify the font files.
- **`Key` enum members are not individually documented** (CS1591 suppressed for that enum only): the member names are the key names.
- **Packages**: `AsciiCanvas.Core` and `AsciiCanvas.Avalonia` are packable (`dotnet pack -c Release -o artifacts`); shared metadata (author, MIT license, readme, tags, snupkg symbols) is in `src/Directory.Build.props`, and `samples/Directory.Build.props` turns packing off for the samples. The version (currently 0.1.0) is one `<Version>` in the root `Directory.Build.props`, shared by the packages, samples and tests. Not published to nuget.org. The repository is private on GitHub (`https://github.com/artstisen/AsciiCanvas`); add `RepositoryUrl` and SourceLink when it becomes public. Bump `<Version>` for a release.
- **`docs/USAGE.md` is the guide for people and agents who build on the library.** Keep it in step with the public API: when a public member, event or option is added, renamed or removed, update the guide and its samples in the same change. Samples in it were compiled against the packed packages; recompile them when you change them.

## Domain notes

- `Present()` copies the back buffer to the front buffer; hosts render from the front buffer via `CopyFrame`. Drawing without `Present()` is never shown.
- Box-drawing (`U+2500` block) and block elements (`U+2580`) are painted procedurally by the Avalonia host, not from font glyphs, so they join at any scale. `BoxDrawing` describes the line arms.
- `CanvasSizing.ScaleToFit` keeps columns/rows and scales cells; `FillWithCells` keeps cell size and changes columns/rows (posts a `ResizeEvent`).
- Typed text (`TextEvent`) and key presses (`KeyEvent`) are separate; a printable key press arrives as both. `TextTerminal.ReadKeyAsync` skips the key-down for printable keys and returns the text.
- **Diagnostics are a feature of the grid**: `CanvasView.ShowDiagnostics` shows Avalonia's FPS/render graphs plus our own overlay (cells, draw ms, renders/s, from `RenderStatistics`); `AllowDiagnosticsToggle` makes F3 switch it at run time. Keep both off by default and keep F3 opt-in, so programs receive every key. The full-screen toggle (`AllowFullScreenToggle`: Alt+Enter and F11, back to the state before) is opt-in for the same reason; `CanvasWindowState.FullScreen` maps to Avalonia's `WindowState.FullScreen`, which used to be reported as `Maximized`.
- **The stress test is the benchmark** (demo menu item 6, run with `--fill --font-size=N`; measure in Release only). Baseline in the README: about 0.6 µs per cell per draw (it was 10 µs before glyph runs). Re-measure after changing `CellRenderer`, `RenderResources` or `CanvasView.Render`. Every `Present()` redraws the whole grid, so cost depends on grid size, not on how much changed.
- **Foreground text is drawn as glyph runs**: `CellRenderer.DrawRow` collects neighbouring plain cells with the same color and font style in a `GlyphRunBuilder` and draws them as one `GlyphRun`, with per-glyph advances taken from the snapped cell edges and the baseline from `FormattedText`, so placement matches single glyphs exactly (verified pixel for pixel against the old per-cell renderer). Box, block, wide, non-BMP and fallback-font cells still go through `DrawForeground` one cell at a time. When changing the renderer, compare screenshots of old and new builds pixel by pixel (build the old commit in a `git worktree`).
- **A blink must not redraw an invisible cursor**: the blink timer only invalidates when the last frame had a visible cursor and the control has focus (a hidden cursor used to cost 38% of a core on a 29,000-cell grid).
- **Host-facing window properties live on `CellCanvas`** (`Title`, `Icon`, `FontSize`, `WindowState`, `WindowDragRegions`, and the request `RequestGridSize`), each with an event or a host call (`TitleChanged`, `IconChanged`, `FontSizeChanged`, `GridSizeRequested`, `WindowStateRequested`, `ReportWindowState`); a grid size requested makes the window normal and sizes it to the grid at the font size (half a pixel more, so the floor in `GridLayout.CountFor` does not lose a cell to rounding); a font size changed sets the window's `SizeToContent` to `Manual` first, so the window keeps its size and the cells change; the host marshals to the UI thread and applies it (`CanvasHostApplication`). The initial values come from `CanvasAppOptions`. `Icon` holds PNG/ICO bytes (copied on set), because `WindowIcon` can only be created once Avalonia runs; empty shows the embedded AsciiCanvas icon (`Assets/Icons/AsciiCanvas.png`, or the executable's with `UseExecutableIcon`) and undecodable bytes are ignored. `WindowState` is write-to-request, read-the-reported-state, so it never disagrees with the real window. New properties of this kind should follow the same pattern.
- **The default icon is generated**: `tools/make-icon.ps1` draws `AsciiCanvas.png` (window) and `AsciiCanvas.ico` (exes, via `ApplicationIcon` in the samples) into `src/AsciiCanvas.Avalonia/Assets/Icons`. Change the script and rerun it; do not edit the files by hand. `System.Drawing` cannot read the PNG-compressed small sizes of that ICO, but Windows can; check the ICO through a built exe.
- **Frameless windows** (`CanvasFrame.None` sets `WindowDecorations.None`): `CanvasView` does the window work, because only the OS can run a move or resize drag. A left press on a cell inside `WindowDragRegions` calls `BeginMoveDrag` (double-click toggles maximize), a press in the outer 6 px band (`ResizeEdges.At`, pure math in Core with tests) calls `BeginResizeDrag`, and both are swallowed (not posted to `Input`). The behavior is active only when the window's decorations are not `Full`. Grips and moves are off while maximized. The editor sample's `--frameless` mode is the reference (`Tui/WindowButtons`).
- **`TerminalConsole` is a source-compatible stand-in for `System.Console`** (static class in Core, state attached by `CanvasApp.RunTerminal` or `Attach`). It exists so a console program ports with one `<Using ... Alias="Console">` line; keep its member names and signatures identical to `System.Console` (the `Write`/`WriteLine` overloads are `inheritdoc` pass-throughs to `Terminal.Out`, a `TextWriter`, so formatting matches). Members that cannot work are left out on purpose (`CancelKeyPress`), so the compiler points at them instead of a silent no-op. The shim's reads are synchronous wrappers over `TextTerminal`'s async ones and block the program's thread, which is safe because `CanvasApp.Run` runs the program off the UI thread. `ConsoleColor` and `CellColor` differ only by swapping the red and blue bits (`ColorMap`). `samples/AsciiCanvas.ConsolePort/ClassicProgram.cs` must keep compiling against the real console (no alias); check that when changing it. Culture-formatted output (`Write(decimal)`) follows the current culture, as `Console` does, so tests use integers.
- **Paste and clipboard**: the host turns Ctrl+V, Cmd+V and Shift+Insert into a `PasteEvent` (`InterceptPasteShortcut`, default on); programs copy through `CellCanvas.Clipboard` (`ICanvasClipboard`), which can throw `ExternalException` when another program holds the clipboard, so callers catch it. `KeyModifiers.Meta` is Command; shortcuts should accept Control or Meta.
- **`CanvasApp.Run` runs the window on an STA thread on Windows**: the Windows clipboard throws `CoInitialize has not been called` otherwise, and top-level statements cannot carry `[STAThread]`. The `Application` object must be created on that same thread (Avalonia binds its dispatcher to the creating thread), which is why `Run` builds it inside the thread's start function.
- **The editor sample's model types are public** (`EditorModel`, `TextBuffer`, `TextMeasure`, `TextPosition`) only so `AsciiCanvas.Editor.Tests` can reach them without `InternalsVisibleTo`; the sample is not a library. Editing logic belongs in the model, drawing in `EditorView`, input handling in `EditorApp`.
- **Known editor limits**: combining characters are separate cursor stops, no search.
- **The editor is a TUI built on the canvas, with no native controls**: `MenuBar` (layout, hit-testing, drawing), `Tui/MenuLoop`, `Tui/MessageDialog` and `Tui/FileDialog` each run their own modal input loop and draw over the editor through `ModalHost.DrawBackground` (the editor drawn without presenting). `EditorApp.ExecuteAsync(EditorCommand)` is the one place where menu entries, shortcuts and close requests end up. Dialogs treat a `CloseRequestedEvent` as "cancel" and re-post it, so the editor's own handler runs. Colors for menus and dialogs are in `Tui/UiStyles`.
- **Closing**: `canvas.InterceptClose = true` makes the host cancel the window's close and send `CloseRequestedEvent`; the program calls `canvas.Exit()` to really close. The host lets the window close freely once the program has ended, so a finished program can never trap the window.
- **Single-file publish** of the editor: `dotnet publish samples/AsciiCanvas.Editor -c Release -r win-x64`. The csproj turns on self-contained single-file only when a runtime identifier is given (so builds and tests are unaffected) and deletes the `.pdb`/`.xml` side files after publishing. Do not enable trimming: Avalonia relies on reflection. Native libraries (Skia, HarfBuzz, ANGLE) are bundled and unpacked to `%TEMP%\.net` on first start; `-p:IncludeNativeLibrariesForSelfExtract=false` keeps them beside the exe instead (verified: nothing unpacked, starts faster, 3 DLLs next to the exe).
- The editor sets `InvariantGlobalization` (no ICU / `libicu` needed). Keep the library and the editor culture-independent: no `new CultureInfo("xx")`, no culture-specific formatting or comparison. The demo is not invariant (its `date` command uses the current culture).
- **Windows PowerShell 5.1 reads UTF-8 files without a BOM as ANSI**: never rewrite source files containing non-ASCII characters (box-drawing, CJK, `█`) with `Get-Content`/`Set-Content` there; use the edit tool.
- **GUI test scripts**: `shot2.ps1` calls `ShowWindow(SW_RESTORE)` while focusing, which un-maximizes a maximized window, so use a plain `CopyFromScreen` of the window rect when testing maximize. Parallel tool calls that edit and build in one batch can build before the edits land; build in a separate call.

## Publishing safety

The repository is on GitHub (private for now; it may become public, and then the whole history is public). Nothing private goes up:

- **Only the GitHub no-reply address**, `46990511+artstisen@users.noreply.github.com`, as author and committer email. Never a
  private or work address, and never write those addresses (or their domains) into files or messages. Each clone sets it with `git config user.email 46990511+artstisen@users.noreply.github.com`;
  check `git config user.email` before committing, because this machine's global email is the private one.
- **The pre-push hook enforces it**: `.githooks/pre-push` refuses commits with another email, files that must stay local, and
  secret-like lines. Enable it once per clone with `git config core.hooksPath .githooks`. Never bypass it (`--no-verify`); fix the cause.
- **Before every commit and push**, check what goes up: `git status` and the diff for build output, `.claude/settings.local.json`,
  keys (`*.pfx`, `*.snk`, `*.pem`), `.env`, tokens, `inspect.xml`, screenshots or logs with personal data, and personal details in
  messages or files. New kinds of local files go into `.gitignore` in the same change.
- **Push only `main` (and feature branches)**, never `--all` or `--mirror`: `refs/agents/...` holds another tool's local checkpoints.
- **A commit with a wrong email** is fixed before pushing by rewriting it (`git filter-branch --env-filter` setting `GIT_AUTHOR_EMAIL`
  and `GIT_COMMITTER_EMAIL`, on the unpushed range only). Once pushed, rewriting needs a force push; ask first.

## Reference files (read on demand, not loaded at start)

- `.claude/csharp-coding-kit/reference/antipatterns.md` — BAD/GOOD catalog; use when reviewing code
- `.claude/csharp-coding-kit/reference/snippets.md` — Result, ProblemDetails mapping, exception handler, endpoint groups, test fixture
- `.claude/csharp-coding-kit/reference/architecture-advisor.md` — architecture questionnaire and decision matrix

## Corrections learned

<!-- When Claude gets something wrong and you correct it, add a one-line rule here. -->
