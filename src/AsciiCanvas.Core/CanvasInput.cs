using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace AsciiCanvas;

/// <summary>
/// Thread-safe stream of input events from the host to the program. The host posts events;
/// the program reads them (one reader at a time).
/// </summary>
public sealed class CanvasInput
{
    private readonly Channel<InputEvent> _channel =
        Channel.CreateUnbounded<InputEvent>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>True once <see cref="Complete"/> was called and everything was read.</summary>
    public bool IsCompleted => _channel.Reader.Completion.IsCompleted;

    /// <summary>Adds an event for the program to read. Ignored after <see cref="Complete"/>.</summary>
    /// <param name="input">The event to add.</param>
    public void Post(InputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _channel.Writer.TryWrite(input);
    }

    /// <summary>Signals that no more input will arrive (for example, the window was closed).</summary>
    public void Complete() => _channel.Writer.TryComplete();

    /// <summary>Reads an event if one is waiting.</summary>
    /// <param name="input">The event, if one was waiting.</param>
    /// <returns>True if an event was read.</returns>
    public bool TryRead([NotNullWhen(true)] out InputEvent? input) => _channel.Reader.TryRead(out input);

    /// <summary>Waits for the next event.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The next event.</returns>
    /// <exception cref="OperationCanceledException">The wait was canceled, or input was completed.</exception>
    public async ValueTask<InputEvent> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new OperationCanceledException("The canvas input was closed.");
        }
    }

    /// <summary>Enumerates events until input completes.</summary>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    public IAsyncEnumerable<InputEvent> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Discards everything currently waiting.</summary>
    public void Clear()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }
}
