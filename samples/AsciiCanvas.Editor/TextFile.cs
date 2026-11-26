namespace AsciiCanvas.Editor;

/// <summary>The result of reading a text file.</summary>
/// <param name="Text">The text, or <see langword="null"/> if the file could not be used.</param>
/// <param name="LineEnding">The line ending the file uses, such as <c>\r\n</c>; kept so saving does not change it.</param>
/// <param name="Error">Why the file could not be used, or <see langword="null"/>.</param>
public readonly record struct TextFileResult(string? Text, string LineEnding, string? Error)
{
    /// <summary>True if <see cref="Text"/> is available.</summary>
    public bool Succeeded => Text is not null;
}

/// <summary>Reads files that hold text, and refuses files that do not.</summary>
public static class TextFile
{
    /// <summary>The largest file the editor opens.</summary>
    public const long MaxBytes = 32L * 1024 * 1024;

    private const int SniffBytes = 8192;

    /// <summary>Reads a text file. A byte-order mark selects UTF-8 or UTF-16; otherwise the file is read as UTF-8.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text and its line ending, or an explanation of why the file was refused.</returns>
    public static async Task<TextFileResult> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            var size = new FileInfo(path).Length;
            if (size > MaxBytes)
            {
                return Refuse($"The file is too large ({size / (1024 * 1024)} MB; the limit is {MaxBytes / (1024 * 1024)} MB).");
            }

            if (await LooksBinaryAsync(path, cancellationToken).ConfigureAwait(false))
            {
                return Refuse("This does not look like a text file: it contains binary data.");
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return new TextFileResult(text, DetectLineEnding(text), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Refuse(ex.Message);
        }
    }

    /// <summary>Gets the line ending a text mostly uses: CRLF if it contains one, otherwise LF, otherwise the platform's.</summary>
    /// <param name="text">The text.</param>
    public static string DetectLineEnding(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        return text.Contains('\n') ? "\n" : Environment.NewLine;
    }

    private static TextFileResult Refuse(string error) => new(null, Environment.NewLine, error);

    // A zero byte does not occur in text, but is common in images, archives and programs.
    private static async Task<bool> LooksBinaryAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[SniffBytes];
        var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        // UTF-16 text is full of zero bytes, but starts with a byte-order mark.
        var utf16 = read >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF));
        return !utf16 && buffer.AsSpan(0, read).Contains((byte)0);
    }
}
