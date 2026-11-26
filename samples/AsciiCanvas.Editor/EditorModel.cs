namespace AsciiCanvas.Editor;

/// <summary>
/// A text document being edited: the text, a caret with an optional selection, editing and movement commands, and undo.
/// It knows nothing about screens or keys, so it can be tested on its own.
/// </summary>
public sealed class EditorModel
{
    private readonly EditHistory _history = new();
    private int _version;
    private int _savedVersion;

    /// <summary>Creates a document.</summary>
    /// <param name="text">The initial text; any kind of line break is accepted.</param>
    public EditorModel(string text = "") => Load(text);

    /// <summary>The text of the document.</summary>
    public TextBuffer Buffer { get; } = new();

    /// <summary>The distance between tab stops, in grid columns.</summary>
    public int TabSize { get; init; } = 4;

    /// <summary>The caret: where typing goes, and the moving end of the selection.</summary>
    public TextPosition Caret { get; private set; }

    /// <summary>The fixed end of the selection. Equal to <see cref="Caret"/> when nothing is selected.</summary>
    public TextPosition Anchor { get; private set; }

    /// <summary>True if some text is selected.</summary>
    public bool HasSelection => Caret != Anchor;

    /// <summary>The start of the selection in reading order.</summary>
    public TextPosition SelectionStart => Caret < Anchor ? Caret : Anchor;

    /// <summary>The end of the selection in reading order.</summary>
    public TextPosition SelectionEnd => Caret < Anchor ? Anchor : Caret;

    /// <summary>The selected text, or an empty string.</summary>
    public string SelectedText => HasSelection ? Buffer.GetText(SelectionStart, SelectionEnd) : "";

    /// <summary>The whole text, with <c>\n</c> between lines.</summary>
    public string Text => Buffer.Text;

    /// <summary>True if the text changed since it was loaded or last marked as saved.</summary>
    public bool IsModified => _version != _savedVersion;

    /// <summary>True if there is a change to undo.</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>True if there is an undone change to redo.</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>The grid column the caret keeps when moving up and down over lines of different length.</summary>
    public int PreferredColumn { get; private set; }

    /// <summary>Replaces the document, puts the caret at the start and forgets all undo history.</summary>
    /// <param name="text">The new text; any kind of line break is accepted.</param>
    public void Load(string text)
    {
        Buffer.SetText(text);
        Caret = default;
        Anchor = default;
        PreferredColumn = 0;
        _history.Clear();
        _version = 0;
        _savedVersion = 0;
    }

    /// <summary>Records that the current text has been written to disk.</summary>
    public void MarkSaved() => _savedVersion = _version;

    /// <summary>Gets the grid column of a position, where tabs are expanded and wide characters take two columns.</summary>
    /// <param name="position">The position.</param>
    public int DisplayColumn(TextPosition position) =>
        TextMeasure.DisplayColumn(Buffer.GetLine(position.Line), position.Column, TabSize);

