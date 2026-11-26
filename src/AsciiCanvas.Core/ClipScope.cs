namespace AsciiCanvas;

/// <summary>Disposing restores the clip that was active before <see cref="CellCanvas.PushClip"/>.</summary>
public readonly struct ClipScope : IDisposable
{
    private readonly CellCanvas? _canvas;
    private readonly int _depth;

    internal ClipScope(CellCanvas canvas, int depth)
    {
        _canvas = canvas;
        _depth = depth;
    }

    /// <summary>Restores the previous clip.</summary>
    public void Dispose() => _canvas?.PopClip(_depth);
}
