namespace ServoAnimator
{
    internal sealed record ModifiedCommandControl(ServoNames Servo, RobotControls? Control)
    {
        public string NameText => ToString();
        public string ValueText => "—";
        public string SpeedText => "—";
        public string TimeText => "all";
        public System.Windows.Media.Brush AccentBrush => MainWindow.BrushFor(Servo);
        public string Details => $"{NameText}\nSelect to highlight every matching command triangle.";
        public override string ToString() => Control.HasValue ? $"{Servo} [{Control}]" : Servo.ToString();
        public bool Includes(ServoCommand command) => command.Servo == Servo &&
            (!Control.HasValue || !command.Control.HasValue || command.Control == Control);
    }

    internal static class ModifiedCommandControls
    {
        internal static ModifiedCommandControl[] From(IEnumerable<ServoCommand> commands) => commands
            .Select(c => new ModifiedCommandControl(c.Servo, c.Control)).Distinct()
            .OrderBy(c => c.Servo.ToString()).ThenBy(c => c.Control?.ToString() ?? "").ToArray();
    }
}
