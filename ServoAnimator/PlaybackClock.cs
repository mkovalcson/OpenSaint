using System.Diagnostics;

namespace ServoAnimator
{
    /// <summary>A monotonic timeline with gentle correction from bytes actually
    /// played by the output device, never the decoder's read-ahead position.</summary>
    internal sealed class PlaybackClock
    {
        private readonly Stopwatch _watch = new();
        private double _time, _lastElapsed, _lastDevice = double.NaN;

        // Read-only interpolation between render ticks for input recording.
        // Reading it must not consume time or skip timeline command dispatch.
        internal double EstimatedTime => _time + Math.Max(0, _watch.Elapsed.TotalSeconds - _lastElapsed);

        public void Start(double time)
        {
            _time = time;
            _lastElapsed = 0;
            _lastDevice = double.NaN;
            _watch.Restart();
        }

        public double Advance(double? outputTime = null) =>
            AdvanceTo(_watch.Elapsed.TotalSeconds, outputTime);

        internal double AdvanceTo(double elapsed, double? outputTime)
        {
            double dt = Math.Max(0, elapsed - _lastElapsed);
            _lastElapsed = elapsed;
            double advance = dt;
            // Only correct on a fresh device observation. A repeated output
            // position is a coarse driver sample, not a reason to freeze.
            if (outputTime.HasValue && double.IsFinite(outputTime.Value) &&
                (double.IsNaN(_lastDevice) || outputTime.Value > _lastDevice))
            {
                _lastDevice = outputTime.Value;
                double error = outputTime.Value - (_time + dt);
                advance += Math.Clamp(error * 0.15, -dt * 0.25, dt * 0.25);
            }
            _time += Math.Max(0, advance);
            return _time;
        }
    }
}
