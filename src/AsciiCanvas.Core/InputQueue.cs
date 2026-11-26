using System.Threading.Channels;

namespace AsciiCanvas;

/// <summary>
/// Thread-safe stream of input events from the host to the program. The host posts events;
/// the program reads them (one reader at a time).
/// </summary>
public sealed class InputQueue
{
    private readonly Channel<InputEvent> _channel =
        Channel.CreateUnbounded<InputEvent>(new UnboundedChannelOptions { SingleReader = true });

    public void Post(InputEvent input) => _channel.Writer.TryWrite(input);

    /// <summary>Signals that no more input will arrive (for example, the window was closed).</summary>
    public void Complete() => _channel.Writer.TryComplete();

    public bool IsCompleted => _channel.Reader.Completion.IsCompleted;

    public bool TryRead(out InputEvent? input) => _channel.Reader.TryRead(out input);

    /// <summary>Waits for the next event. Throws <see cref="OperationCanceledException"/> once the queue is completed.</summary>
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

    /// <summary>Enumerates events until the queue completes.</summary>
    public IAsyncEnumerable<InputEvent> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Discards everything currently queued.</summary>
    public void Clear()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }
}
