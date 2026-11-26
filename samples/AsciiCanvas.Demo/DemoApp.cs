using System.Globalization;

namespace AsciiCanvas.Demo;

/// <summary>The demo program: a menu with one screen per feature of the canvas.</summary>
internal static class DemoApp
{
    private static readonly CellStyle _title = new(CellColor.BrightYellow, CellColor.Default, CellAttributes.Bold);
    private static readonly CellStyle _frame = new(CellColor.BrightBlue);
    private static readonly CellStyle _hint = new(CellColor.BrightBlack);
    private static readonly CellStyle _keyLabel = new(CellColor.BrightCyan, CellColor.Default, CellAttributes.Bold);

    public static async Task RunAsync(CellCanvas canvas)
    {
        canvas.CursorVisible = false;
        while (true)
        {
            DrawMenu(canvas);
            var choice = await WaitAsync(canvas, onResize: () => DrawMenu(canvas), onText: text => text);
            switch (choice)
            {
                case "1":
                    await PaletteAsync(canvas);
                    break;
                case "2":
                    await BoxesAsync(canvas);
                    break;
                case "3":
                    await TerminalAsync(canvas);
                    break;
                case "4":
                    await PaintAsync(canvas);
                    break;
                case "5":
                    await UnicodeAsync(canvas);
                    break;
                case "6":
                    await StressAsync(canvas);
                    break;
                case "q" or null:
                    return;
            }
        }
    }

