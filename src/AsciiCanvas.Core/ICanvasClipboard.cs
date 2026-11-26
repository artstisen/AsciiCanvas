namespace AsciiCanvas;

/// <summary>
/// Access to the system clipboard for a program that runs on a canvas. The host provides it through
/// <see cref="CellCanvas.Clipboard"/>, so the program does not depend on a UI framework. Only text is supported.
/// Both methods can throw <see cref="System.Runtime.InteropServices.ExternalException"/> when the system clipboard is busy
/// because another program holds it; a program should treat that as a failed copy or paste, not as a fatal error.
/// </summary>
public interface ICanvasClipboard
{
    /// <summary>Gets the text on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text, or <see langword="null"/> if the clipboard holds no text.</returns>
    ValueTask<string?> GetTextAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the clipboard content with text.</summary>
    /// <param name="text">The text to put on the clipboard.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask SetTextAsync(string text, CancellationToken cancellationToken = default);
}
