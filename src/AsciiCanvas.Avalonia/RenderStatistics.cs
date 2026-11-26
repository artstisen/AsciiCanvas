namespace AsciiCanvas.Avalonia;

/// <summary>Collects how long frames take to draw and how often they are drawn, averaged over short windows.</summary>
internal sealed class RenderStatistics(TimeProvider clock)
{
    private const double WindowSeconds = 0.5;

    private long _windowStart = clock.GetTimestamp();
    private int _count;
    private double _sumMilliseconds;
    private double _maxMilliseconds;

    /// <summary>Average draw time of the last completed window.</summary>
    public double AverageMilliseconds { get; private set; }

    /// <summary>Slowest draw time of the last completed window.</summary>
    public double MaxMilliseconds { get; private set; }

    /// <summary>Frames drawn per second in the last completed window.</summary>
    public double RendersPerSecond { get; private set; }

    public void Record(TimeSpan drawTime)
    {
        var milliseconds = drawTime.TotalMilliseconds;
        _count++;
        _sumMilliseconds += milliseconds;
        _maxMilliseconds = Math.Max(_maxMilliseconds, milliseconds);

        var window = clock.GetElapsedTime(_windowStart).TotalSeconds;
        if (window < WindowSeconds)
        {
            return;
        }

        AverageMilliseconds = _sumMilliseconds / _count;
        MaxMilliseconds = _maxMilliseconds;
        RendersPerSecond = _count / window;

        _windowStart = clock.GetTimestamp();
        _count = 0;
        _sumMilliseconds = 0;
        _maxMilliseconds = 0;
    }
}
