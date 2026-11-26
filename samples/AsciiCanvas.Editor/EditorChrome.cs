namespace AsciiCanvas.Editor;

/// <summary>Text shown around the document: the title bar, a message and the line ending.</summary>
/// <param name="Title">The file name, with a marker when there are unsaved changes.</param>
/// <param name="Message">A message to show instead of the caret position, or <see langword="null"/>.</param>
/// <param name="LineEnding">A short name of the line ending used when saving, such as <c>LF</c>.</param>
/// <param name="Prompt">A question to ask on the status line, or <see langword="null"/>.</param>
/// <param name="PromptInput">What the user has typed in answer to <paramref name="Prompt"/>.</param>
internal readonly record struct EditorChrome(string Title, string? Message, string LineEnding, string? Prompt = null, string PromptInput = "");
