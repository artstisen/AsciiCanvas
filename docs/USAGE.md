# Using AsciiCanvas

This guide is for a programmer, or an AI agent, who wants to build a program on AsciiCanvas. For what the project is and how
to build it, see the [README](../README.md). The code samples here were built and run against the NuGet packages.

## Summary

- AsciiCanvas is a grid of character cells (columns x rows) that a program draws on. It is **not** a terminal emulator: there
  are no escape sequences and no processes.
- Two packages: `AsciiCanvas.Core` (the model, no UI dependency) and `AsciiCanvas.Avalonia` (the window and the control).
  Both target `net10.0`. Namespaces are `AsciiCanvas` and `AsciiCanvas.Avalonia`.
- The usual program is one lambda: `CanvasApp.Run(async canvas => { ... })`. Draw with `canvas.Write/Box/Fill/...`, call
  `canvas.Present()` to show the drawing, and read user input from `canvas.Input`.
- Nothing appears until `Present()`. Drawing goes to a back buffer.
- Typing a printable key produces **both** a `KeyEvent` and a `TextEvent`. Use `TextEvent` for text and `KeyEvent` for
  shortcuts and navigation, never both for the same thing.
- One rune per cell. East Asian wide characters and emoji take two cells. There are no combining characters.
- `CellCanvas` is thread-safe. The program runs on a background thread; the window runs on the UI thread.
- Set `canvas.Title` and `canvas.Icon` (PNG or ICO bytes) to change the window title and icon at any time.

## Getting the packages

The packages are not published to nuget.org yet. Build them and use the folder as a NuGet source:

```powershell
dotnet pack -c Release -o artifacts
dotnet nuget add source (Resolve-Path artifacts) -n asciicanvas
dotnet add package AsciiCanvas.Avalonia
```

`AsciiCanvas.Avalonia` brings `AsciiCanvas.Core` and Avalonia with it. A program that only needs the model (for tests, or with
its own renderer) can use `AsciiCanvas.Core` alone. Inside this repository, a `ProjectReference` to the two projects works too.

## A first program

```csharp
using AsciiCanvas;
using AsciiCanvas.Avalonia;

return CanvasApp.Run(
    async canvas =>
    {
        var counter = 0;

        void Redraw()
        {
            canvas.Clear();
            canvas.Box(canvas.Bounds, BoxStyle.DoubleLine, title: "Hello");
            canvas.Write(2, 2, "Type, click or press Esc", new CellStyle(CellColor.BrightGreen));
            canvas.Write(2, 4, $"Events seen: {counter}", new CellStyle(CellColor.BrightYellow, CellColor.Blue));
            canvas.Present();
        }

        Redraw();
        while (true)
        {
            var input = await canvas.Input.ReadAsync(canvas.Closed);
            counter++;
            canvas.Title = $"Hello ({counter})";
            if (input is KeyEvent { Key: Key.Escape })
            {
                return;
            }

            Redraw();
        }
    },
    new CanvasAppOptions { Title = "Hello", Columns = 50, Rows = 12 },
    args);
```

`CanvasApp.Run` opens the window, runs your lambda on a background thread and blocks until the window closes. The program should
stop when `canvas.Closed` is canceled. `canvas.Input.ReadAsync(canvas.Closed)` already throws `OperationCanceledException` then,
and `Run` treats that as a normal end. If the lambda returns, the final screen stays until the user closes the window (set
`CloseWindowWhenProgramEnds` to close it at once). A failure thrown by the lambda is rethrown from `Run` after the window closes.

## Drawing

All drawing methods are on `CellCanvas`. Coordinates are cells, with (0, 0) at the top-left. Anything outside the canvas or the
current clip is ignored silently. Every method takes an optional `CellStyle`; without one, `canvas.Pen` is used.

