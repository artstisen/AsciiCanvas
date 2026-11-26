namespace AsciiCanvas;

/// <summary>
/// Turns canvas input into the <see cref="ConsoleKeyInfo"/> values that <c>Console.ReadKey</c> returns. <see cref="TerminalConsole.ReadKey"/>
/// uses it; a program that reads <see cref="CellCanvas.Input"/> itself can use <see cref="FromInput"/> to get the same keys.
/// </summary>
public static class ConsoleKeyMap
{
    /// <summary>
    /// The console keys of one input event, in order, as <see cref="TerminalConsole.ReadKey"/> would return them: one key per character of
    /// typed text, one key for an action key (arrow, Enter, F-key, Ctrl+letter...), and for a paste one key per character with
    /// <see cref="ConsoleKey.Enter"/> for each line break. Mouse input, resizes and the key-down of printable keys (which also arrive as
    /// a <see cref="TextEvent"/>) give no keys.
    /// </summary>
    /// <param name="input">The input event.</param>
    /// <returns>The keys; empty if the event has none.</returns>
    public static IReadOnlyList<ConsoleKeyInfo> FromInput(InputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<KeyPress> presses = [];
        KeyPresses.Add(input, presses);
        if (presses.Count == 0)
        {
            return [];
        }

        List<ConsoleKeyInfo> keys = [];
        foreach (var press in presses)
        {
            if (press.IsText)
            {
                keys.AddRange(press.Text.Select(FromChar));
            }
            else
            {
                keys.Add(FromKeyPress(press));
            }
        }

        return keys;
    }

    /// <summary>Converts an action key (arrow, Enter, F-key, Ctrl+letter...) to a <see cref="ConsoleKeyInfo"/>.</summary>
    /// <param name="press">The key press; its <see cref="KeyPress.Text"/> is ignored.</param>
    /// <returns>The console key.</returns>
    public static ConsoleKeyInfo FromKeyPress(KeyPress press)
    {
        var shift = (press.Modifiers & KeyModifiers.Shift) != 0;
        var alt = (press.Modifiers & KeyModifiers.Alt) != 0;
        var control = (press.Modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;

        var key = press.Key;
        if (key is >= Key.A and <= Key.Z)
        {
            var offset = key - Key.A;
            var consoleKey = ConsoleKey.A + offset;
            var letter = (char)((shift ? 'A' : 'a') + offset);
            return new ConsoleKeyInfo(control ? (char)(offset + 1) : letter, consoleKey, shift, alt, control);
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            var offset = key - Key.D0;
            return new ConsoleKeyInfo(control ? '\0' : (char)('0' + offset), ConsoleKey.D0 + offset, shift, alt, control);
        }

        if (key is >= Key.F1 and <= Key.F12)
        {
            return new ConsoleKeyInfo('\0', ConsoleKey.F1 + (key - Key.F1), shift, alt, control);
        }

        var (consoleKeyName, character) = key switch
        {
            Key.Left => (ConsoleKey.LeftArrow, '\0'),
            Key.Right => (ConsoleKey.RightArrow, '\0'),
            Key.Up => (ConsoleKey.UpArrow, '\0'),
            Key.Down => (ConsoleKey.DownArrow, '\0'),
            Key.Home => (ConsoleKey.Home, '\0'),
            Key.End => (ConsoleKey.End, '\0'),
            Key.PageUp => (ConsoleKey.PageUp, '\0'),
            Key.PageDown => (ConsoleKey.PageDown, '\0'),
            Key.Insert => (ConsoleKey.Insert, '\0'),
            Key.Delete => (ConsoleKey.Delete, '\0'),
            Key.Enter => (ConsoleKey.Enter, '\r'),
            Key.Escape => (ConsoleKey.Escape, '\u001b'),
            Key.Back => (ConsoleKey.Backspace, '\b'),
            Key.Tab => (ConsoleKey.Tab, '\t'),
            _ => (ConsoleKey.None, '\0'),
        };

        return new ConsoleKeyInfo(character, consoleKeyName, shift, alt, control);
    }

    /// <summary>Converts a typed character to a <see cref="ConsoleKeyInfo"/>. A surrogate pair is two calls, one per half, as from the console.</summary>
    /// <param name="character">The character.</param>
    /// <returns>The console key.</returns>
    public static ConsoleKeyInfo FromChar(char character)
    {
        var key = character switch
        {
            >= 'a' and <= 'z' => ConsoleKey.A + (character - 'a'),
            >= 'A' and <= 'Z' => ConsoleKey.A + (character - 'A'),
            >= '0' and <= '9' => ConsoleKey.D0 + (character - '0'),
            ' ' => ConsoleKey.Spacebar,
            '\t' => ConsoleKey.Tab,
            ',' => ConsoleKey.OemComma,
            '.' => ConsoleKey.OemPeriod,
            '-' => ConsoleKey.OemMinus,
            '+' => ConsoleKey.OemPlus,
            _ => ConsoleKey.None,
        };

        return new ConsoleKeyInfo(character, key, char.IsAsciiLetterUpper(character), alt: false, control: false);
    }
}
