namespace AsciiCanvas.Editor.Tui;

/// <summary>A row of buttons: draws them, remembers where they are for the mouse, and keeps the focus.</summary>
internal sealed class ButtonBar(IReadOnlyList<DialogButton> buttons)
{
    private readonly CellRect[] _rects = new CellRect[buttons.Count];

    /// <summary>The index of the highlighted button.</summary>
    public int Focus { get; set; }

    /// <summary>The columns needed to draw all buttons.</summary>
    public int Width => buttons.Sum(b => CharWidth.Of(b.Label) + 4) + (2 * (buttons.Count - 1));

    /// <summary>The answer of the highlighted button.</summary>
    public DialogChoice FocusedChoice => buttons[Focus].Choice;

    /// <summary>The answer Escape gives: Cancel or No if there is such a button, otherwise the first button.</summary>
    public DialogChoice EscapeChoice =>
        buttons.FirstOrDefault(b => b.Choice == DialogChoice.Cancel)?.Choice
        ?? buttons.FirstOrDefault(b => b.Choice == DialogChoice.No)?.Choice
        ?? buttons[0].Choice;

    /// <summary>Draws the buttons centered in a span of columns.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The first column of the span.</param>
    /// <param name="width">The width of the span.</param>
    /// <param name="y">The row.</param>
    public void Draw(CellCanvas canvas, int x, int width, int y)
    {
        var column = x + Math.Max(0, (width - Width) / 2);
        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            var style = i == Focus ? UiStyles.ButtonFocus : UiStyles.Button;
            var buttonWidth = CharWidth.Of(button.Label) + 4;
            _rects[i] = new CellRect(column, y, buttonWidth, 1);

            canvas.Write(column, y, "[ ", style);
            var labelX = column + 2;
            var underlined = false;
            foreach (var rune in button.Label.EnumerateRunes())
            {
                var hot = !underlined && char.ToUpperInvariant((char)rune.Value) == char.ToUpperInvariant(button.Hotkey);
                underlined |= hot;
                canvas.Write(labelX, y, rune.ToString(), hot ? style with { Attributes = style.Attributes | CellAttributes.Underline } : style);
                labelX += CharWidth.Of(rune);
            }

            canvas.Write(labelX, y, " ]", style);
            column += buttonWidth + 2;
        }
    }

    /// <summary>Finds the button under a cell.</summary>
    /// <param name="column">The grid column.</param>
    /// <param name="row">The grid row.</param>
    /// <returns>The button index, or -1.</returns>
    public int HitTest(int column, int row)
    {
        for (var i = 0; i < _rects.Length; i++)
        {
            if (_rects[i].Contains(column, row))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the button whose hotkey was typed.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="choice">The answer of that button.</param>
    public bool TryHotkey(string text, out DialogChoice choice)
    {
        var typed = text.Length == 1 ? char.ToUpperInvariant(text[0]) : '\0';
        var match = buttons.FirstOrDefault(b => char.ToUpperInvariant(b.Hotkey) == typed);
        choice = match?.Choice ?? DialogChoice.None;
        return match is not null;
    }

    /// <summary>Moves the focus to the next or previous button, wrapping around.</summary>
    /// <param name="delta">1 for next, -1 for previous.</param>
    public void MoveFocus(int delta) => Focus = (Focus + delta + buttons.Count) % buttons.Count;
}
