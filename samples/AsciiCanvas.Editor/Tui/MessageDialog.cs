namespace AsciiCanvas.Editor.Tui;

/// <summary>A modal message with buttons, drawn over the editor.</summary>
internal static class MessageDialog
{
    /// <summary>Shows a message and waits for the user to choose a button.</summary>
    /// <param name="host">The canvas and the editor background.</param>
    /// <param name="title">The title in the top edge.</param>
    /// <param name="message">The message; <c>\n</c> starts a new line.</param>
    /// <param name="buttons">The buttons, left to right. The first one is focused.</param>
    /// <returns>The choice of the button that was pressed.</returns>
    public static async Task<DialogChoice> ShowAsync(ModalHost host, string title, string message, params DialogButton[] buttons)
    {
        var canvas = host.Canvas;
        var bar = new ButtonBar(buttons);

        while (true)
        {
            var width = Math.Clamp(Math.Max(bar.Width, CharWidth.Of(title)) + 8, 40, Math.Max(20, canvas.Columns - 4));
            var lines = TextFit.Wrap(message, width - 6);
            var rect = DialogBox.Center(canvas, width, lines.Count + 6);

            host.DrawBackground();
            DialogBox.Draw(canvas, rect, title);
            for (var i = 0; i < lines.Count; i++)
            {
                canvas.Write(rect.X + 3, rect.Y + 2 + i, lines[i], UiStyles.Panel);
            }

            bar.Draw(canvas, rect.X, rect.Width, rect.Bottom - 2);
            canvas.CursorVisible = false;
            canvas.Present();

            switch (await canvas.Input.ReadAsync(canvas.Closed))
            {
                case KeyEvent { Key: Key.Enter }:
                    return bar.FocusedChoice;
                case KeyEvent { Key: Key.Escape }:
                    return bar.EscapeChoice;
                case KeyEvent { Key: Key.Left or Key.Up }:
                    bar.MoveFocus(-1);
                    break;
                case KeyEvent { Key: Key.Right or Key.Down or Key.Tab }:
                    bar.MoveFocus(1);
                    break;
                case TextEvent text when bar.TryHotkey(text.Text, out var hotkeyChoice):
                    return hotkeyChoice;
                case MouseEvent { Action: MouseAction.Move } move when bar.HitTest(move.Column, move.Row) is var hover and >= 0:
                    bar.Focus = hover;
                    break;
                case MouseEvent { Action: MouseAction.Press, Button: MouseButton.Left } press when bar.HitTest(press.Column, press.Row) is var hit and >= 0:
                    bar.Focus = hit;
                    return bar.FocusedChoice;
                case CloseRequestedEvent closeRequest:
                    // The user closed the window while a question was open: answer it as canceled and let the editor handle the request.
                    canvas.Input.Post(closeRequest);
                    return bar.EscapeChoice;
            }
        }
    }
}
