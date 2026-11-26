namespace AsciiCanvas.Editor;

/// <summary>Things the user can ask the editor to do from the menu or with a shortcut.</summary>
public enum EditorCommand
{
    /// <summary>Start an empty document.</summary>
    New,

    /// <summary>Open a text file chosen in the file browser.</summary>
    Open,

    /// <summary>Save to the current file, or ask for a name if there is none.</summary>
    Save,

    /// <summary>Save under a name chosen in the file browser.</summary>
    SaveAs,

    /// <summary>Leave the editor.</summary>
    Exit,

    /// <summary>Undo the last change.</summary>
    Undo,

    /// <summary>Redo the last undone change.</summary>
    Redo,

    /// <summary>Move the selection to the clipboard.</summary>
    Cut,

    /// <summary>Copy the selection to the clipboard.</summary>
    Copy,

    /// <summary>Insert the clipboard text.</summary>
    Paste,

    /// <summary>Select all the text.</summary>
    SelectAll,
}
