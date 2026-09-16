using System.Windows.Media;

namespace ServoAnimator;

/// <summary>One visual heartbeat per UI thread. Apply fresh controller targets
/// before advancing any calibrated model, at most once per display frame and
/// near 60 Hz. A late frame never queues a burst of obsolete updates.</summary>
internal sealed class UrdfRenderLoop
{
    [ThreadStatic] private static UrdfRenderLoop _current;
    internal static UrdfRenderLoop Current => _current ??= new();
    private readonly PlaybackFrameCadence _cadence = new(60);
    private readonly bool _attachToRendering;
    private Action<TimeSpan> _targets, _motion;
    private TimeSpan _lastFrame = TimeSpan.MinValue;
    private bool _subscribed;

    internal UrdfRenderLoop(bool attachToRendering = true) => _attachToRendering = attachToRendering;
    internal int ParticipantCount => (_targets?.GetInvocationList().Length ?? 0) + (_motion?.GetInvocationList().Length ?? 0);

    internal void AddTargets(Action<TimeSpan> callback) { _targets -= callback; _targets += callback; UpdateSubscription(); }
    internal void RemoveTargets(Action<TimeSpan> callback) { _targets -= callback; UpdateSubscription(); }
    internal void AddMotion(Action<TimeSpan> callback) { _motion -= callback; _motion += callback; UpdateSubscription(); }
    internal void RemoveMotion(Action<TimeSpan> callback) { _motion -= callback; UpdateSubscription(); }

    private void UpdateSubscription()
    {
        bool needed = _targets != null || _motion != null;
        if (_attachToRendering && needed != _subscribed)
        {
            if (needed) CompositionTarget.Rendering += Rendering;
            else CompositionTarget.Rendering -= Rendering;
            _subscribed = needed;
        }
        if (!needed) { _cadence.Reset(); _lastFrame = TimeSpan.MinValue; }
    }

    private void Rendering(object sender, EventArgs e)
    {
        if (e is RenderingEventArgs frame) Advance(frame.RenderingTime);
    }

    internal bool Advance(TimeSpan frameTime)
    {
        if (_lastFrame == frameTime) return false;
        if (frameTime < _lastFrame) _cadence.Reset();
        _lastFrame = frameTime;
        if (!_cadence.IsDue(frameTime)) return false;
        _targets?.Invoke(frameTime);
        _motion?.Invoke(frameTime);
        return true;
    }
}
