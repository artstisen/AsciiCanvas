namespace AsciiCanvas.Editor;

/// <summary>One change to the text, with what is needed to undo and redo it.</summary>
/// <param name="Start">Where the change starts.</param>
/// <param name="Removed">The text that was removed at <paramref name="Start"/>.</param>
/// <param name="Inserted">The text that was inserted at <paramref name="Start"/>.</param>
/// <param name="CaretBefore">The caret before the change.</param>
/// <param name="AnchorBefore">The selection anchor before the change.</param>
/// <param name="CaretAfter">The caret after the change.</param>
internal sealed record Edit(
    TextPosition Start,
    string Removed,
    string Inserted,
    TextPosition CaretBefore,
    TextPosition AnchorBefore,
    TextPosition CaretAfter);
