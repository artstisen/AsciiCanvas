namespace AsciiCanvas.Editor;

/// <summary>Undo and redo stacks. Typing one character after another is merged into a single undo step.</summary>
internal sealed class EditHistory
{
    private readonly Stack<Edit> _undo = new();
    private readonly Stack<Edit> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Record(Edit edit)
    {
        _redo.Clear();

        if (_undo.TryPeek(out var last) && CanMerge(last, edit))
        {
            _undo.Pop();
            edit = last with { Inserted = last.Inserted + edit.Inserted, CaretAfter = edit.CaretAfter };
        }

        _undo.Push(edit);
    }

    public bool TryUndo(out Edit edit)
    {
        if (_undo.TryPop(out edit!))
        {
            _redo.Push(edit);
            return true;
        }

        return false;
    }

    public bool TryRedo(out Edit edit)
    {
        if (_redo.TryPop(out edit!))
        {
            _undo.Push(edit);
            return true;
        }

        return false;
    }

    // Only plain typing merges: one character, directly after the previous insertion, on the same line.
    private static bool CanMerge(Edit last, Edit next) =>
        last.Removed.Length == 0
        && next.Removed.Length == 0
        && next.Inserted.Length == 1
        && next.Inserted[0] != '\n'
        && !last.Inserted.Contains('\n')
        && next.Start == last.CaretAfter;
}
