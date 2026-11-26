namespace AsciiCanvas;

/// <summary>Turns the key presses of a <see cref="TextTerminal"/> into the <see cref="ConsoleKeyInfo"/> values that <c>Console.ReadKey</c> returns.</summary>
internal static class ConsoleKeyMap
{
    /// <summary>Converts an action key (arrow, Enter, F-key, Ctrl+letter...) to a <see cref="ConsoleKeyInfo"/>.</summary>
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

    /// <summary>Converts a typed character to a <see cref="ConsoleKeyInfo"/>.</summary>
    public static ConsoleKeyInfo FromChar(char character)
    {
        var key = character switch
        {
            >= 'a' and <= 'z' => ConsoleKey.A + (character - 'a'),
            >= 'A' and <= 'Z' => ConsoleKey.A + (character - 'A'),
            >= '0' and <= '9' => ConsoleKey.D0 + (character - '0'),
            ' ' => ConsoleKey.Spacebar,
            ',' => ConsoleKey.OemComma,
            '.' => ConsoleKey.OemPeriod,
            '-' => ConsoleKey.OemMinus,
            '+' => ConsoleKey.OemPlus,
            _ => ConsoleKey.None,
        };

        return new ConsoleKeyInfo(character, key, char.IsAsciiLetterUpper(character), alt: false, control: false);
    }
}
