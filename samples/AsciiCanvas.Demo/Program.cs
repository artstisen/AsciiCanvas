using AsciiCanvas;
using AsciiCanvas.Avalonia;

var fill = args.Contains("--fill");

return CanvasApp.Run(
    Demo.RunAsync,
    new CanvasAppOptions
    {
        Title = fill ? "AsciiCanvas demo (fixed cell size)" : "AsciiCanvas demo (scaled grid)",
        Columns = 80,
        Rows = 30,
        Sizing = fill ? CanvasSizing.FillWithCells : CanvasSizing.ScaleToFit,
        CloseWindowWhenProgramEnds = true,
    },
    args);

internal static class Demo
{
    private static readonly CellStyle Title = new(CellColor.BrightYellow, CellColor.Default, CellAttributes.Bold);
    private static readonly CellStyle Frame = new(CellColor.BrightBlue);
    private static readonly CellStyle Hint = new(CellColor.BrightBlack);
    private static readonly CellStyle KeyLabel = new(CellColor.BrightCyan, CellColor.Default, CellAttributes.Bold);

    public static async Task RunAsync(CellCanvas canvas)
    {
        canvas.CursorVisible = false;
        while (true)
        {
            DrawMenu(canvas);
            var choice = await WaitAsync(canvas, onResize: () => DrawMenu(canvas), onText: text => text);
            switch (choice)
            {
                case "1": await PaletteAsync(canvas); break;
                case "2": await BoxesAsync(canvas); break;
                case "3": await TerminalAsync(canvas); break;
                case "4": await PaintAsync(canvas); break;
                case "5": await UnicodeAsync(canvas); break;
                case "q" or null: return;
            }
        }
    }

    // Waits for Escape (returns null) or for typed text that onText accepts. Redraws on resize.
    private static async Task<string?> WaitAsync(CellCanvas canvas, Action onResize, Func<string, string?> onText)
    {
        while (true)
        {
            switch (await canvas.Input.ReadAsync())
            {
                case ResizeEvent:
                    onResize();
                    break;
                case KeyEvent { Key: Key.Escape }:
                    return null;
                case TextEvent text when onText(text.Text) is { } result:
                    return result;
            }
        }
    }

    private static void DrawFrame(CellCanvas canvas, string title, string hint = "Esc: back")
    {
        canvas.Pen = default;
        canvas.Clear();
        canvas.Box(canvas.Bounds, BoxStyle.Double, Frame, title);
        canvas.Write(2, canvas.Rows - 1, $" {hint} ", Hint);
    }

    private static void DrawMenu(CellCanvas canvas)
    {
        DrawFrame(canvas, "AsciiCanvas", "q: quit");
        canvas.Write(4, 3, "A character-cell canvas for ASCII user interfaces", Title);
        canvas.Write(4, 4, $"Grid: {canvas.Columns} x {canvas.Rows} cells - resize the window!", Hint);

        string[] items = ["Colors and attributes", "Boxes, lines and blocks", "Terminal (line input)", "Mouse paint", "Unicode and wide characters"];
        for (var i = 0; i < items.Length; i++)
        {
            canvas.Write(6, 7 + i * 2, $"{i + 1}", KeyLabel);
            canvas.Write(9, 7 + i * 2, items[i]);
        }

        canvas.Present();
    }

    private static async Task PaletteAsync(CellCanvas canvas)
    {
        void Draw()
        {
            DrawFrame(canvas, "Colors");
            canvas.Write(3, 2, "Standard colors:");
            for (var i = 0; i < 16; i++)
            {
                canvas.Fill(new CellRect(3 + i * 4, 3, 4, 2), ' ', new CellStyle(CellColor.Default, CellColor.FromIndex((byte)i)));
            }

            canvas.Write(3, 6, "Color cube:");
            for (var g = 0; g < 6; g++)
            {
                for (var r = 0; r < 6; r++)
                {
                    for (var b = 0; b < 6; b++)
                    {
                        canvas.SetCell(3 + r * 7 + b, 7 + g, ' ', new CellStyle(CellColor.Default, CellColor.Cube(r, g, b)));
                    }
                }
            }

            canvas.Write(3, 14, "Gray ramp:");
            for (var i = 0; i < 24; i++)
            {
                canvas.SetCell(3 + i, 15, ' ', new CellStyle(CellColor.Default, CellColor.Gray(i)));
            }

            canvas.Write(3, 17, "Attributes:");
            var x = 3;
            foreach (var attribute in new[] { CellAttributes.Bold, CellAttributes.Dim, CellAttributes.Italic, CellAttributes.Underline, CellAttributes.Strikethrough, CellAttributes.Reverse })
            {
                x = canvas.Write(x, 18, attribute.ToString(), new CellStyle(CellColor.BrightGreen, CellColor.Default, attribute)) + 2;
            }

            canvas.Present();
        }

        Draw();
        await WaitAsync(canvas, Draw, _ => null);
    }

