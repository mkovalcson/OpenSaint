namespace ServoAnimator;

internal sealed class ControlConnectionState
{
    public bool Connected { get; set; }
    public bool ManuallyDisabled { get; private set; }
    public bool Enabled => Connected && !ManuallyDisabled;
    public void SetEnabled(bool enabled) => ManuallyDisabled = !enabled;
}
