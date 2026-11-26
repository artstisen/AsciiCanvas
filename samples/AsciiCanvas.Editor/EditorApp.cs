using System.Runtime.InteropServices;
using System.Text;
using AsciiCanvas.Editor.Tui;

namespace AsciiCanvas.Editor;

/// <summary>
/// The editor program: reads keys, text, paste, mouse and close requests from the canvas, edits the model, and shows menus
/// and dialogs that are drawn with cells too. Menu entries, shortcuts and a close request all end up in <see cref="ExecuteAsync"/>.
/// </summary>
internal sealed class EditorApp(CellCanvas canvas, EditorModel model, string? path, string lineEnding, bool frameless)
{
    private const string Welcome = """
        Welcome to the AsciiCanvas editor
        =================================

        A small cross-platform text editor drawn on a character-cell canvas.
        Everything you see is cells: the menu, the line numbers, the selection,
        the scroll bar, and the dialogs for opening and saving files.

        What to try:
            - Click File or Edit in the menu bar, or press F10 or Alt+F / Alt+E.
            - File > Open shows a file browser. Click a file once to select it,
              again to open it. Text files only; other files are refused.
            - Type, and use Backspace, Delete, Enter (it keeps the indentation) and Tab.
            - Arrow keys, Home/End, PageUp/PageDown; Ctrl+arrows jump by word.
            - Hold Shift with any of those to select. Drag with the mouse to select.
            - Ctrl+C, Ctrl+X and Ctrl+V (Cmd on a Mac) use the system clipboard.
              Paste something from another program: it arrives as one paste event.
            - Ctrl+Z undoes, Ctrl+Y redoes.
            - Closing the window with unsaved changes asks first.
            - F3 shows how fast the canvas draws.

        Wide characters take two cells and stay aligned:
            日本語のテキスト   한국어   中文   😀 🚀

        Tabs align to the next tab stop:
        a	b	c
        long	column	text
        """;

    private readonly EditorView _view = new();
    private readonly MenuBar _menuBar = MenuBar.CreateDefault();
    private string? _path = path;
    private string _lineEnding = lineEnding;
    private string? _message;
    private bool _quit;
    private bool _dragging;
    private bool _followCaret = true;
    private WindowButton _hoverButton;

    /// <summary>Runs the editor on a canvas until the user exits or the window closes.</summary>
    /// <param name="canvas">The canvas to use.</param>
    /// <param name="path">The file to open, or <see langword="null"/> for a new document.</param>
    /// <param name="frameless">True when the window has no frame, so the editor draws its own window buttons and title bar.</param>
    public static async Task RunAsync(CellCanvas canvas, string? path, bool frameless = false)
    {
        // The window must not close before the editor has had the chance to ask about unsaved changes.
        canvas.InterceptClose = true;

        var message = default(string);
        var text = Welcome;
        var lineEnding = Environment.NewLine;

        if (path is not null)
        {
            if (!File.Exists(path))
            {
                text = "";
                message = "New file";
            }
            else
            {
                var result = await TextFile.ReadAsync(path, canvas.Closed);
                text = result.Text ?? "";
                lineEnding = result.LineEnding;
                message = result.Error;
                path = result.Succeeded ? path : null;
            }
        }

        var app = new EditorApp(canvas, new EditorModel(text), path, lineEnding, frameless) { _message = message };
        await app.RunAsync();
        canvas.Exit();
    }

    private ModalHost Modal => field ??= new ModalHost(canvas, DrawBackground);

    private string DisplayName => _path is null ? "Untitled" : Path.GetFileName(_path);

