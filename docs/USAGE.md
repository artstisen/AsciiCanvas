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
- A program that already uses `Console` (WriteLine, ReadLine, ReadKey, colors, cursor placement) can run on a canvas with one alias
  line in its `.csproj` and `CanvasApp.RunTerminal`; see "Porting a console program".
- `CellCanvas` is thread-safe. The program runs on a background thread; the window runs on the UI thread.
- Set `canvas.Title` and `canvas.Icon` (PNG or ICO bytes) to change the window title and icon at any time. `Frame = CanvasFrame.None`
  removes the OS title bar and border so the program draws its own (see "Windows without a frame").

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
| `WindowStateEvent(State)` | The window was minimized, maximized or restored (`Normal`, `Minimized`, `Maximized`) |
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
- After a resize, the old content stays at the top-left and new cells are blank, so redraw everything. With
  `canvas.KeepCursorRowOnShrink = true` (which `TextTerminal` sets), a canvas made shorter than the cursor row is scrolled up
  first, as a terminal does, so the cursor row stays the last row and the rows that leave the top go to the scrollback.
- Handle a burst of events before drawing, so a long paste or a held key draws once:

  ```csharp
  var input = await canvas.Input.ReadAsync(canvas.Closed);
  do { Handle(input); } while (canvas.Input.TryRead(out input));
  Redraw();
  ```

