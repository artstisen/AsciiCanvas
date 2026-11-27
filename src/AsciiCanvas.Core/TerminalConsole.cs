using System.Text;

namespace AsciiCanvas;

/// <summary>
/// A stand-in for <see cref="Console"/> that reads and writes the <see cref="TextTerminal"/> of a canvas, so a console program
/// can move to a canvas window without changing its code. Give the project an alias, and every <c>Console.WriteLine</c>,
/// <c>Console.ReadKey</c>, <c>Console.ForegroundColor</c> and the like in it goes to the canvas:
/// <code>
/// &lt;ItemGroup&gt;
///   &lt;Using Include="AsciiCanvas.TerminalConsole" Alias="Console" /&gt;
/// &lt;/ItemGroup&gt;
/// </code>
/// <para>
/// The terminal is attached by <c>CanvasApp.RunTerminal</c>; use <see cref="Attach"/> to do it yourself. Reads block the calling
/// thread until the user types, so call them from the program's thread, never from the UI thread. When the window closes they
/// throw <see cref="OperationCanceledException"/>, which <c>CanvasApp.Run</c> treats as the normal end of the program.
/// </para>
/// <para>
/// Differences from the console: a cursor position outside the window is clamped instead of throwing, colors are the 16 console colors,
/// ANSI escape sequences are not interpreted, and <c>Console.CancelKeyPress</c> does not exist (Ctrl+C arrives as a key). The scrollback
/// (<see cref="CellCanvas.ScrollbackLimit"/>) belongs to the window, so <see cref="BufferHeight"/> and <see cref="WindowTop"/> do not
/// include it. Only one thread should read at a time. The state is static, so one terminal is attached at a time.
/// </para>
/// </summary>
public static class TerminalConsole
{
    private static TextTerminal? _terminal;
    private static string _typed = "";
    private static int _typedIndex;
    private static bool _typedPasted; // The text being handed out key by key came from a paste

    /// <summary>The terminal that the members of this class use.</summary>
    /// <exception cref="InvalidOperationException">No terminal is attached.</exception>
    public static TextTerminal Terminal =>
        _terminal ?? throw new InvalidOperationException("No terminal is attached. Run the program with CanvasApp.RunTerminal, or call TerminalConsole.Attach.");

    /// <summary>Makes <paramref name="terminal"/> the terminal that the members of this class use.</summary>
    /// <param name="terminal">The terminal to use.</param>
    public static void Attach(TextTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        _terminal = terminal;
        _typed = "";
        _typedIndex = 0;
    }

    /// <summary>Stops using <paramref name="terminal"/>, if it is the attached one.</summary>
    /// <param name="terminal">The terminal to let go.</param>
    public static void Detach(TextTerminal terminal)
    {
        if (ReferenceEquals(_terminal, terminal))
        {
            _terminal = null;
        }
    }

    // ---- Streams and settings ----

    /// <inheritdoc cref="Console.Out"/>
    public static TextWriter Out => Terminal.Out;

    /// <inheritdoc cref="Console.Error"/>
    public static TextWriter Error => Terminal.Out;

    /// <inheritdoc cref="Console.In"/>
    public static TextReader In => Terminal.In;

    /// <summary>The encoding is always Unicode; setting it has no effect.</summary>
    public static Encoding OutputEncoding
    {
        get => Encoding.Unicode;
        set => ArgumentNullException.ThrowIfNull(value);
    }

    /// <summary>The encoding is always Unicode; setting it has no effect.</summary>
    public static Encoding InputEncoding
    {
        get => Encoding.Unicode;
        set => ArgumentNullException.ThrowIfNull(value);
    }

    /// <summary>Always false: the canvas is not redirected.</summary>
    public static bool IsOutputRedirected => false;

    /// <summary>Always false: the canvas is not redirected.</summary>
    public static bool IsInputRedirected => false;

    /// <summary>Always false: the canvas is not redirected.</summary>
    public static bool IsErrorRedirected => false;

    /// <summary>Has no effect: Ctrl+C always arrives as a key press, and nothing stops the program.</summary>
    public static bool TreatControlCAsInput { get; set; }

    /// <inheritdoc cref="Console.Title"/>
    public static string Title
    {
        get => Terminal.Canvas.Title;
        set => Terminal.Canvas.Title = value;
    }

    // ---- Window and cursor ----

