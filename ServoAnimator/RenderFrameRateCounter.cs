namespace ServoAnimator;

/// <summary>
/// Rolling rate of unique WPF render callbacks, measured against wall time.
/// This observes rendering opportunities; it is not a GPU presentation fence.
/// Recording uses fixed storage and does not allocate or request rendering.
/// </summary>
internal sealed class RenderFrameRateCounter
{
    // More than a second of callbacks even on a 1000 Hz display.
    private readonly double[] _frames = new double[2048];
    private int _first, _count;
    private TimeSpan _lastRenderingTime = TimeSpan.MinValue;
    private double _firstFrameTime = double.NaN;

    internal void Reset()
    {
        _first = _count = 0;
        _lastRenderingTime = TimeSpan.MinValue;
        _firstFrameTime = double.NaN;
    }

    internal void Record(TimeSpan renderingTime, double elapsedSeconds)
    {
        if (renderingTime == _lastRenderingTime) return;
        if (renderingTime < _lastRenderingTime) Reset();
        _lastRenderingTime = renderingTime;
        if (double.IsNaN(_firstFrameTime)) _firstFrameTime = elapsedSeconds;
        Expire(elapsedSeconds);
        if (_count == _frames.Length) { _first = (_first + 1) % _frames.Length; _count--; }
        _frames[(_first + _count) % _frames.Length] = elapsedSeconds;
        _count++;
    }

    internal int Read(double elapsedSeconds)
    {
        Expire(elapsedSeconds);
        if (_count == 0) return 0;
        double duration = elapsedSeconds - _firstFrameTime;
        // Before a full second is available, measure intervals between frames
        // instead of treating the initial callback as an elapsed frame.
        double rate = duration >= 1 ? _count : duration > 0 ? (_count - 1) / duration : 0;
        return (int)Math.Round(rate, MidpointRounding.AwayFromZero);
    }

    private void Expire(double elapsedSeconds)
    {
        double cutoff = elapsedSeconds - 1;
        while (_count > 0 && _frames[_first] <= cutoff)
        {
            _first = (_first + 1) % _frames.Length;
            _count--;
        }
    }
}