- A program that works with console keys (a line editor ported from the console, for example) but reads `canvas.Input` itself can
  turn each event into the keys `Console.ReadKey` would return, with `ConsoleKeyMap.FromInput(input)`. Typed text gives one key per
  character, an action key gives one key, and a paste gives its characters with an `Enter` key for each line break, so pasted lines
  arrive one by one. Mouse input, resizes and the key-down of printable keys (which also arrive as a `TextEvent`) give no keys:

  ```csharp
  var input = await canvas.Input.ReadAsync(canvas.Closed);
  foreach (var key in ConsoleKeyMap.FromInput(input))
  {
      editor.HandleKey(key);   // a ConsoleKeyInfo, as from Console.ReadKey(true)
  }
  ```

  `ConsoleKeyMap.FromKeyPress(press)` and `ConsoleKeyMap.FromChar(c)` do the same for one `KeyPress` (from
  `TextTerminal.ReadKeyAsync`) or one typed character.

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
    UseExecutableIcon = false,               // true: show the exe's icon (Windows) instead of the AsciiCanvas icon
    Frame = CanvasFrame.System,              // CanvasFrame.None: no title bar or border; the program draws its own
    Columns = 100, Rows = 30,
    Sizing = CanvasSizing.FillWithCells,
    FontSize = 16,
    FontFamily = CanvasFonts.JetBrainsMono,
    Palette = ColorPalette.Xterm,
    CloseWindowWhenProgramEnds = false,
    ShowDiagnostics = false,
    AllowDiagnosticsToggle = true,           // F3 shows frame times
    ScrollbackLines = 1000,                  // rows kept for scrolling back; RunTerminal's default, 0 turns it off
}
```

- **Title and icon**: `canvas.Title = "notes.txt *";` and `canvas.Icon = File.ReadAllBytes("app.png");` work at any time, from any
  thread, and the window follows. The icon is the bytes of a PNG or ICO file (also available as `CanvasAppOptions.Icon` for the
  start-up icon). Without one, the window shows the AsciiCanvas icon (a dark gray rounded square with three blue squares forming
  an "A"); empty bytes bring that back, and bytes the platform cannot decode are ignored. To show the icon of your executable
  instead (on Windows, the one set with `<ApplicationIcon>` in your `.csproj`), set `UseExecutableIcon = true`. Other platforms have
  no executable icon for a window, so give them an `Icon`.
- **Frame**: `Frame = CanvasFrame.System` (default) gives the window the title bar and border of the operating system.
  `CanvasFrame.None` removes them, so the program draws its own; see "Windows without a frame" below.
- **Sizing**. `ScaleToFit` (default) keeps the column and row count and scales cells and font with the window.
  `PreserveAspectRatio` (default true) keeps the cells at the font's proportions and centers the grid; false stretches the grid
  to fill. `FillWithCells` keeps the cell size and changes the column and row count when the window is resized. The program
  gets a `ResizeEvent` and must redraw.
- **Fonts**: Cascadia Mono (default) and JetBrains Mono are embedded, so every platform looks the same. Any other
  monospaced `FontFamily` works.
- **Diagnostics**: `ShowDiagnostics` draws the frame rate, render times and the grid size over the canvas, for testing.
- **Scrollback**: see "Scrollback" below. `ScrollbackLines` sets `canvas.ScrollbackLimit` at start-up. Unset, `RunTerminal` keeps
  `CanvasApp.DefaultTerminalScrollbackLines` (1000) rows and `Run` keeps none.

## Scrollback

With `canvas.ScrollbackLimit` above 0, the canvas keeps the rows that scroll off the top, and the user can scroll back to read them,
as in a terminal:

- **What is kept**: the rows that an upward `Scroll` of a full-width region starting at row 0 pushes off the top. `TextTerminal`
  output that runs past the bottom does exactly that, so console-style programs get scrollback without doing anything. A program
  with a status line at the bottom can scroll only the rows above it (`new CellRect(0, 0, canvas.Columns, canvas.Rows - 1)`) and
  still feed the scrollback. Scrolling a region lower down, or not full width, keeps nothing. The oldest rows are dropped at the limit.
- **Scrolling**: the mouse wheel (3 rows a notch; trackpad movement is added up, so a swipe scrolls smoothly) and
  Shift+PageUp/PageDown (a page; Fn+Shift+Up/Down on a Mac keyboard without those keys) scroll the view. These then do not reach
  the program: no wheel `MouseEvent`, no `KeyEvent` for Shift+PageUp/PageDown. Any other key that the program receives, typed text
  or a paste brings the view back to the bottom before the program gets it (keys the host keeps, such as F3 for diagnostics, do not).
- **While scrolled back**: new output does not move the view; it stays on the rows the user is reading. The cursor is hidden when it
  is out of view. Mouse events still give the cell under the pointer on the screen, not in the history.
- **For the program**: `ScrollbackCount` is the number of rows kept, `ScrollbackOffset` is how far the view is scrolled back (set it
  to 0 to show the canvas, for example after a command), `ScrollBack(rows)` moves the view relative to where it is (negative moves
  toward the bottom; use it rather than `ScrollbackOffset += n`, which can miss rows that arrive at the same time), and
  `ClearScrollback()` empties it. Memory grows with the rows kept, up to the limit. Scrolling back changes only what the
  host shows; drawing, `GetCell` and the cursor still work on the canvas itself.

Leave scrollback off (the default for `Run`) in a full-screen program that uses the wheel or Shift+PageUp itself.

A program with lines that must stay at the bottom (a status line, an input line) sets `canvas.ScrollbackPinnedRows` to their
number. While the view is scrolled back, those rows stay where they are and the scrollback shows above them, and the cursor stays
visible in them. When the canvas is resized, they stay at the bottom; with `KeepCursorRowOnShrink`, a smaller canvas first takes the
blank rows above them (one is left, for the next line) and only then moves the top rows into the scrollback.

## Windows without a frame

With `Frame = CanvasFrame.None` the window has no title bar and no border: the whole window is the canvas, and the program draws
its own frame, title bar and buttons with cells, in the style of the program. The host supplies what only the operating system
can do:

| What | How |
|---|---|
| Move the window | Set `canvas.WindowDragRegions` to the cells of your title bar (without the buttons in it). A press there moves the window, and a double-click maximizes or restores it. The press does not reach `canvas.Input` |
| Resize the window | Built in: the outer 6 pixels along each edge and corner resize the window, with the matching mouse cursor. Presses there do not reach `canvas.Input`, so keep clickable things a few pixels away from the very edge |
| Minimize, maximize, restore | `canvas.WindowState = CanvasWindowState.Minimized / Maximized / Normal`. Read `canvas.WindowState` to draw the right glyph; a `WindowStateEvent` arrives when it changes |
| Close | `canvas.Exit()`, or your own close logic first (an "unsaved changes" question, for example) |

```csharp
CanvasApp.Run(async canvas =>
{
    canvas.InterceptClose = true;
    while (true)
    {
        // Row 0 is the title bar: a title at the left and [-][+][x] at the right.
        canvas.Fill(new CellRect(0, 0, canvas.Columns, 1), ' ', new CellStyle(CellColor.White, CellColor.Blue));
        canvas.Write(1, 0, "My program", new CellStyle(CellColor.BrightWhite, CellColor.Blue, CellAttributes.Bold));
        canvas.Write(canvas.Columns - 9, 0, " - ", new CellStyle(CellColor.BrightWhite, CellColor.Blue));
        canvas.Write(canvas.Columns - 6, 0, canvas.WindowState == CanvasWindowState.Maximized ? " r " : " + ", new CellStyle(CellColor.BrightWhite, CellColor.Blue));
        canvas.Write(canvas.Columns - 3, 0, " x ", new CellStyle(CellColor.BrightWhite, CellColor.Red));
        canvas.WindowDragRegions = [new CellRect(0, 0, canvas.Columns - 9, 1)];
        canvas.Present();

        switch (await canvas.Input.ReadAsync(canvas.Closed))
        {
            case CloseRequestedEvent:
                canvas.Exit();
                break;
            case MouseEvent { Action: MouseAction.Press, Button: MouseButton.Left, Row: 0 } click:
                if (click.Column >= canvas.Columns - 3)
                {
                    canvas.Exit();
                }
                else if (click.Column >= canvas.Columns - 6)
                {
                    canvas.WindowState = canvas.WindowState == CanvasWindowState.Maximized ? CanvasWindowState.Normal : CanvasWindowState.Maximized;
                }
                else if (click.Column >= canvas.Columns - 9)
                {
                    canvas.WindowState = CanvasWindowState.Minimized;
                }

                break;
        }
    }
}, new CanvasAppOptions { Title = "My program", Frame = CanvasFrame.None, Sizing = CanvasSizing.FillWithCells });
```

Things to know:

- Use `Sizing = CanvasSizing.FillWithCells` (or `PreserveAspectRatio = false`) so the grid reaches the edges of the window. With the
  default scaled grid the cells keep their proportions and the grid is centered, which leaves margins around your title bar.
- The window title still matters: it is what the taskbar and Alt+Tab show. Keep setting `canvas.Title`.
- With no frame there is no border or shadow, so draw a border or give the title bar a color that stands out from the desktop.
- Set `WindowDragRegions` again whenever the layout changes (after a `ResizeEvent`, for example). It is copied when set.
- A maximized window fills the work area and leaves the taskbar visible. Resize grips and dragging are off while it is maximized.
- The `samples/AsciiCanvas.Editor` sample shows a complete version: run it with `--frameless`.
- Tested on Windows only. Moving and resizing use the window manager's own drag, which is the normal way on Linux and macOS too, but I have not run them there. On macOS the red, yellow and green buttons are gone with the frame, so the program must offer every action itself.

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

`terminal.Out` is a `TextWriter` for code that expects `Console.Out`, and `terminal.In` is a `TextReader` for `Console.In`
(its `ReadLine` blocks until Enter, so use it from the program's thread). `terminal.Canvas` is the underlying canvas.

### Several terminals on one canvas

A `TextTerminal` can write in some of the rows only: `MarginTop` and `MarginBottom` are rows above and below it that it never writes
to or scrolls. They follow the canvas when it is resized, and at least one row is always left, so `MarginTop = int.MaxValue` keeps a
terminal on the last row. Only a terminal whose rows start at row 0 feeds the scrollback. `Pen` gives a terminal a style of its own
(null: the canvas `Pen`), so two terminals on one canvas can write in different colors, and `Resized` is raised on the reading
thread when a read takes a `ResizeEvent`, so the program can redraw what lies outside the terminals.

```csharp
var output = new TextTerminal(canvas) { MarginBottom = 2 };                  // scrolls above the last two rows
var input = new TextTerminal(canvas) { MarginTop = int.MaxValue, Pen = new CellStyle(CellColor.BrightGreen) };
canvas.ScrollbackPinnedRows = 2;                                             // a status line and the input line stay put
input.Resized += () => canvas.HLine(0, canvas.Rows - 2, canvas.Columns);     // redraw the status line
output.WriteLine("output scrolls here");
var line = await input.ReadLineAsync("> ", cancellationToken: canvas.Closed);
```


A paste arrives as it does in a console. `ReadKeyAsync` returns the text of each pasted line with an `Enter` press for each line
break. `ReadLineAsync` returns the first pasted line, and the following reads return the rest, one line each. `RunTerminal` turns on
1000 rows of scrollback (see "Scrollback"). When the window is made shorter, the terminal keeps the line it writes on: the rows
above it move up into the scrollback instead of the bottom rows being cut off.

## Porting a console program

A program that uses `Console` can move to a canvas window without changing its code. `TerminalConsole` has the same members as
`System.Console` for what a text program uses, and `CanvasApp.RunTerminal` attaches it to the window's terminal. Two steps:

1. Add an alias to the program's `.csproj`, so every `Console` in the project means `TerminalConsole`:

   ```xml
   <ItemGroup>
     <Using Include="AsciiCanvas.TerminalConsole" Alias="Console" />
   </ItemGroup>
   ```

2. Start the program from `RunTerminal` instead of `Main`:

   ```csharp
   return CanvasApp.RunTerminal(
       _ => { ClassicProgram.Run(); return Task.CompletedTask; },
       new CanvasAppOptions { Title = "My program", Sizing = CanvasSizing.FillWithCells, CloseWindowWhenProgramEnds = true },
       args);
   ```

`samples/AsciiCanvas.ConsolePort` is a complete example: its `ClassicProgram.cs` is ordinary console code (prompt, arrow-key menu
with `ReadKey`, colors, cursor placement, a long wrapped line, an in-place progress bar) and compiles unchanged against the real console.

What works: `Write` and `WriteLine` with all their overloads and format strings, `ReadLine`, `Read`, `ReadKey` (with
`intercept`, returning a real `ConsoleKeyInfo`), `KeyAvailable`, `ForegroundColor`, `BackgroundColor`, `ResetColor`, `Clear`,
`CursorLeft`, `CursorTop`, `SetCursorPosition`, `GetCursorPosition`, `CursorVisible`, `WindowWidth`, `WindowHeight`, `BufferWidth`,
`BufferHeight`, `Title`, `In`, `Out` and `Error`. Long lines wrap at the window width, and wide characters take two cells.

What differs:

- **Reads block the calling thread.** That is fine on the program's own thread, which is where `RunTerminal` runs it. When the user
  closes the window, a pending read throws `OperationCanceledException`, which `Run` treats as a normal end.
- **Use `Sizing = CanvasSizing.FillWithCells`** if the program should see the real window size. `WindowWidth` and `WindowHeight` are the
  canvas's columns and rows, and with that sizing they follow the window; read them again after the user may have resized it.
- **No ANSI escape sequences.** The canvas does not interpret `\e[31m`; use `ForegroundColor` and `BackgroundColor`. Libraries that
  print escape codes (Spectre.Console and the like) will show them as text.
- **Colors are the 16 `ConsoleColor` values.** Reading `ForegroundColor` gives `Gray` until the program sets one.
- **A cursor position outside the window is moved to the nearest edge** instead of throwing.
- **`Console.CancelKeyPress` does not exist.** Ctrl+C arrives as a key press (`ReadKey` returns `C` with Control). A program that
  uses the event must be changed. `TreatControlCAsInput` is accepted and ignored.
- **The scrollback belongs to the window.** Lines that scroll off the top can be read again with the mouse wheel or
  Shift+PageUp, but `BufferHeight` is the window height and `WindowTop` is always 0. A program that needs Shift+PageUp or the wheel
  itself sets `ScrollbackLines = 0`.
- **Pasted lines arrive one by one**, as in a console: `ReadLine` returns one pasted line per call, and `ReadKey` returns `Enter` for each line break.
- **A newline at the bottom of the window scrolls with the current background color**, as a real terminal does, so reset colors
  before the last `WriteLine` of a highlighted line.
- **Redirection and encodings.** `IsOutputRedirected` and its relatives are always false, and `OutputEncoding` is accepted and ignored.
- The attached terminal is static: one canvas window per process.

Without `RunTerminal`, call `TerminalConsole.Attach(terminal)` yourself. To route code that calls the real `System.Console` (a library,
for example) to the canvas, use `System.Console.SetOut(terminal.Out)` and `System.Console.SetIn(terminal.In)` (spelled out, because in a project with the alias `Console` means `TerminalConsole`).

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