    /// <summary>The number of columns of the canvas. Setting it resizes the canvas.</summary>
    public static int WindowWidth
    {
        get => Terminal.Canvas.Columns;
        set => Terminal.Canvas.Resize(value, Terminal.Canvas.Rows);
    }

    /// <summary>The number of rows of the canvas. Setting it resizes the canvas.</summary>
    public static int WindowHeight
    {
        get => Terminal.Canvas.Rows;
        set => Terminal.Canvas.Resize(Terminal.Canvas.Columns, value);
    }

    /// <summary>The number of columns of the canvas. Setting it resizes the canvas.</summary>
    public static int BufferWidth
    {
        get => WindowWidth;
        set => WindowWidth = value;
    }

    /// <summary>The number of rows of the canvas. Setting it resizes the canvas.</summary>
    public static int BufferHeight
    {
        get => WindowHeight;
        set => WindowHeight = value;
    }

    /// <summary>The number of columns of the canvas.</summary>
    public static int LargestWindowWidth => WindowWidth;

    /// <summary>The number of rows of the canvas.</summary>
    public static int LargestWindowHeight => WindowHeight;

    /// <summary>Always 0: the window shows the whole canvas.</summary>
    public static int WindowLeft => 0;

    /// <summary>Always 0: the window shows the whole canvas.</summary>
    public static int WindowTop => 0;

    /// <summary>Resizes the canvas.</summary>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows.</param>
    public static void SetWindowSize(int width, int height) => Terminal.Canvas.Resize(width, height);

    /// <summary>Resizes the canvas.</summary>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows.</param>
    public static void SetBufferSize(int width, int height) => Terminal.Canvas.Resize(width, height);

    /// <inheritdoc cref="Console.CursorLeft"/>
    public static int CursorLeft
    {
        get => Math.Min(Terminal.CursorColumn, Terminal.Canvas.Columns - 1);
        set => Terminal.SetCursorPosition(value, Terminal.CursorRow);
    }

    /// <inheritdoc cref="Console.CursorTop"/>
    public static int CursorTop
    {
        get => Terminal.CursorRow;
        set => Terminal.SetCursorPosition(Terminal.CursorColumn, value);
    }

    /// <inheritdoc cref="Console.CursorVisible"/>
    public static bool CursorVisible
    {
        get => Terminal.Canvas.CursorVisible;
        set => Terminal.Canvas.CursorVisible = value;
    }

    /// <summary>Moves the cursor. A position outside the canvas is moved to the nearest edge instead of throwing.</summary>
    /// <param name="left">The column.</param>
    /// <param name="top">The row.</param>
    public static void SetCursorPosition(int left, int top) => Terminal.SetCursorPosition(left, top);

    /// <inheritdoc cref="Console.GetCursorPosition"/>
    public static (int Left, int Top) GetCursorPosition() => (CursorLeft, CursorTop);

    /// <inheritdoc cref="Console.Clear"/>
    public static void Clear() => Terminal.Clear();

    /// <summary>Does nothing: there is no sound.</summary>
    public static void Beep()
    {
    }

    /// <summary>Does nothing: there is no sound.</summary>
    /// <param name="frequency">Ignored.</param>
    /// <param name="duration">Ignored.</param>
    public static void Beep(int frequency, int duration)
    {
    }

    // ---- Colors ----

    /// <summary>The color of the text written next, as one of the 16 console colors.</summary>
    public static ConsoleColor ForegroundColor
    {
        get => ColorMap.ToConsoleColor(Terminal.Style.Foreground, ConsoleColor.Gray);
        set => Terminal.Style = Terminal.Style with { Foreground = ColorMap.ToCellColor(value) };
    }

    /// <summary>The background of the text written next, as one of the 16 console colors.</summary>
    public static ConsoleColor BackgroundColor
    {
        get => ColorMap.ToConsoleColor(Terminal.Style.Background, ConsoleColor.Black);
        set => Terminal.Style = Terminal.Style with { Background = ColorMap.ToCellColor(value) };
    }

    /// <inheritdoc cref="Console.ResetColor"/>
    public static void ResetColor() => Terminal.Style = Terminal.Style with { Foreground = CellColor.Default, Background = CellColor.Default };

    // ---- Input ----

    /// <summary>True when a key press or typed text is waiting.</summary>
    public static bool KeyAvailable => _typedIndex < _typed.Length || Terminal.KeyAvailable;

    /// <inheritdoc cref="Console.ReadLine"/>
    public static string? ReadLine() => Terminal.In.ReadLine();

