namespace ServoAnimator
{
    /// <summary>Limit heavy preview work without discarding any cursor frames.</summary>
    internal sealed class PlaybackFrameCadence
    {
        private readonly double _interval;
        private double _next = double.NaN;
        public PlaybackFrameCadence(double updatesPerSecond = 30)
        {
            if (!double.IsFinite(updatesPerSecond) || updatesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(updatesPerSecond));
            _interval = 1.0 / updatesPerSecond;
        }
        public void Reset() => _next = double.NaN;
        public bool IsDue(TimeSpan frameTime)
        {
            double now = frameTime.TotalSeconds;
            if (double.IsNaN(_next)) { _next = now + _interval; return true; }
            if (now + 1e-7 < _next) return false;
            _next += (Math.Floor(Math.Max(0, now - _next) / _interval) + 1) * _interval;
            return true;
        }
    }
}
