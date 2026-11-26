namespace AsciiCanvas.ConsolePort;

/// <summary>
/// A classic console program: ReadLine, ReadKey, colors, cursor placement, long lines and the window size. It uses
/// <c>Console</c> only. The project maps <c>Console</c> to the canvas with one alias line; remove it and this file runs in a real console.
/// </summary>
internal static class ClassicProgram
{
    private const string Sentence =
        "The quick brown fox jumps over the lazy dog, and then keeps running through a very long line of text that does not " +
        "fit on one row, to show that long lines wrap at the width of the window just as they do in a console. ";

    private static readonly string[] _menu = ["Show the colors", "Print a long line", "Show a progress bar", "Quit"];

    public static void Run()
    {
        Console.Title = "Classic console program";
        Console.Write("What is your name? ");
        var name = Console.ReadLine();
        var greeting = $"Hello, {(string.IsNullOrWhiteSpace(name) ? "stranger" : name)}!";

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== A console program ===");
            Console.ResetColor();
            Console.WriteLine(greeting);
            Console.WriteLine($"The window is {Console.WindowWidth} columns by {Console.WindowHeight} rows.");
            Console.WriteLine();

            var choice = Choose();
            if (choice == _menu.Length - 1)
            {
                break;
            }

            Console.WriteLine();
            switch (choice)
            {
                case 0:
                    ShowColors();
                    break;
                case 1:
                    Console.WriteLine(string.Concat(Enumerable.Repeat(Sentence, 4)));
                    break;
                case 2:
                    ShowProgress();
                    break;
            }

            Console.WriteLine();
            Console.Write("Press any key to go back to the menu.");
            Console.ReadKey(intercept: true);
        }

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Bye.");
        Console.ResetColor();
    }

    // Arrow keys move the highlight, Enter chooses. Redraws in place by putting the cursor back at the top of the menu.
    private static int Choose()
    {
        Console.WriteLine("Choose with the arrow keys and Enter:");
        Console.CursorVisible = false;
        var (left, top) = Console.GetCursorPosition();
        var selected = 0;

        while (true)
        {
            Console.SetCursorPosition(left, top);
            for (var i = 0; i < _menu.Length; i++)
            {
                Console.ForegroundColor = i == selected ? ConsoleColor.Black : ConsoleColor.Gray;
                Console.BackgroundColor = i == selected ? ConsoleColor.Yellow : ConsoleColor.Black;
                Console.Write($" {_menu[i],-24}");
                Console.ResetColor();
                Console.WriteLine();
            }

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.UpArrow)
            {
                selected = (selected + _menu.Length - 1) % _menu.Length;
            }
            else if (key == ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % _menu.Length;
            }
            else if (key == ConsoleKey.Enter)
            {
                Console.CursorVisible = true;
                return selected;
            }
        }
    }

    private static void ShowColors()
    {
        foreach (var color in Enum.GetValues<ConsoleColor>())
        {
            Console.BackgroundColor = color;
            Console.Write("    ");
        }

        Console.ResetColor();
        Console.WriteLine();
        foreach (var color in Enum.GetValues<ConsoleColor>().Skip(1))
        {
            Console.ForegroundColor = color;
            Console.Write($"{color} ");
        }

        Console.ResetColor();
        Console.WriteLine();
    }

    private static void ShowProgress()
    {
        var width = Math.Min(40, Console.WindowWidth - 12);
        var top = Console.CursorTop;
        for (var step = 0; step <= width; step++)
        {
            Console.SetCursorPosition(0, top);
            Console.Write($"[{new string('#', step)}{new string('.', width - step)}] {step * 100 / width,3}%");
            Thread.Sleep(25);
        }

        Console.WriteLine();
    }
}