    private string StartDirectory => _path is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(_path)) ?? Environment.CurrentDirectory;

    private string LineEndingName => _lineEnding switch
    {
        "\r\n" => "CRLF",
        "\r" => "CR",
        _ => "LF",
    };

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

    // The editor and its menu bar, without presenting, so a menu or dialog can be drawn on top before the frame is shown.
    private void DrawBackground()
    {
        var name = DisplayName + (model.IsModified ? " *" : string.Empty);
        canvas.Title = $"{name} - AsciiCanvas Editor";
        _view.Draw(canvas, model, new EditorChrome(_message, LineEndingName), _followCaret);

        if (!frameless)
        {
            _menuBar.DrawBar(canvas, name);
            return;
        }

        // Without a frame, the top row is also the title bar: the free space between the menus and the buttons moves the window.
        _menuBar.DrawBar(canvas, name, WindowButtons.Width);
        WindowButtons.Draw(canvas, canvas.WindowState == CanvasWindowState.Maximized, _hoverButton);
        var menusEnd = _menuBar.TitleRect(_menuBar.Menus.Count - 1).Right;
        canvas.WindowDragRegions = [new CellRect(menusEnd, 0, Math.Max(0, canvas.Columns - WindowButtons.Width - menusEnd), 1)];
    }

    private void Redraw()
    {
        DrawBackground();
        canvas.Present();
    }

    private bool IsEnabled(EditorCommand command) => command switch
    {
        EditorCommand.Undo => model.CanUndo,
        EditorCommand.Redo => model.CanRedo,
        EditorCommand.Cut or EditorCommand.Copy => model.HasSelection,
        EditorCommand.Paste => canvas.Clipboard is not null,
        _ => true,
    };

    private async Task HandleAsync(InputEvent input)
    {
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
                await HandleKeyAsync(key);
                break;
            case MouseEvent mouse:
                await HandleMouseAsync(mouse);
                break;
            case CloseRequestedEvent:
                await ExecuteAsync(EditorCommand.Exit);
                break;
        }
    }

    private async Task HandleKeyAsync(KeyEvent key)
    {
        var shift = (key.Modifiers & KeyModifiers.Shift) != 0;
        var command = (key.Modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var alt = (key.Modifiers & KeyModifiers.Alt) != 0;

        if (alt && !command)
        {
            switch (key.Key)
            {
                case Key.F:
                    await OpenMenuAsync(0, fromKeyboard: true);
                    break;
                case Key.E:
                    await OpenMenuAsync(1, fromKeyboard: true);
                    break;
            }

            return;
        }

        if (alt)
        {
            // Ctrl+Alt is how some layouts type AltGr characters, which arrive as text.
            return;
        }

        _followCaret = true;

        if (command)
        {
            await HandleShortcutAsync(key.Key, shift);
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
            case Key.F10:
                await OpenMenuAsync(0, fromKeyboard: true);
                break;
        }
    }

    private async Task HandleShortcutAsync(Key key, bool shift)
    {
        switch (key)
        {
            case Key.N:
                await ExecuteAsync(EditorCommand.New);
                break;
            case Key.O:
                await ExecuteAsync(EditorCommand.Open);
                break;
            case Key.S:
                await ExecuteAsync(shift ? EditorCommand.SaveAs : EditorCommand.Save);
                break;
            case Key.Q:
                await ExecuteAsync(EditorCommand.Exit);
                break;
            case Key.Z:
                await ExecuteAsync(shift ? EditorCommand.Redo : EditorCommand.Undo);
                break;
            case Key.Y:
                await ExecuteAsync(EditorCommand.Redo);
                break;
            case Key.X:
                await ExecuteAsync(EditorCommand.Cut);
                break;
            case Key.C:
                await ExecuteAsync(EditorCommand.Copy);
                break;
            case Key.V:
                await ExecuteAsync(EditorCommand.Paste);
                break;
            case Key.A:
                await ExecuteAsync(EditorCommand.SelectAll);
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

    private async Task HandleMouseAsync(MouseEvent mouse)
    {
        if (frameless)
        {
            _hoverButton = WindowButtons.At(canvas, mouse.Column, mouse.Row);
            if (mouse is { Action: MouseAction.Press, Button: MouseButton.Left } && _hoverButton != WindowButton.None)
            {
                await ClickWindowButtonAsync(_hoverButton);
                return;
            }
        }

        switch (mouse)
        {
            case { Action: MouseAction.Press, Button: MouseButton.Left, Row: 0 }:
                var title = _menuBar.TitleAt(mouse.Column, mouse.Row);
                if (title >= 0)
                {
                    await OpenMenuAsync(title, fromKeyboard: false);
                }

                break;
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

    private async Task ClickWindowButtonAsync(WindowButton button)
    {
        switch (button)
        {
            case WindowButton.Minimize:
                canvas.WindowState = CanvasWindowState.Minimized;
                break;
            case WindowButton.Maximize:
                canvas.WindowState = canvas.WindowState == CanvasWindowState.Maximized ? CanvasWindowState.Normal : CanvasWindowState.Maximized;
                break;
            case WindowButton.Close:
                await ExecuteAsync(EditorCommand.Exit);
                break;
        }
    }

    private async Task OpenMenuAsync(int index, bool fromKeyboard)
    {
        _dragging = false;
        var chosen = await MenuLoop.RunAsync(Modal, _menuBar, index, fromKeyboard, IsEnabled);
        if (chosen is { } command)
        {
            await ExecuteAsync(command);
        }
    }

    private async Task ExecuteAsync(EditorCommand command)
    {
        _followCaret = true;
        switch (command)
        {
            case EditorCommand.New:
                await NewAsync();
                break;
            case EditorCommand.Open:
                await OpenAsync();
                break;
            case EditorCommand.Save:
                await SaveAsync();
                break;
            case EditorCommand.SaveAs:
                await SaveAsAsync();
                break;
            case EditorCommand.Exit:
                _quit = await ConfirmLeaveAsync();
                break;
            case EditorCommand.Undo:
                _message = model.Undo() ? null : "Nothing to undo";
                break;
            case EditorCommand.Redo:
                _message = model.Redo() ? null : "Nothing to redo";
                break;
            case EditorCommand.Cut:
                if (await CopyAsync())
                {
                    model.DeleteSelection();
                }

                break;
            case EditorCommand.Copy:
                await CopyAsync();
                break;
            case EditorCommand.Paste:
                await PasteAsync();
                break;
            case EditorCommand.SelectAll:
                model.SelectAll();
                break;
        }
    }

    private Task<DialogChoice> AskAsync(string title, string message, params DialogButton[] buttons) =>
        MessageDialog.ShowAsync(Modal, title, message, buttons);

    // Asks what to do with unsaved changes. True means the document may be replaced or closed.
    private async Task<bool> ConfirmLeaveAsync()
    {
        if (!model.IsModified)
        {
            return true;
        }

        var choice = await AskAsync(
            "Unsaved changes",
            $"Save changes to {DisplayName}?",
            new DialogButton("Save", DialogChoice.Save, 'S'),
            new DialogButton("Don't save", DialogChoice.DontSave, 'D'),
            new DialogButton("Cancel", DialogChoice.Cancel, 'C'));

        return choice switch
        {
            DialogChoice.Save => await SaveAsync(),
            DialogChoice.DontSave => true,
            _ => false,
        };
    }

    private async Task NewAsync()
    {
        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        Load("", null, Environment.NewLine);
        _message = "New document";
    }

    private async Task OpenAsync()
    {
        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        var chosen = await new FileDialog(Modal, FileDialogMode.Open, StartDirectory, "").RunAsync();
        if (chosen is null)
        {
            return;
        }

        var result = await TextFile.ReadAsync(chosen, canvas.Closed);
        if (!result.Succeeded)
        {
            await AskAsync("Cannot open file", $"{Path.GetFileName(chosen)}\n{result.Error}", new DialogButton("OK", DialogChoice.Ok, 'O'));
            return;
        }

        Load(result.Text!, chosen, result.LineEnding);
        _message = $"Opened {Path.GetFileName(chosen)}";
    }

    private void Load(string text, string? newPath, string newLineEnding)
    {
        model.Load(text);
        _path = newPath;
        _lineEnding = newLineEnding;
        _view.Reset();
    }

    private async Task<bool> SaveAsync() => _path is null ? await SaveAsAsync() : await WriteAsync(_path);

    private async Task<bool> SaveAsAsync()
    {
        var chosen = await new FileDialog(Modal, FileDialogMode.Save, StartDirectory, _path is null ? "" : Path.GetFileName(_path)).RunAsync();
        if (chosen is null)
        {
            _message = "Save canceled";
            return false;
        }

        var sameFile = _path is not null && string.Equals(Path.GetFullPath(_path), chosen, StringComparison.OrdinalIgnoreCase);
        if (File.Exists(chosen) && !sameFile)
        {
            var replace = await AskAsync(
                "Replace file?",
                $"{Path.GetFileName(chosen)} already exists. Replace it?",
                new DialogButton("Replace", DialogChoice.Yes, 'R'),
                new DialogButton("Cancel", DialogChoice.Cancel, 'C'));
            if (replace != DialogChoice.Yes)
            {
                _message = "Save canceled";
                return false;
            }
        }

        return await WriteAsync(chosen);
    }

    private async Task<bool> WriteAsync(string target)
    {
        try
        {
            await File.WriteAllTextAsync(target, model.Text.ReplaceLineEndings(_lineEnding), new UTF8Encoding(false), canvas.Closed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await AskAsync("Cannot save file", $"{Path.GetFileName(target)}\n{ex.Message}", new DialogButton("OK", DialogChoice.Ok, 'O'));
            return false;
        }

        _path = target;
        model.MarkSaved();
        _message = $"Saved {model.Buffer.LineCount} lines to {Path.GetFileName(target)}";
        return true;
    }

    private async Task<bool> CopyAsync()
    {
        var text = model.SelectedText;
        if (text.Length == 0)
        {
            _message = "Nothing selected";
            return false;
        }

        if (canvas.Clipboard is null)
        {
            _message = "No clipboard available";
            return false;
        }

        try
        {
            await canvas.Clipboard.SetTextAsync(text, canvas.Closed);
        }
        catch (ExternalException)
        {
            // Another program can hold the clipboard for a moment; the editor should carry on.
            _message = "Could not use the clipboard; try again";
            return false;
        }

        _message = $"Copied {text.Length} characters";
        return true;
    }

    // Reached from the menu, or when the host does not turn the paste shortcut into a paste event itself.
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
}
