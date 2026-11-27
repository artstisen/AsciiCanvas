namespace AsciiCanvas;

/// <summary>Turns canvas input events into the key presses a console program sees.</summary>
internal static class KeyPresses
{
    /// <summary>
    /// Adds the key presses of <paramref name="input"/> to <paramref name="presses"/>: typed text as one text press, an action key as a key
    /// press, and a paste as its lines with an Enter between them, as a console delivers it. Mouse input, resizes and the key-down of
    /// printable keys (which also arrive as text) add nothing.
    /// </summary>
    public static void Add(InputEvent input, List<KeyPress> presses)
    {
        switch (input)
        {
            case TextEvent { Text.Length: > 0 } text:
                presses.Add(new KeyPress(Key.Unknown, text.Text, KeyModifiers.None));
                break;
            case PasteEvent { Text.Length: > 0 } paste:
                // Every Unicode line break counts (\r\n, \r, \n and the rare NEL, FF, LS and PS), whatever the source program used.
                var lines = paste.Text.ReplaceLineEndings("\n").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i > 0)
                    {
                        presses.Add(new KeyPress(Key.Enter, "", KeyModifiers.None, Pasted: true));
                    }

                    if (lines[i].Length > 0)
                    {
                        presses.Add(new KeyPress(Key.Unknown, lines[i], KeyModifiers.None, Pasted: true));
                    }
                }

                break;
            case KeyEvent key when IsActionKey(key):
                presses.Add(new KeyPress(key.Key, "", key.Modifiers));
                break;
        }
    }

    /// <summary>
    /// Option (Alt) with a letter or digit types a character (macOS): the key press is left to the text that follows it
    /// </summary>
    private static readonly bool _optionTypesText = OperatingSystem.IsMacOS();

    private static bool IsActionKey(KeyEvent e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown
                or Key.Insert or Key.Delete or Key.Enter or Key.Escape or Key.Back or Key.Tab:
                return true;
            case >= Key.F1 and <= Key.F12:
                return true;
            case (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9):
                // Ctrl+Alt is how some layouts type AltGr characters, which arrive as text instead. On macOS, Option (Alt)
                // alone types characters too (@ $ [ { on many layouts), so it is text there, not an action key.
                var command = (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
                var alt = (e.Modifiers & KeyModifiers.Alt) != 0;
                return command ? !alt : alt && !_optionTypesText;
            default:
                return false;
        }
    }
}
