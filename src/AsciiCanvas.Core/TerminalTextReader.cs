namespace AsciiCanvas;

/// <summary>Adapter that lets <see cref="TextTerminal"/> be used wherever a <see cref="TextReader"/> is expected, such as <c>Console.In</c>.</summary>
internal sealed class TerminalTextReader(TextTerminal terminal) : TextReader
{
    // The rest of a line that was started by Read or Peek, ending with the line break.
    private string _buffer = "";
    private int _index;

    public override string? ReadLine()
    {
        if (_index < _buffer.Length)
        {
            var rest = _buffer[_index..^1];
            _index = _buffer.Length;
            return rest;
        }

        return ReadNewLine();
    }

    public override int Read()
    {
        Fill();
        return _buffer[_index++];
    }

    public override int Peek()
    {
        Fill();
        return _buffer[_index];
    }

    private void Fill()
    {
        if (_index >= _buffer.Length)
        {
            _buffer = ReadNewLine() + "\n";
            _index = 0;
        }
    }

    private string ReadNewLine() =>
        terminal.ReadLineAsync(cancellationToken: terminal.Canvas.Closed).AsTask().GetAwaiter().GetResult();
}
