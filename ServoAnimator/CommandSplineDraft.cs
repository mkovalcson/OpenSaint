namespace ServoAnimator;

/// <summary>Sequence-level spline edits staged alongside command drafts.</summary>
public sealed class CommandSplineDraft
{
    private readonly HashSet<ServoNames> _initial;
    private readonly HashSet<ServoNames> _enabled;
    public event Action Changed;

    public CommandSplineDraft(IEnumerable<ServoNames> enabled)
    {
        _enabled = enabled.ToHashSet();
        if (_enabled.Contains(ServoNames.NeckNodUp) || _enabled.Contains(ServoNames.NeckTiltRight))
        {
            _enabled.Add(ServoNames.NeckNodUp);
            _enabled.Add(ServoNames.NeckTiltRight);
        }
        _initial = new(_enabled);
    }

    public bool IsEnabled(ServoNames servo) => _enabled.Contains(servo);

    public void SetEnabled(ServoNames servo, bool enabled)
    {
        if (servo is ServoNames.Play or ServoNames.RGBCommand) return;
        void Set(ServoNames target) { if (enabled) _enabled.Add(target); else _enabled.Remove(target); }
        Set(servo);
        if (servo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
        {
            Set(ServoNames.NeckNodUp);
            Set(ServoNames.NeckTiltRight);
        }
        Changed?.Invoke();
    }

    // Only changed settings are merged, preserving unrelated modeless-editor changes.
    public IReadOnlyDictionary<ServoNames, bool> Changes => _initial.Union(_enabled)
        .Where(s => _initial.Contains(s) != _enabled.Contains(s))
        .ToDictionary(s => s, s => _enabled.Contains(s));
}
