namespace AsciiCanvas.Editor;

/// <summary>Text shown around the document by <see cref="EditorView"/>.</summary>
/// <param name="Message">A message to show instead of the caret position, or <see langword="null"/>.</param>
/// <param name="LineEnding">A short name of the line ending used when saving, such as <c>LF</c>.</param>
internal readonly record struct EditorChrome(string? Message, string LineEnding);
