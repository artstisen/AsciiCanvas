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
tests/
  AsciiCanvas.Core.Tests/  xUnit v3
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
- **The default font is embedded**: Cascadia Mono (SIL OFL 1.1) under `src/AsciiCanvas.Avalonia/Assets/Fonts`, used through an `avares://` URI. Avalonia resolves that family only from the embedded collection, so a wrong family name in `CanvasView.DefaultFontFamily` crashes at the first render. Keep the OFL text and `THIRD-PARTY-NOTICES.md` with the fonts; do not modify the font files.
- **`Key` enum members are not individually documented** (CS1591 suppressed for that enum only): the member names are the key names.
- **Not a NuGet package yet**, so package metadata (license, repository URL) is not set. Add it, with SourceLink and symbol packages, when publishing.

## Domain notes

- `Present()` copies the back buffer to the front buffer; hosts render from the front buffer via `CopyFrame`. Drawing without `Present()` is never shown.
- Box-drawing (`U+2500` block) and block elements (`U+2580`) are painted procedurally by the Avalonia host, not from font glyphs, so they join at any scale. `BoxDrawing` describes the line arms.
- `CanvasSizing.ScaleToFit` keeps columns/rows and scales cells; `FillWithCells` keeps cell size and changes columns/rows (posts a `ResizeEvent`).
- Typed text (`TextEvent`) and key presses (`KeyEvent`) are separate; a printable key press arrives as both. `TextTerminal.ReadKeyAsync` skips the key-down for printable keys and returns the text.

## Reference files (read on demand, not loaded at start)

- `.claude/csharp-coding-kit/reference/antipatterns.md` — BAD/GOOD catalog; use when reviewing code
- `.claude/csharp-coding-kit/reference/snippets.md` — Result, ProblemDetails mapping, exception handler, endpoint groups, test fixture
- `.claude/csharp-coding-kit/reference/architecture-advisor.md` — architecture questionnaire and decision matrix

## Corrections learned

<!-- When Claude gets something wrong and you correct it, add a one-line rule here. -->