| Method | What it does |
|---|---|
| `Clear(style?)` | Fills the whole canvas with blanks (ignores the clip) |
| `Write(x, y, text, style?)` | One line of text; returns the column after it. Wide characters take two cells. Control characters are skipped |
| `SetCell(x, y, char or Rune, style?)` | One character |
| `GetCell(x, y)` | Reads a cell of the back buffer (blank outside the canvas) |
| `Fill(rect, char = ' ', style?)` | Fills a rectangle |
| `HLine(x, y, length, BoxStyle, style?)`, `VLine(...)` | Lines |
| `Box(rect, BoxStyle, style?, title?, fill)` | A frame with an optional title; `fill: true` clears the inside |
| `PushClip(rect)` | Limits drawing to `rect` until the returned scope is disposed: `using (canvas.PushClip(r)) { ... }` |
| `Scroll(region, dx, dy, style?)` | Moves a region's content |
| `Capture(rect)` / `Blit(grid, x, y, ...)` | Copy a region out as a `CellGrid`, and draw a grid back (`skipBlanks` makes it a transparent sprite) |
| `Resize(columns, rows)` | Changes the grid size from the program (posts a `ResizeEvent`) |
| `SetCursor(x, y)`, `CursorVisible`, `CursorShape` | The text cursor, shown by the host |
| `Present()` | Publishes everything drawn since the last call. Does nothing if nothing changed |

`CellRect` is `(X, Y, Width, Height)` with `Right`, `Bottom`, `Contains`, `Intersect`, `Offset` and `Inflate`. `canvas.Bounds` is
the whole canvas. `BoxStyle` is `SingleLine`, `DoubleLine`, `Rounded`, `Heavy` or `Ascii`.

Box-drawing (U+2500-U+257F) and block (U+2580-U+259F) characters are drawn by the control itself, so they join without gaps at
any scale.

### Styles and colors

```csharp
var warning = new CellStyle(CellColor.BrightYellow, CellColor.Red, CellAttributes.Bold | CellAttributes.Underline);
canvas.Pen = new CellStyle(CellColor.White, CellColor.Black);   // the default for later calls
var orange = CellColor.Cube(5, 2, 0);                           // 6 x 6 x 6 color cube, each value 0 to 5
var gray = CellColor.Gray(12);                                  // 24-step gray ramp, 0 to 23
```

