namespace AsciiCanvas;

/// <summary>Text attributes of a cell. Combine with <c>|</c>.</summary>
[Flags]
public enum CellAttributes : byte
{
    /// <summary>No attributes.</summary>
    None = 0,

    /// <summary>Bold text.</summary>
    Bold = 1,

    /// <summary>Dimmed text (the foreground is mixed towards the background).</summary>
    Dim = 2,

    /// <summary>Italic text.</summary>
    Italic = 4,

    /// <summary>Underlined text.</summary>
    Underline = 8,

    /// <summary>Struck-through text.</summary>
    Strikethrough = 16,

    /// <summary>Foreground and background swapped.</summary>
    Reverse = 32,
}
