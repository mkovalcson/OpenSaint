namespace ServoAnimator;

public partial class MainWindow
{
    private void ApplyTimelineNeckChildren(RobotHeadView view)
    {
        var parents = new[] { ServoNames.NeckNodUp, ServoNames.NeckTiltRight };
        double lastGang = double.NegativeInfinity;
        foreach (var parent in parents)
        {
            _gangCommandIndex.TryGetValue(parent, out var commands);
            lastGang = Math.Max(lastGang, LastCommandAtOrBefore(commands, _cursorTime)?.OffsetSeconds ?? double.NegativeInfinity);
        }
        foreach (var control in new[] { RobotControls.NeckTiltLeft, RobotControls.NeckTiltRight })
        {
            ServoCommand latest = null;
            foreach (var parent in parents)
            {
                _childCommandIndex.TryGetValue((parent, control), out var commands);
                var candidate = LastCommandAtOrBefore(commands, _cursorTime);
                if (candidate != null && candidate.OffsetSeconds > lastGang && (latest == null || candidate.OffsetSeconds >= latest.OffsetSeconds)) latest = candidate;
            }
            if (latest == null || latest.Disable || RecordingOwns(latest.Servo, control)) continue;
            view.SetChildServo(latest.Servo, control, latest.NumericValue, preserveOtherNeck: true);
        }
    }
}
