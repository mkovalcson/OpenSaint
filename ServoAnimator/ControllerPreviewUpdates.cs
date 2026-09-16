namespace ServoAnimator;

/// <summary>Coalesce only visual work. Input, audio and hardware dispatch never enter this queue.</summary>
internal sealed class ControllerPreviewUpdates
{
    private readonly Dictionary<string, (long Order, Action Apply)> _pending = new();
    private long _order;
    public int Count => _pending.Count;

    public void Enqueue(string target, Action apply) => _pending[target] = (++_order, apply);
    public void Clear() => _pending.Clear();

    // The render loop supplies the cadence. Explicit snapshots may also
    // synchronously capture the newest controller pose.
    public void Flush()
    {
        if (_pending.Count == 0) return;
        var updates = _pending.Values.OrderBy(p => p.Order).ToArray();
        _pending.Clear();
        // Retain the order of the last write to each target so a later group
        // command still overrides earlier child commands, and vice versa.
        foreach (var update in updates) update.Apply();
    }
}
