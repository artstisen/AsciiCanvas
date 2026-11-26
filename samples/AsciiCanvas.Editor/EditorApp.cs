using System.Runtime.InteropServices;
using System.Text;

namespace AsciiCanvas.Editor;

/// <summary>The editor program: reads keys, text, paste and mouse input from the canvas, edits the model and redraws.</summary>
internal sealed class EditorApp(CellCanvas canvas, EditorModel model, string? path, string lineEnding)
{
    private const string Welcome = """
        Welcome to the AsciiCanvas editor
        =================================

        A small cross-platform text editor drawn on a character-cell canvas.
        Everything you see is cells: the line numbers, the selection, the scroll bar.

        What to try:
            - Type, and use Backspace, Delete, Enter (it keeps the indentation) and Tab.
            - Arrow keys, Home/End, PageUp/PageDown; Ctrl+arrows jump by word.
            - Hold Shift with any of those to select. Drag with the mouse to select.
            - Ctrl+C, Ctrl+X and Ctrl+V (Cmd on a Mac) use the system clipboard.
              Paste something from another program: it arrives as one paste event.
            - Ctrl+Z undoes, Ctrl+Y redoes.
            - Ctrl+S saves. This text has no file yet, so you are asked for a name.
            - F3 shows how fast the canvas draws.

        Wide characters take two cells and stay aligned:
            日本語のテキスト   한국어   中文   😀 🚀

        Tabs align to the next tab stop:
        a	b	c
        long	column	text
        """;

    private readonly EditorView _view = new();
    private string? _path = path;
    private string? _message;
    private bool _quit;
    private bool _quitArmed;
    private bool _dragging;
    private bool _followCaret = true;

    /// <summary>Runs the editor on a canvas until the user quits or the window closes.</summary>
    /// <param name="canvas">The canvas to use.</param>
    /// <param name="path">The file to open, or <see langword="null"/> for a new document.</param>
    public static async Task RunAsync(CellCanvas canvas, string? path)
    {
        var message = default(string);
        var text = Welcome;
        var lineEnding = Environment.NewLine;

        if (path is not null)
        {
            try
            {
                text = File.Exists(path) ? await File.ReadAllTextAsync(path, canvas.Closed) : "";
                lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\n') ? "\n" : lineEnding;
                message = File.Exists(path) ? null : "New file";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                text = "";
                message = $"Could not open {path}: {ex.Message}";
            }
        }

        var app = new EditorApp(canvas, new EditorModel(text), path, lineEnding) { _message = message };
        await app.RunAsync();
    }

    private async Task RunAsync()
    {
        canvas.CursorShape = CursorShape.Bar;
        canvas.CursorVisible = true;

        while (!_quit)
        {
            Redraw();

            // Handle everything that is waiting before drawing again, so a long paste or key repeat draws once.
            var input = await canvas.Input.ReadAsync(canvas.Closed);
            do
            {
                await HandleAsync(input);
            }
            while (!_quit && canvas.Input.TryRead(out input));
        }
    }

    private string LineEndingName => lineEnding switch
    {
        "\r\n" => "CRLF",
        "\r" => "CR",
        _ => "LF",
    };

    private void Redraw(string? prompt = null, string promptInput = "")
    {
        var title = (_path is null ? "Untitled" : Path.GetFileName(_path)) + (model.IsModified ? " *" : "");
        _view.Draw(canvas, model, new EditorChrome(title, _message, LineEndingName, prompt, promptInput), _followCaret);
    }

    private async Task HandleAsync(InputEvent input)
    {
        var armed = _quitArmed;
        _quitArmed = false;
        _message = null;

        switch (input)
        {
            case TextEvent text:
                _followCaret = true;
                model.InsertText(text.Text);
                break;
            case PasteEvent paste:
                _followCaret = true;
                model.InsertText(paste.Text);
                _message = $"Pasted {paste.Text.Length} characters";
                break;
            case KeyEvent key:
                await HandleKeyAsync(key, armed);
                break;
            case MouseEvent mouse:
                HandleMouse(mouse);
                break;
        }
    }