    private static async Task BoxesAsync(CellCanvas canvas)
    {
        void Draw()
        {
            DrawFrame(canvas, "Boxes, lines and blocks");
            var styles = Enum.GetValues<BoxStyle>();
            for (var i = 0; i < styles.Length; i++)
            {
                canvas.Box(new CellRect(3 + i * 15, 2, 14, 5), styles[i], new CellStyle(CellColor.FromIndex((byte)(9 + i))), styles[i].ToString(), fill: true);
            }

            canvas.Write(3, 8, "Joined lines:");
            canvas.Write(3, 9, "┌──┬──┐  ╔══╦══╗  ┏━━┳━━┓");
            canvas.Write(3, 10, "├──┼──┤  ╠══╬══╣  ┣━━╋━━┫");
            canvas.Write(3, 11, "└──┴──┘  ╚══╩══╝  ┗━━┻━━┛");

            canvas.Write(3, 13, "Blocks:");
            canvas.Write(3, 14, "▁▂▃▄▅▆▇█ ▏▎▍▌▋▊▉█ ░▒▓█ ▀▄ ▖▗▘▙▚▛▜▝▞▟", new CellStyle(CellColor.BrightMagenta));

            canvas.Write(3, 16, "Clipped drawing (the text is cut at the box edge):");
            var clipBox = new CellRect(3, 17, 12, 3);
            canvas.Box(clipBox, BoxStyle.Rounded, Hint);
            using (canvas.PushClip(clipBox.Inflate(-1, -1)))
            {
                canvas.Write(8, 18, "hello, world", new CellStyle(CellColor.BrightGreen));
            }

            canvas.Present();
        }

        Draw();
        await WaitAsync(canvas, Draw, _ => null);
    }

    private static async Task UnicodeAsync(CellCanvas canvas)
    {
        void Draw()
        {
            DrawFrame(canvas, "Unicode");
            canvas.Write(3, 3, "Latin: Åäö éèê ñ ß", new CellStyle(CellColor.BrightGreen));
            canvas.Write(3, 5, "Wide (2 cells each): 日本語 한국어 中文", new CellStyle(CellColor.BrightYellow));
            canvas.Write(3, 7, "Emoji: 😀 🚀 ✨", new CellStyle(CellColor.BrightCyan));
            canvas.Write(3, 9, "|日本|語|  <- the bars stay aligned", Hint);
            canvas.Write(3, 10, "|abcd|ef|", Hint);
            canvas.Present();
        }

        Draw();
        await WaitAsync(canvas, Draw, _ => null);
    }

    private static async Task TerminalAsync(CellCanvas canvas)
    {
        canvas.CursorVisible = true;
        try
        {
            await RunTerminalAsync(canvas);
        }
        finally
        {
            canvas.CursorVisible = false;
            canvas.Pen = default;
        }
    }

    private static async Task RunTerminalAsync(CellCanvas canvas)
    {
        canvas.Pen = default;
        var terminal = new TextTerminal(canvas);
        terminal.Clear();
        terminal.WriteLine("AsciiCanvas terminal. Try: help, color red, date, clear, exit", new CellStyle(CellColor.BrightYellow));

        var textStyle = CellStyle.Default;
        while (true)
        {
            terminal.Style = new CellStyle(CellColor.BrightGreen);
            var line = await terminal.ReadLineAsync("> ");
            terminal.Style = textStyle;

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            switch (parts)
            {
                case []:
                    break;
                case ["exit"]:
                    canvas.Pen = default;
                    return;
                case ["help"]:
                    terminal.WriteLine("help | clear | date | color <name> | echo <text> | exit");
                    break;
                case ["clear"]:
                    terminal.Clear();
                    break;
                case ["date"]:
                    terminal.WriteLine(DateTime.Now.ToString("F"));
                    break;
                case ["echo", var text]:
                    terminal.Out.WriteLine(text);
                    break;
                case ["color", var name]:
                    if (Enum.TryParse<ConsoleColor>(name, true, out var color))
                    {
                        textStyle = new CellStyle(CellColor.FromIndex(ToAnsi((int)color)));
                        terminal.Style = textStyle;
                        terminal.WriteLine($"Now writing in {color}.");
                    }
                    else
                    {
                        terminal.WriteLine("Unknown color.");
                    }

                    break;
                default:
                    terminal.WriteLine($"Unknown command: {parts[0]}");
                    break;
            }
        }
    }

    // ConsoleColor uses a different bit order than ANSI (blue and red swapped).
    private static byte ToAnsi(int consoleColor) =>
        (byte)((consoleColor & 8) | ((consoleColor & 1) << 2) | (consoleColor & 2) | ((consoleColor & 4) >> 2));

    private static async Task PaintAsync(CellCanvas canvas)
    {
        var color = 9;
        var cell = (Column: 0, Row: 0);

        void Status()
        {
            canvas.Fill(new CellRect(2, 1, canvas.Columns - 4, 1), ' ');
            canvas.Write(2, 1, $" color {color}  cell {cell.Column},{cell.Row} ", new CellStyle(CellColor.White, CellColor.FromIndex((byte)color)));
        }

        void Draw()
        {
            DrawFrame(canvas, "Mouse paint", "Esc: back   left: paint   right: erase   wheel: color");
            Status();
            canvas.Present();
        }

        Draw();
        while (true)
        {
            switch (await canvas.Input.ReadAsync())
            {
                case ResizeEvent:
                    Draw();
                    break;
                case KeyEvent { Key: Key.Escape }:
                    return;
                case MouseEvent m:
                    cell = (m.Column, m.Row);
                    if (m.Action == MouseAction.Wheel)
                    {
                        color = (color + m.WheelDelta + 256) % 256;
                    }
                    else if (canvas.Bounds.Inflate(-1, -2).Contains(m.Column, m.Row) && m.Action != MouseAction.Release)
                    {
                        if (m.Button == MouseButton.Left)
                        {
                            canvas.SetCell(m.Column, m.Row, ' ', new CellStyle(CellColor.Default, CellColor.FromIndex((byte)color)));
                        }
                        else if (m.Button == MouseButton.Right)
                        {
                            canvas.SetCell(m.Column, m.Row, ' ', default(CellStyle));
                        }
                    }

                    Status();
                    canvas.Present();
                    break;
            }
        }
    }
}
