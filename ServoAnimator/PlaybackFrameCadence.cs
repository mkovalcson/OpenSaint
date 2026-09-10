namespace ServoAnimator
{
    /// <summary>Limit heavy preview work without discarding any cursor frames.</summary>
    internal sealed class PlaybackFrameCadence
    {
        private double _next = double.NaN;
        public void Reset() => _next = double.NaN;
        public bool IsDue(TimeSpan frameTime)
        {
            const double interval = 1.0 / 60;
            double now = frameTime.TotalSeconds;
            if (double.IsNaN(_next)) { _next = now + interval; return true; }
            if (now + 1e-7 < _next) return false;
            _next += (Math.Floor(Math.Max(0, now - _next) / interval) + 1) * interval;
            return true;
        }
    }
}
