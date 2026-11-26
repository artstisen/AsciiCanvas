namespace AsciiCanvas.Editor;

/// <summary>A menu in the menu bar, with its drop-down entries.</summary>
/// <param name="Title">The text in the menu bar.</param>
/// <param name="Items">The entries, top to bottom.</param>
public sealed record Menu(string Title, IReadOnlyList<MenuItem> Items);