    /// <inheritdoc cref="Console.Read"/>
    public static int Read() => Terminal.In.Read();

    /// <summary>
    /// True when the last key <see cref="ReadKey(bool)"/> returned came from a paste (Ctrl+V, Cmd+V, Shift+Insert), not from
    /// typing: a program with keys of its own (an editor's normal mode) can put pasted text in as text. Not part of
    /// <see cref="System.Console"/>, which cannot tell them apart.
    /// </summary>
    public static bool LastKeyPasted { get; private set; }

    /// <summary>
    /// Waits for a key. Typed text comes one character at a time, as it does from the console. Without <paramref name="intercept"/>
    /// the typed character is shown.
    /// </summary>
    /// <param name="intercept">True to keep the typed character off the screen.</param>
    /// <returns>The key that was pressed.</returns>
    public static ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        var terminal = Terminal;
        if (_typedIndex >= _typed.Length)
        {
            var press = terminal.ReadKeyAsync(terminal.Canvas.Closed).AsTask().GetAwaiter().GetResult();
            if (!press.IsText)
            {
                LastKeyPasted = press.Pasted;
                return ConsoleKeyMap.FromKeyPress(press);
            }

            _typed = press.Text;
            _typedIndex = 0;
            _typedPasted = press.Pasted;
        }

        LastKeyPasted = _typedPasted;

        var character = _typed[_typedIndex++];
        if (!intercept)
        {
            Echo(terminal, character);
        }

        return ConsoleKeyMap.FromChar(character);
    }

    // ---- Output ----

    /// <inheritdoc cref="Console.WriteLine()"/>
    public static void WriteLine() => Out.WriteLine();

    /// <inheritdoc cref="Console.WriteLine(bool)"/>
    public static void WriteLine(bool value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(char)"/>
    public static void WriteLine(char value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(char[])"/>
    public static void WriteLine(char[]? buffer) => Out.WriteLine(buffer);

    /// <inheritdoc cref="Console.WriteLine(char[], int, int)"/>
    public static void WriteLine(char[] buffer, int index, int count) => Out.WriteLine(buffer, index, count);

    /// <inheritdoc cref="Console.WriteLine(decimal)"/>
    public static void WriteLine(decimal value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(double)"/>
    public static void WriteLine(double value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(float)"/>
    public static void WriteLine(float value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(int)"/>
    public static void WriteLine(int value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(uint)"/>
    public static void WriteLine(uint value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(long)"/>
    public static void WriteLine(long value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(ulong)"/>
    public static void WriteLine(ulong value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(object)"/>
    public static void WriteLine(object? value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(string)"/>
    public static void WriteLine(string? value) => Out.WriteLine(value);

    /// <inheritdoc cref="Console.WriteLine(string, object[])"/>
    public static void WriteLine(string format, params object?[]? arg) => Out.WriteLine(format, arg ?? []);

    /// <inheritdoc cref="Console.Write(bool)"/>
    public static void Write(bool value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(char)"/>
    public static void Write(char value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(char[])"/>
    public static void Write(char[]? buffer) => Out.Write(buffer);

    /// <inheritdoc cref="Console.Write(char[], int, int)"/>
    public static void Write(char[] buffer, int index, int count) => Out.Write(buffer, index, count);

    /// <inheritdoc cref="Console.Write(decimal)"/>
    public static void Write(decimal value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(double)"/>
    public static void Write(double value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(float)"/>
    public static void Write(float value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(int)"/>
    public static void Write(int value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(uint)"/>
    public static void Write(uint value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(long)"/>
    public static void Write(long value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(ulong)"/>
    public static void Write(ulong value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(object)"/>
    public static void Write(object? value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(string)"/>
    public static void Write(string? value) => Out.Write(value);

    /// <inheritdoc cref="Console.Write(string, object[])"/>
    public static void Write(string format, params object?[]? arg) => Out.Write(format, arg ?? []);

    // A surrogate pair is two characters of one key, so it is shown when the second half has arrived.
    private static void Echo(TextTerminal terminal, char character)
    {
        if (char.IsLowSurrogate(character) && _typedIndex >= 2)
        {
            terminal.Write(_typed.AsSpan(_typedIndex - 2, 2));
        }
        else if (!char.IsControl(character) && !char.IsSurrogate(character))
        {
            terminal.Write([character]);
        }
    }
}
