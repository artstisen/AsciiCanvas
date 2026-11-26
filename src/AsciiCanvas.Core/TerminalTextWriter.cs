using System.Text;

namespace AsciiCanvas;

/// <summary>Adapter that lets <see cref="TextTerminal"/> be used wherever a <see cref="TextWriter"/> is expected.</summary>
internal sealed class TerminalTextWriter(TextTerminal terminal) : TextWriter
{
    private char _highSurrogate;

    public override Encoding Encoding => Encoding.Unicode;

    public override void Write(char value)
    {
        if (char.IsHighSurrogate(value))
        {
            _highSurrogate = value;
            return;
        }

        if (_highSurrogate != 0)
        {
            Span<char> pair = [_highSurrogate, value];
            _highSurrogate = '\0';
            terminal.Write(pair);
            return;
        }

        terminal.Write([value]);
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            terminal.Write(value);
        }
    }

    public override void Write(ReadOnlySpan<char> buffer) => terminal.Write(buffer);

    public override void Write(char[] buffer, int index, int count) => terminal.Write(buffer.AsSpan(index, count));

    public override void WriteLine(string? value) => terminal.WriteLine(value ?? "");

    public override void WriteLine() => terminal.WriteLine();

    public override void Flush() => terminal.Finish();
}
