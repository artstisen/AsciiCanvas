using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace AsciiCanvas.Avalonia;

/// <summary>The system clipboard as seen by a program running on a <see cref="CanvasView"/>. The program calls it from any thread; the clipboard is used on the UI thread.</summary>
internal sealed class AvaloniaCanvasClipboard(Control owner) : ICanvasClipboard
{
    public async ValueTask<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread
            .InvokeAsync(
                async () => TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null,
                DispatcherPriority.Normal)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await Dispatcher.UIThread
            .InvokeAsync(
                async () =>
                {
                    if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
                    {
                        await clipboard.SetTextAsync(text);
                    }
                },
                DispatcherPriority.Normal)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
