using System.Globalization;
using System.Text;

namespace AsciiCanvas.Editor.Tui;

/// <summary>Whether the file dialog picks a file that must exist, or a name to save under.</summary>
internal enum FileDialogMode
{
    Open,
    Save,
}

/// <summary>
/// A file browser drawn with cells: the folder, a list of folders and files with sizes, a name field and buttons.
/// It works with the keyboard (arrows, Enter, typing, Backspace) and the mouse (click to select, click again to open, wheel, buttons).
/// </summary>
internal sealed class FileDialog(ModalHost host, FileDialogMode mode, string startDirectory, string initialName)
{
    private readonly FileBrowserModel _model = new(startDirectory);
    private readonly StringBuilder _field = new(initialName);
    private readonly ButtonBar _buttons = new([
        new DialogButton(mode == FileDialogMode.Open ? "Open" : "Save", DialogChoice.Ok, '\0'),
        new DialogButton("Cancel", DialogChoice.Cancel, '\0'),
    ]);

    private int _top;
    private string? _hint;

    // True while the name field holds text the user has not edited yet: typing then replaces it, like a selected field.
    private bool _replaceOnType = initialName.Length > 0;

    /// <summary>Shows the dialog and waits.</summary>
    /// <returns>The full path chosen, or <see langword="null"/> if the user canceled.</returns>
    public async Task<string?> RunAsync()
    {
        var canvas = host.Canvas;
        while (true)
        {
            var frame = Layout(canvas);
            KeepSelectionVisible(frame.List.Height);

            host.DrawBackground();
            Draw(canvas, frame);
            canvas.Present();

            var outcome = await canvas.Input.ReadAsync(canvas.Closed) switch
            {
                KeyEvent key => HandleKey(key, frame),
                MouseEvent mouse => HandleMouse(mouse, frame),
                TextEvent text => Type(text.Text),
                PasteEvent paste => Type(paste.Text.ReplaceLineEndings(" ")),
                CloseRequestedEvent closeRequest => PassOnCloseRequest(canvas, closeRequest),
                _ => Outcome.Continue,
            };

            if (outcome.Done)
            {
                return outcome.Path;
            }
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F1} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):F1} MB"),
    };

    private static Outcome PassOnCloseRequest(CellCanvas canvas, CloseRequestedEvent closeRequest)
    {
        // The user closed the window while the dialog was open: cancel the dialog and let the editor handle the request.
        canvas.Input.Post(closeRequest);
        return Outcome.Cancel;
    }

    private static Frame Layout(CellCanvas canvas)
    {
        var outer = DialogBox.Center(canvas, Math.Min(76, canvas.Columns - 4), Math.Clamp(canvas.Rows - 2, 14, 26));
        var listHeight = Math.Max(3, outer.Height - 8);
        var list = new CellRect(outer.X + 2, outer.Y + 3, outer.Width - 4, listHeight);
        return new Frame(outer, list, list.Bottom + 1);
    }

    private void KeepSelectionVisible(int height)
    {
        var selected = _model.Selected;
        if (selected < _top)
        {
            _top = selected;
        }
        else if (selected >= _top + height)
        {
            _top = selected - height + 1;
        }

        _top = Math.Clamp(_top, 0, Math.Max(0, _model.Entries.Count - height));
    }

    private void Draw(CellCanvas canvas, Frame frame)
    {
        var outer = frame.Outer;
        DialogBox.Draw(canvas, outer, mode == FileDialogMode.Open ? "Open file" : "Save as");

        var folder = _model.Folder ?? "(drives)";
        canvas.Write(outer.X + 2, outer.Y + 1, "Folder: " + TextFit.Truncate(folder, outer.Width - 4 - 8, fromStart: true), UiStyles.Panel);

        canvas.Box(new CellRect(outer.X + 1, outer.Y + 2, outer.Width - 2, frame.List.Height + 2), BoxStyle.SingleLine, UiStyles.Panel);
        DrawList(canvas, frame);

        var fieldX = outer.X + 8;
        var fieldWidth = outer.Right - 2 - fieldX;
        canvas.Write(outer.X + 2, frame.FieldY, "Name:", UiStyles.Panel);
        canvas.Fill(new CellRect(fieldX, frame.FieldY, fieldWidth, 1), ' ', _replaceOnType ? UiStyles.PanelSelected : UiStyles.Field);
        var shown = TextFit.Truncate(_field.ToString(), fieldWidth - 1, fromStart: true);
        canvas.Write(fieldX, frame.FieldY, shown, _replaceOnType ? UiStyles.PanelSelected : UiStyles.Field);

        var message = _hint ?? _model.Error ?? "Enter: open   Esc: cancel   Backspace: up a folder";
        canvas.Write(outer.X + 2, frame.FieldY + 1, TextFit.Truncate(message, outer.Width - 4), UiStyles.PanelDisabled);
        _buttons.Draw(canvas, outer.X, outer.Width, frame.FieldY + 2);

        canvas.CursorShape = CursorShape.Bar;
        canvas.CursorVisible = true;
        canvas.SetCursor(Math.Min(fieldX + CharWidth.Of(shown), fieldX + fieldWidth - 1), frame.FieldY);
    }

    private void DrawList(CellCanvas canvas, Frame frame)
    {
        var list = frame.List;
        var entries = _model.Entries;
        var sizeWidth = 10;

        for (var row = 0; row < list.Height; row++)
        {
            var index = _top + row;
            var y = list.Y + row;
            if (index >= entries.Count)
            {
                canvas.Fill(new CellRect(list.X, y, list.Width, 1), ' ', UiStyles.Panel);
                continue;
            }

            var entry = entries[index];
            var style = index == _model.Selected ? UiStyles.PanelSelected : UiStyles.Panel;
            canvas.Fill(new CellRect(list.X, y, list.Width, 1), ' ', style);

            var name = entry.Kind switch
            {
                FileEntryKind.Parent => "..",
                FileEntryKind.Directory => entry.Name + Path.DirectorySeparatorChar,
                _ => entry.Name,
            };
            canvas.Write(list.X + 1, y, TextFit.Truncate(name, list.Width - sizeWidth - 3), style);

            if (entry.Kind == FileEntryKind.File)
            {
                var size = FormatSize(entry.Size);
                canvas.Write(list.Right - 1 - CharWidth.Of(size), y, size, style);
            }
        }

        // The border column to the right of the list doubles as a scroll bar.
        var total = entries.Count;
        if (total > list.Height)
        {
            var thumb = Math.Max(1, list.Height * list.Height / total);
            var travel = list.Height - thumb;
            var top = travel * _top / Math.Max(1, total - list.Height);
            canvas.Fill(new CellRect(list.Right, list.Y + Math.Clamp(top, 0, travel), 1, thumb), '█', UiStyles.Panel);
        }
    }

    private Outcome HandleKey(KeyEvent key, Frame frame)
    {
        _hint = null;
        var page = Math.Max(1, frame.List.Height - 1);
        switch (key.Key)
        {
            case Key.Escape:
                return Outcome.Cancel;
            case Key.Enter:
                return Accept();
            case Key.Up:
                MoveSelection(-1);
                break;
            case Key.Down:
                MoveSelection(1);
                break;
            case Key.PageUp:
                MoveSelection(-page);
                break;
            case Key.PageDown:
                MoveSelection(page);
                break;
            case Key.Home:
                MoveSelection(-_model.Entries.Count);
                break;
            case Key.End:
                MoveSelection(_model.Entries.Count);
                break;
            case Key.Back when _field.Length > 0:
                _replaceOnType = false;
                _field.Length = TextMeasure.PreviousIndex(_field.ToString(), _field.Length);
                break;
            case Key.Back:
                _model.GoUp();
                break;
        }

        return Outcome.Continue;
    }

    private Outcome HandleMouse(MouseEvent mouse, Frame frame)
    {
        var list = frame.List;
        var inList = list.Contains(mouse.Column, mouse.Row);

        switch (mouse)
        {
            case { Action: MouseAction.Move } when _buttons.HitTest(mouse.Column, mouse.Row) is var hover and >= 0:
                _buttons.Focus = hover;
                break;
            case { Action: MouseAction.Wheel } when inList:
                _top = Math.Clamp(_top - (mouse.WheelDelta * 3), 0, Math.Max(0, _model.Entries.Count - list.Height));
                break;
            case { Action: MouseAction.Press, Button: MouseButton.Left }:
                _hint = null;
                if (_buttons.HitTest(mouse.Column, mouse.Row) is var button and >= 0)
                {
                    return button == 0 ? Accept() : Outcome.Cancel;
                }

                if (inList)
                {
                    return ClickEntry(_top + mouse.Row - list.Y);
                }

                break;
        }

        return Outcome.Continue;
    }

    // The first click selects an entry; clicking the selected entry again opens it.
    private Outcome ClickEntry(int index)
    {
        if (index < 0 || index >= _model.Entries.Count)
        {
            return Outcome.Continue;
        }

        if (index != _model.Selected)
        {
            _model.Select(index);
            ShowSelectedFileName();
            return Outcome.Continue;
        }

        var file = _model.ActivateSelected();
        _top = 0;
        return file is null ? Outcome.Continue : new Outcome(true, file);
    }

    private Outcome Type(string text)
    {
        _hint = null;
        if (_replaceOnType)
        {
            _field.Clear();
            _replaceOnType = false;
        }

        _field.Append(text);
        return Outcome.Continue;
    }

    private void MoveSelection(int delta)
    {
        _model.Select(_model.Selected + delta);
        ShowSelectedFileName();
    }

    private void ShowSelectedFileName()
    {
        if (_model.SelectedEntry is { Kind: FileEntryKind.File } file)
        {
            _field.Clear().Append(file.Name);
            _replaceOnType = true;
        }
    }

    // What Enter and the main button do: use the typed name if there is one, otherwise the selected entry.
    private Outcome Accept()
    {
        var name = _field.ToString().Trim();
        if (name.Length == 0)
        {
            var file = _model.ActivateSelected();
            _top = 0;
            if (file is null)
            {
                return Outcome.Continue;
            }

            name = Path.GetFileName(file);
        }

        string? path;
        try
        {
            path = Resolve(name);
        }
        catch (ArgumentException)
        {
            _hint = "That is not a valid file name.";
            return Outcome.Continue;
        }

        if (path is null)
        {
            _hint = "Choose a drive first.";
            return Outcome.Continue;
        }

        if (Directory.Exists(path))
        {
            _model.Navigate(path);
            _field.Clear();
            _top = 0;
            return Outcome.Continue;
        }

        if (mode == FileDialogMode.Open && !File.Exists(path))
        {
            _hint = "There is no such file.";
            return Outcome.Continue;
        }

        if (Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(path).Length == 0)
        {
            _hint = "That is not a valid file name.";
            return Outcome.Continue;
        }

        return new Outcome(true, path);
    }

    private string? Resolve(string name) =>
        Path.IsPathRooted(name) ? Path.GetFullPath(name)
        : _model.Folder is null ? null
        : Path.GetFullPath(Path.Combine(_model.Folder, name));

    private readonly record struct Frame(CellRect Outer, CellRect List, int FieldY);

    private readonly record struct Outcome(bool Done, string? Path)
    {
        public static Outcome Continue => new(false, null);

        public static Outcome Cancel => new(true, null);
    }
}