    // Waits for Escape (returns null) or for typed text that onText accepts. Redraws on resize.
    private static async Task<string?> WaitAsync(CellCanvas canvas, Action onResize, Func<string, string?> onText)
    {
        while (true)
        {
            switch (await canvas.Input.ReadAsync(canvas.Closed))
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
        canvas.Box(canvas.Bounds, BoxStyle.DoubleLine, _frame, title);
        canvas.Write(2, canvas.Rows - 1, $" {hint} ", _hint);
    }

    private static void DrawMenu(CellCanvas canvas)
    {
        DrawFrame(canvas, "AsciiCanvas", "q: quit   F3: performance overlay");
        canvas.Write(4, 3, "A character-cell canvas for ASCII user interfaces", _title);
        canvas.Write(4, 4, $"Grid: {canvas.Columns} x {canvas.Rows} cells - resize the window!", _hint);

        string[] items = ["Colors and attributes", "Boxes, lines and blocks", "Terminal (line input)", "Mouse paint", "Unicode and wide characters", "Stress test (every cell, every frame)"];
        for (var i = 0; i < items.Length; i++)
        {
            canvas.Write(6, 7 + (i * 2), $"{i + 1}", _keyLabel);
            canvas.Write(9, 7 + (i * 2), items[i]);
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
                canvas.Fill(new CellRect(3 + (i * 4), 3, 4, 2), ' ', new CellStyle(CellColor.Default, CellColor.FromIndex((byte)i)));
            }

            canvas.Write(3, 6, "Color cube:");
            for (var g = 0; g < 6; g++)
            {
                for (var r = 0; r < 6; r++)
                {
                    for (var b = 0; b < 6; b++)
                    {
                        canvas.SetCell(3 + (r * 7) + b, 7 + g, ' ', new CellStyle(CellColor.Default, CellColor.Cube(r, g, b)));
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
            CellAttributes[] attributes =
            [
                CellAttributes.Bold,
                CellAttributes.Dim,
                CellAttributes.Italic,
                CellAttributes.Underline,
                CellAttributes.Strikethrough,
                CellAttributes.Reverse,
            ];
            foreach (var attribute in attributes)
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
                canvas.Box(new CellRect(3 + (i * 15), 2, 14, 5), styles[i], new CellStyle(CellColor.FromIndex((byte)(9 + i))), styles[i].ToString(), fill: true);
            }

            canvas.Write(3, 8, "Joined lines:");
            canvas.Write(3, 9, "┌──┬──┐  ╔══╦══╗  ┏━━┳━━┓");
            canvas.Write(3, 10, "├──┼──┤  ╠══╬══╣  ┣━━╋━━┫");
            canvas.Write(3, 11, "└──┴──┘  ╚══╩══╝  ┗━━┻━━┛");

            canvas.Write(3, 13, "Blocks:");
            canvas.Write(3, 14, "▁▂▃▄▅▆▇█ ▏▎▍▌▋▊▉█ ░▒▓█ ▀▄ ▖▗▘▙▚▛▜▝▞▟", new CellStyle(CellColor.BrightMagenta));

            canvas.Write(3, 16, "Clipped drawing (the text is cut at the box edge):");
            var clipBox = new CellRect(3, 17, 12, 3);
            canvas.Box(clipBox, BoxStyle.Rounded, _hint);
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
            canvas.Write(3, 9, "|日本|語|  <- the bars stay aligned", _hint);
            canvas.Write(3, 10, "|abcd|ef|", _hint);
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
            var line = await terminal.ReadLineAsync("> ", cancellationToken: canvas.Closed);
            terminal.Style = textStyle;

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            switch (parts)
            {
                case []:
                    break;
                case ["exit"]:
                    return;
                case ["help"]:
                    terminal.WriteLine("help | clear | date | color <name> | echo <text> | exit");
                    break;
                case ["clear"]:
                    terminal.Clear();
                    break;
                case ["date"]:
                    terminal.WriteLine(TimeProvider.System.GetLocalNow().ToString("F", CultureInfo.CurrentCulture));
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

    private static async Task StressAsync(CellCanvas canvas)
    {
        const string ramp = " .:-=+*#%@";
        const string special = "│─┼╬█▓▒░▀▄▌▐■□●○◆◇▲▼★";
        var clock = TimeProvider.System;
        var start = clock.GetTimestamp();
        var paused = false;
        var frames = 0;
        var fps = 0.0;
        var fpsWindowStart = start;

        while (true)
        {
            while (canvas.Input.TryRead(out var input))
            {
                switch (input)
                {
                    case KeyEvent { Key: Key.Escape }:
                        return;
                    case TextEvent { Text: " " }:
                        paused = !paused;
                        break;
                }
            }

            if (!paused)
            {
                var t = clock.GetElapsedTime(start).TotalSeconds;
                var frameStart = clock.GetTimestamp();
                FillPlasma(canvas, t, ramp, special);
                var fillMilliseconds = clock.GetElapsedTime(frameStart).TotalMilliseconds;
                DrawStressPanel(canvas, fps, fillMilliseconds);
                canvas.Present();
                frames++;

                var windowSeconds = clock.GetElapsedTime(fpsWindowStart).TotalSeconds;
                if (windowSeconds >= 0.5)
                {
                    fps = frames / windowSeconds;
                    frames = 0;
                    fpsWindowStart = clock.GetTimestamp();
                }
            }

            await Task.Delay(paused ? 50 : 1, canvas.Closed);
        }
    }

    // Every cell gets a new character, foreground and background each frame.
    private static void FillPlasma(CellCanvas canvas, double t, string ramp, string special)
    {
        var columns = canvas.Columns;
        var rows = canvas.Rows;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var v = Math.Sin((x * 0.09) + t)
                    + Math.Sin((y * 0.17) - (t * 1.3))
                    + Math.Sin(((x + y) * 0.05) + (t * 0.7))
                    + Math.Sin((Math.Sqrt(((x * x) / 4.0) + (y * y)) * 0.2) - t);
                var n = (v + 4) / 8;

                var r = (int)(n * 5.99);
                var g = (int)(Math.Abs(Math.Sin(n * Math.PI)) * 5.99);
                var b = (int)((1 - n) * 5.99);
                var style = new CellStyle(CellColor.Cube(5 - r, 5 - g, 5 - b), CellColor.Cube(r / 2, g / 2, b / 2));

                var c = ((x * 7) + (y * 13)) % 23 == 0
                    ? special[(x + y) % special.Length]
                    : ramp[(int)(n * 9.99)];
                canvas.SetCell(x, y, c, style);
            }
        }
    }

    private static void DrawStressPanel(CellCanvas canvas, double fps, double fillMilliseconds)
    {
        var panel = new CellRect(2, 1, 46, 7);
        if (canvas.Columns < panel.Right + 2 || canvas.Rows < panel.Bottom + 1)
        {
            return;
        }

        var cells = canvas.Columns * canvas.Rows;
        var text = new CellStyle(CellColor.BrightWhite, CellColor.Black);
        canvas.Box(panel, BoxStyle.DoubleLine, new CellStyle(CellColor.BrightYellow, CellColor.Black), "Stress test", fill: true);
        canvas.Write(4, 2, $"Grid:    {canvas.Columns} x {canvas.Rows} = {cells:N0} cells", text);
        canvas.Write(4, 3, $"Program: {fillMilliseconds,6:F1} ms to fill, {fps,5:F0} frames/s", text);
        canvas.Write(4, 4, "Every cell changes every frame.", text);
        canvas.Write(4, 5, "Space: pause   F3: overlay   Esc: back", text);
    }

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
            switch (await canvas.Input.ReadAsync(canvas.Closed))
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
