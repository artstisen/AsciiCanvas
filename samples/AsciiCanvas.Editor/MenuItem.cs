namespace AsciiCanvas.Editor;

/// <summary>An entry in a drop-down menu.</summary>
/// <param name="Label">The text of the entry.</param>
/// <param name="Shortcut">The shortcut shown at the right, such as <c>Ctrl+S</c>; empty if none.</param>
/// <param name="Command">What the entry does, or <see langword="null"/> for a separator line.</param>
public sealed record MenuItem(string Label, string Shortcut, EditorCommand? Command)
{
    /// <summary>A separator line between groups of entries.</summary>
    public static MenuItem Separator { get; } = new("", "", null);

    /// <summary>True for a separator line.</summary>
    public bool IsSeparator => Command is null;
}