    /// <summary>Inserts text at the caret, replacing the selection. The caret ends up after the text.</summary>
    /// <param name="text">The text, which may span several lines.</param>
    public void InsertText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = TextBuffer.Normalize(text);
        if (normalized.Length > 0 || HasSelection)
        {
            Replace(SelectionStart, SelectionEnd, normalized);
        }
    }

    /// <summary>Starts a new line, repeating the indentation of the current one.</summary>
    public void InsertNewLine()
    {
        var start = SelectionStart;
        var beforeCaret = Buffer.GetLine(start.Line)[..start.Column];
        InsertText("\n" + beforeCaret[..LeadingWhitespace(beforeCaret)]);
    }

    /// <summary>Inserts spaces up to the next tab stop.</summary>
    public void InsertTab() => InsertText(new string(' ', TabSize - (DisplayColumn(SelectionStart) % TabSize)));

    /// <summary>Deletes the selection, or the character before the caret (joining lines at the start of a line).</summary>
    public void Backspace()
    {
        if (HasSelection)
        {
            DeleteSelection();
        }
        else if (Caret.Column > 0)
        {
            var line = Buffer.GetLine(Caret.Line);
            Replace(new TextPosition(Caret.Line, TextMeasure.PreviousIndex(line, Caret.Column)), Caret, "");
        }
        else if (Caret.Line > 0)
        {
            Replace(new TextPosition(Caret.Line - 1, Buffer.GetLine(Caret.Line - 1).Length), Caret, "");
        }
    }

    /// <summary>Deletes the selection, or the character after the caret (joining lines at the end of a line).</summary>
    public void DeleteForward()
    {
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }

        var line = Buffer.GetLine(Caret.Line);
        if (Caret.Column < line.Length)
        {
            Replace(Caret, new TextPosition(Caret.Line, TextMeasure.NextIndex(line, Caret.Column)), "");
        }
        else if (Caret.Line < Buffer.LineCount - 1)
        {
            Replace(Caret, new TextPosition(Caret.Line + 1, 0), "");
        }
    }

    /// <summary>Deletes the selected text.</summary>
    /// <returns>The deleted text; empty if nothing was selected.</returns>
    public string DeleteSelection()
    {
        var removed = SelectedText;
        if (removed.Length > 0)
        {
            Replace(SelectionStart, SelectionEnd, "");
        }

        return removed;
    }

    /// <summary>Undoes the last change.</summary>
    /// <returns>False if there was nothing to undo.</returns>
    public bool Undo()
    {
        if (!_history.TryUndo(out var edit))
        {
            return false;
        }

        Buffer.Delete(edit.Start, TextBuffer.Advance(edit.Start, edit.Inserted));
        Buffer.Insert(edit.Start, edit.Removed);
        Caret = edit.CaretBefore;
        Anchor = edit.AnchorBefore;
        PreferredColumn = DisplayColumn(Caret);
        _version++;
        return true;
    }

    /// <summary>Redoes the last undone change.</summary>
    /// <returns>False if there was nothing to redo.</returns>
    public bool Redo()
    {
        if (!_history.TryRedo(out var edit))
        {
            return false;
        }

        Buffer.Delete(edit.Start, TextBuffer.Advance(edit.Start, edit.Removed));
        Buffer.Insert(edit.Start, edit.Inserted);
        Move(edit.CaretAfter, extend: false);
        _version++;
        return true;
    }

    /// <summary>Moves the caret, optionally keeping the anchor so the text in between is selected.</summary>
    /// <param name="position">Where to move; moved to the nearest valid position.</param>
    /// <param name="extend">True to extend the selection, false to drop it.</param>
    public void MoveTo(TextPosition position, bool extend) => Move(position, extend);

    /// <summary>Moves one character left; with a selection and no <paramref name="extend"/>, to its start.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveLeft(bool extend)
    {
        if (HasSelection && !extend)
        {
            Move(SelectionStart, extend: false);
        }
        else if (Caret.Column > 0)
        {
            Move(new TextPosition(Caret.Line, TextMeasure.PreviousIndex(Buffer.GetLine(Caret.Line), Caret.Column)), extend);
        }
        else if (Caret.Line > 0)
        {
            Move(new TextPosition(Caret.Line - 1, Buffer.GetLine(Caret.Line - 1).Length), extend);
        }
    }

    /// <summary>Moves one character right; with a selection and no <paramref name="extend"/>, to its end.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveRight(bool extend)
    {
        var line = Buffer.GetLine(Caret.Line);
        if (HasSelection && !extend)
        {
            Move(SelectionEnd, extend: false);
        }
        else if (Caret.Column < line.Length)
        {
            Move(new TextPosition(Caret.Line, TextMeasure.NextIndex(line, Caret.Column)), extend);
        }
        else if (Caret.Line < Buffer.LineCount - 1)
        {
            Move(new TextPosition(Caret.Line + 1, 0), extend);
        }
    }

    /// <summary>Moves one line up, keeping the grid column. On the first line, moves to its start.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveUp(bool extend) => MoveLines(-1, extend);

    /// <summary>Moves one line down, keeping the grid column. On the last line, moves to its end.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveDown(bool extend) => MoveLines(1, extend);

    /// <summary>Moves a number of lines up or down, keeping the grid column.</summary>
    /// <param name="lines">Lines to move; negative is up.</param>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveLines(int lines, bool extend)
    {
        var target = Caret.Line + lines;
        if (target < 0)
        {
            Move(new TextPosition(0, 0), extend);
        }
        else if (target >= Buffer.LineCount)
        {
            Move(Buffer.End, extend);
        }
        else
        {
            var index = TextMeasure.IndexAtDisplayColumn(Buffer.GetLine(target), PreferredColumn, TabSize);
            Move(new TextPosition(target, index), extend, keepPreferredColumn: true);
        }
    }

    /// <summary>Moves to the first non-blank character of the line; if already there, to the start of the line.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveHome(bool extend)
    {
        var indent = LeadingWhitespace(Buffer.GetLine(Caret.Line));
        Move(new TextPosition(Caret.Line, Caret.Column == indent ? 0 : indent), extend);
    }

    /// <summary>Moves to the end of the line.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveEnd(bool extend) => Move(new TextPosition(Caret.Line, Buffer.GetLine(Caret.Line).Length), extend);

    /// <summary>Moves to the start of the previous word.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveWordLeft(bool extend)
    {
        var line = Buffer.GetLine(Caret.Line);
        if (Caret.Column == 0)
        {
            MoveLeft(extend);
            return;
        }

        var i = Caret.Column;
        while (i > 0 && Classify(line[i - 1]) == CharClass.Space)
        {
            i--;
        }

        if (i > 0)
        {
            var kind = Classify(line[i - 1]);
            while (i > 0 && Classify(line[i - 1]) == kind)
            {
                i--;
            }
        }

        Move(new TextPosition(Caret.Line, i), extend);
    }

    /// <summary>Moves to the start of the next word.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveWordRight(bool extend)
    {
        var line = Buffer.GetLine(Caret.Line);
        if (Caret.Column >= line.Length)
        {
            MoveRight(extend);
            return;
        }

        var i = Caret.Column;
        var kind = Classify(line[i]);
        while (i < line.Length && Classify(line[i]) == kind)
        {
            i++;
        }

        while (i < line.Length && Classify(line[i]) == CharClass.Space)
        {
            i++;
        }

        Move(new TextPosition(Caret.Line, i), extend);
    }

    /// <summary>Moves to the very start of the text.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveToStart(bool extend) => Move(new TextPosition(0, 0), extend);

    /// <summary>Moves to the very end of the text.</summary>
    /// <param name="extend">True to extend the selection.</param>
    public void MoveToEnd(bool extend) => Move(Buffer.End, extend);

    /// <summary>Selects all the text.</summary>
    public void SelectAll()
    {
        Anchor = new TextPosition(0, 0);
        Caret = Buffer.End;
        PreferredColumn = DisplayColumn(Caret);
    }

    /// <summary>Drops the selection, leaving the caret where it is.</summary>
    public void ClearSelection() => Anchor = Caret;

    private static int LeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] is ' ' or '\t')
        {
            i++;
        }

        return i;
    }

    private static CharClass Classify(char c) =>
        char.IsWhiteSpace(c) ? CharClass.Space : char.IsLetterOrDigit(c) || c == '_' ? CharClass.Word : CharClass.Punctuation;

    private void Replace(TextPosition from, TextPosition to, string insert)
    {
        var removed = Buffer.GetText(from, to);
        var caretBefore = Caret;
        var anchorBefore = Anchor;

        Buffer.Delete(from, to);
        var end = Buffer.Insert(from, insert);

        _history.Record(new Edit(from, removed, insert, caretBefore, anchorBefore, end));
        _version++;
        Move(end, extend: false);
    }

    private void Move(TextPosition position, bool extend, bool keepPreferredColumn = false)
    {
        Caret = Buffer.Clamp(position);
        if (!extend)
        {
            Anchor = Caret;
        }

        if (!keepPreferredColumn)
        {
            PreferredColumn = DisplayColumn(Caret);
        }
    }

    private enum CharClass
    {
        Space,
        Word,
        Punctuation,
    }
}