- `CellColor` is the 256-color palette: 16 named colors (`Black` ... `BrightWhite`), `Cube(r, g, b)`, `Gray(level)`,
  `FromIndex(0-255)`, and `Default` (the palette's default foreground or background).
- `CellAttributes`: `Bold`, `Dim`, `Italic`, `Underline`, `Strikethrough`, `Reverse`.
- A `ColorPalette` (`Campbell` by default, or `Xterm`, or your own) maps colors to RGB. Set it with `CanvasAppOptions.Palette`.

## Input

Everything the user does arrives in `canvas.Input` as an `InputEvent`. Read with `await canvas.Input.ReadAsync(ct)`, or
`TryRead(out var e)` to drain what is waiting, or `ReadAllAsync(ct)`.

| Event | Meaning |
|---|---|
| `KeyEvent(Key, Modifiers)` | Any key, including printable ones. Use for shortcuts, arrows, Enter, Escape, function keys |
| `TextEvent(Text)` | Typed text, translated by the user's keyboard layout |
| `PasteEvent(Text)` | Pasted text from Ctrl+V, Cmd+V or Shift+Insert, with line breaks as `\n` |
| `MouseEvent(Column, Row, Button, Action, WheelDelta, Modifiers)` | Press, release, move (only when the cell changes) and wheel, in cell coordinates |
| `ResizeEvent(Columns, Rows)` | The grid changed size. Redraw |
| `CloseRequestedEvent` | The user tried to close the window and the program asked to decide (`InterceptClose`) |

```csharp
switch (await canvas.Input.ReadAsync(canvas.Closed))
{
    case KeyEvent { Key: Key.Escape }:
        return;
    case KeyEvent { Key: Key.S, Modifiers: KeyModifiers.Control }:
        Save();
        break;
    case TextEvent text:
        buffer.Append(text.Text);
        break;
    case PasteEvent paste:
        buffer.Append(paste.Text);
        break;
    case MouseEvent { Action: MouseAction.Press, Button: MouseButton.Left } click:
        Select(click.Column, click.Row);
        break;
    case ResizeEvent:
        break; // nothing to update; the redraw below uses the new size
}

Redraw();
```

- `KeyModifiers` are `Shift`, `Control`, `Alt` and `Meta` (the Command key on a Mac). A program that wants "Ctrl+S" on every
  platform should accept `Control` or `Meta`.
- `Key` names: `A`-`Z`, `D0`-`D9`, `NumPad0`-`9`, `F1`-`F12`, `Left`/`Right`/`Up`/`Down`, `Home`/`End`/`PageUp`/`PageDown`,
  `Insert`/`Delete`, `Enter`, `Escape`, `Back` (Backspace), `Tab`, `Space`. If you also use `Avalonia.Input`, write
  `AsciiCanvas.Key` in full, because `Key` is ambiguous there.
- After a resize, the old content stays at the top-left and new cells are blank, so redraw everything.
- Handle a burst of events before drawing, so a long paste or a held key draws once:

  ```csharp
  var input = await canvas.Input.ReadAsync(canvas.Closed);
  do { Handle(input); } while (canvas.Input.TryRead(out input));
  Redraw();
  ```

### Closing

By default the window closes when the user closes it, and `canvas.Closed` is canceled. To ask about unsaved changes first:

```csharp
canvas.InterceptClose = true;   // the close button now only sends a CloseRequestedEvent
...
case CloseRequestedEvent:
    if (!dirty || await ConfirmDiscardAsync())
    {
        canvas.Exit();          // closes the window
    }
    break;
```

A program that sets `InterceptClose` must keep reading input, or the window cannot be closed. `canvas.Exit()` also closes the
window when the program decides to quit by itself.

### Clipboard

`canvas.Clipboard` (`ICanvasClipboard`) has `GetTextAsync` and `SetTextAsync`. Pasting arrives as a `PasteEvent`; use the
clipboard for copy and cut. Both methods can throw `ExternalException` when another program holds the clipboard, so treat that
as a failed copy. `canvas.Clipboard` is null when there is no host (in tests).

## The window and its options

```csharp
new CanvasAppOptions
{
    Title = "My tool",                       // the initial title; change it later with canvas.Title
    Icon = File.ReadAllBytes("app.ico"),     // optional; PNG or ICO bytes; change it later with canvas.Icon
    Columns = 100, Rows = 30,
    Sizing = CanvasSizing.FillWithCells,
    FontSize = 16,
    FontFamily = CanvasFonts.JetBrainsMono,
    Palette = ColorPalette.Xterm,
    CloseWindowWhenProgramEnds = false,
    ShowDiagnostics = false,
    AllowDiagnosticsToggle = true,           // F3 shows frame times
}
```

- **Title and icon**: `canvas.Title = "notes.txt *";` and `canvas.Icon = File.ReadAllBytes("app.ico");` work at any time, from any
  thread, and the window follows. The icon is the bytes of a PNG or ICO file (also available as `CanvasAppOptions.Icon` for the
  start-up icon); empty bytes restore the platform default, and bytes the platform cannot decode are ignored. On Windows,
  `<ApplicationIcon>` in your `.csproj` sets the icon of the exe, and the window uses it too when no `Icon` is given. On Linux
  and macOS use `Icon`.
- **Sizing**. `ScaleToFit` (default) keeps the column and row count and scales cells and font with the window.
  `PreserveAspectRatio` (default true) keeps the cells at the font's proportions and centers the grid; false stretches the grid
  to fill. `FillWithCells` keeps the cell size and changes the column and row count when the window is resized. The program
  gets a `ResizeEvent` and must redraw.
- **Fonts**: Cascadia Mono (default) and JetBrains Mono are embedded, so every platform looks the same. Any other
  monospaced `FontFamily` works.
- **Diagnostics**: `ShowDiagnostics` draws the frame rate, render times and the grid size over the canvas, for testing.

## Console-style programs

`CanvasApp.RunTerminal` gives the program a `TextTerminal`, which behaves like a console: `Write`, `WriteLine`, `Style`, wrapping
and scrolling at the edges, `ReadKeyAsync` and `ReadLineAsync` (with editing, history, pasting and an optional mask character).

```csharp
return CanvasApp.RunTerminal(async terminal =>
{
    terminal.WriteLine("What is your name?", new CellStyle(CellColor.BrightCyan));
    var name = await terminal.ReadLineAsync("> ", cancellationToken: terminal.Canvas.Closed);
    terminal.WriteLine($"Hello, {name}!");
    await terminal.Canvas.Input.ReadAsync(terminal.Canvas.Closed);
});
```

`terminal.Out` is a `TextWriter` for code that expects `Console.Out`. `terminal.Canvas` is the underlying canvas.

## Using the control in your own Avalonia application

`CanvasView` is an ordinary Avalonia control with a `Canvas` property. Create the canvas, give it to the view, and run your
program against the same canvas:

```csharp
public sealed class HostApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var canvas = new CellCanvas(40, 10);
            var view = new CanvasView { Canvas = canvas, Sizing = CanvasSizing.FillWithCells, FontSize = 16 };

            var header = new TextBlock { Text = "Any other Avalonia control", Margin = new Thickness(8) };
            DockPanel.SetDock(header, Dock.Top);
            var window = new Window { Title = "Embedded", Content = new DockPanel { Children = { header, view } } };
            window.Closed += (_, _) => canvas.Close();   // completes canvas.Input and cancels canvas.Closed
            desktop.MainWindow = window;

            _ = Task.Run(() => MyProgramAsync(canvas));   // never run the program on the UI thread
        }

        base.OnFrameworkInitializationCompleted();
    }
}

// Main:
[STAThread]
static int Main(string[] args) =>
    AppBuilder.Configure<HostApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
```

Things to know when hosting the control yourself:

- Mark `Main` with `[STAThread]` on Windows, because the clipboard needs a single-threaded apartment. `CanvasApp.Run` does that for you.
- `canvas.Title` and `InterceptClose` are handled by the window `CanvasApp` creates. In your own window, subscribe to
  `canvas.TitleChanged` and `Window.Closing` yourself if you want the same behavior.
- Your Avalonia version must be compatible with the one `AsciiCanvas.Avalonia` is built against (Avalonia 12).
- The control only draws when the program calls `Present()`, so an idle canvas costs nothing.

## Testing a program without a window

`CellCanvas` does not need a host. Draw on it, read the cells back, and post input yourself:

```csharp
var canvas = new CellCanvas(20, 3);
canvas.Write(1, 1, "Hi", new CellStyle(CellColor.BrightGreen));
canvas.Present();

var cell = canvas.GetCell(1, 1);             // 'H', bright green
canvas.Input.Post(new KeyEvent(Key.Enter));
var next = await canvas.Input.ReadAsync();   // KeyEvent { Key = Enter }
```

Design a program as `async Task RunAsync(CellCanvas canvas)` and pass the same method to `CanvasApp.Run` and to your tests.
The editor sample does this: its text model has no screen dependency and is tested on its own.

## Publishing

- A self-contained single file works (`-r win-x64`, `-r linux-x64`, `-r osx-arm64`). See "As a single executable" in the README.
- `<InvariantGlobalization>true</InvariantGlobalization>` removes the need for ICU (`libicu` on Linux) and is safe unless your
  own code depends on culture rules.
- Do not enable trimming: Avalonia relies on reflection.

## Samples

- `samples/AsciiCanvas.Demo`: colors, boxes, a terminal, mouse paint, Unicode, a stress test.
- `samples/AsciiCanvas.Editor`: a full text editor with menus, dialogs, a file browser, selection, clipboard, undo and
  close handling. The best reference for a real program: `EditorApp.cs` has the input loop and `Tui/` has the widgets.