    private async Task HandleKeyAsync(KeyEvent key, bool quitArmed)
    {
        var shift = (key.Modifiers & KeyModifiers.Shift) != 0;
        var command = (key.Modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var alt = (key.Modifiers & KeyModifiers.Alt) != 0;
        if (alt)
        {
            // Alt combinations are not used; Ctrl+Alt is how some layouts type AltGr characters, which arrive as text.
            return;
        }

        _followCaret = true;

        if (command)
        {
            await HandleCommandAsync(key.Key, shift, quitArmed);
            return;
        }

        switch (key.Key)
        {
            case Key.Left:
                model.MoveLeft(shift);
                break;
            case Key.Right:
                model.MoveRight(shift);
                break;
            case Key.Up:
                model.MoveUp(shift);
                break;
            case Key.Down:
                model.MoveDown(shift);
                break;
            case Key.Home:
                model.MoveHome(shift);
                break;
            case Key.End:
                model.MoveEnd(shift);
                break;
            case Key.PageUp:
                model.MoveLines(-Math.Max(1, _view.PageSize - 1), shift);
                break;
            case Key.PageDown:
                model.MoveLines(Math.Max(1, _view.PageSize - 1), shift);
                break;
            case Key.Enter:
                model.InsertNewLine();
                break;
            case Key.Back:
                model.Backspace();
                break;
            case Key.Delete:
                model.DeleteForward();
                break;
            case Key.Tab:
                model.InsertTab();
                break;
            case Key.Escape:
                model.ClearSelection();
                break;
        }
    }

    private async Task HandleCommandAsync(Key key, bool shift, bool quitArmed)
    {
        switch (key)
        {
            case Key.S:
                await SaveAsync();
                break;
            case Key.Q:
                RequestQuit(quitArmed);
                break;
            case Key.Z when shift:
            case Key.Y:
                _message = model.Redo() ? null : "Nothing to redo";
                break;
            case Key.Z:
                _message = model.Undo() ? null : "Nothing to undo";
                break;
            case Key.A:
                model.SelectAll();
                break;
            case Key.C:
                await CopyAsync();
                break;
            case Key.X:
                await CopyAsync();
                model.DeleteSelection();
                break;
            case Key.V:
                await PasteAsync();
                break;
            case Key.Left:
                model.MoveWordLeft(shift);
                break;
            case Key.Right:
                model.MoveWordRight(shift);
                break;
            case Key.Home:
                model.MoveToStart(shift);
                break;
            case Key.End:
                model.MoveToEnd(shift);
                break;
        }
    }

    private void HandleMouse(MouseEvent mouse)
    {
        switch (mouse)
        {
            case { Action: MouseAction.Press, Button: MouseButton.Left }:
                _followCaret = true;
                _dragging = true;
                model.MoveTo(_view.HitTest(model, mouse.Column, mouse.Row), extend: (mouse.Modifiers & KeyModifiers.Shift) != 0);
                break;
            case { Action: MouseAction.Move, Button: MouseButton.Left } when _dragging:
                _followCaret = true;
                model.MoveTo(_view.HitTest(model, mouse.Column, mouse.Row), extend: true);
                break;
            case { Action: MouseAction.Release }:
                _dragging = false;
                break;
            case { Action: MouseAction.Wheel }:
                // Scrolling looks around without moving the caret, so the view must not snap back to it.
                _followCaret = false;
                _view.Scroll(model, -mouse.WheelDelta * 3);
                break;
        }
    }

    private void RequestQuit(bool quitArmed)
    {
        if (!model.IsModified || quitArmed)
        {
            _quit = true;
            return;
        }

        _quitArmed = true;
        _message = "Unsaved changes. Press Ctrl+Q again to quit without saving, or Ctrl+S to save.";
    }

    private async Task CopyAsync()
    {
        var text = model.SelectedText;
        if (text.Length == 0)
        {
            _message = "Nothing selected";
        }
        else if (canvas.Clipboard is null)
        {
            _message = "No clipboard available";
        }
        else
        {
            try
            {
                await canvas.Clipboard.SetTextAsync(text, canvas.Closed);
                _message = $"Copied {text.Length} characters";
            }
            catch (ExternalException)
            {
                // Another program can hold the clipboard for a moment; the editor should carry on.
                _message = "Could not use the clipboard; try again";
            }
        }
    }

    // Reached only when the host does not turn the paste shortcut into a paste event itself.
    private async Task PasteAsync()
    {
        string? text;
        try
        {
            text = canvas.Clipboard is null ? null : await canvas.Clipboard.GetTextAsync(canvas.Closed);
        }
        catch (ExternalException)
        {
            _message = "Could not use the clipboard; try again";
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            model.InsertText(text);
            _message = $"Pasted {text.Length} characters";
        }
    }

    private async Task SaveAsync()
    {
        var target = _path ?? await PromptAsync("Save as: ");
        if (string.IsNullOrWhiteSpace(target))
        {
            _message = "Save canceled";
            return;
        }

        try
        {
            await File.WriteAllTextAsync(target, model.Text.ReplaceLineEndings(lineEnding), new UTF8Encoding(false), canvas.Closed);
            _path = target;
            model.MarkSaved();
            _message = $"Saved {model.Buffer.LineCount} lines to {Path.GetFileName(target)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"Could not save: {ex.Message}";
        }
    }

    private async Task<string?> PromptAsync(string prompt)
    {
        var answer = new StringBuilder();
        while (true)
        {
            Redraw(prompt, answer.ToString());
            switch (await canvas.Input.ReadAsync(canvas.Closed))
            {
                case TextEvent text:
                    answer.Append(text.Text);
                    break;
                case PasteEvent paste:
                    answer.Append(paste.Text.ReplaceLineEndings(" "));
                    break;
                case KeyEvent { Key: Key.Back } when answer.Length > 0:
                    answer.Length--;
                    break;
                case KeyEvent { Key: Key.Enter }:
                    return answer.ToString();
                case KeyEvent { Key: Key.Escape }:
                    return null;
            }
        }
    }
}
