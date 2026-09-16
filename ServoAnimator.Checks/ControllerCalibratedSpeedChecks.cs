using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using ServoAnimator;

internal static partial class Program
{
    private static void ControllerCalibratedSpeedChecks()
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var head = (RobotHeadView)window.FindName("EmbeddedHeadView");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags).SetValue(window, value);
        object Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags).Invoke(window, values);
        void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-7, $"{message}: {actual} vs {expected}");
        var config = ServoConfiguration.CreateDefault();
        var entry = config.Get(RobotControls.NeckTurn);
        entry.MinPwm = 1000; entry.DefaultPwm = 1500; entry.MaxPwm = 2000; entry.Reversed = false;
        entry.Speeds = new[] { 4, 1, 0, 2 }; entry.Accels = new[] { 0, 0, 0, 4 };
        var data = new SpeedCalibrationData();
        Set("_servoConfig", config); Set("_speedCalibration", data);
        Set("_speedCalibrationRoot", typeof(MainWindow).GetProperty("ConfigRoot", flags).GetValue(window));
        head.SetServoConfiguration(config); head.SetSpeedCalibration(data); head.SetCollisionWarningsEnabled(false);
        double Move(string target, double before = 0, double input = 1, double seconds = .025) =>
            (double)Call("AdvanceControllerRelative", target, before, input, seconds);
        const string neck = "Servo:NeckTurn";
        Near(Move(neck), .5, "Controller target uses the configured Default calibration");
        Near(Move(neck, input: .5), .25, "Partial deflection scales the shared calibrated velocity");
        Near(Move(neck, input: -1), -.5, "Negative input uses the calibrated reverse direction");
        Call("ControllerAction", "Speed Slow"); Near(Move(neck), .125, "Mapped Slow action selects the shared Slow profile");
        Call("ControllerAction", "Speed Crawl"); Near(Move(neck), .25, "Mapped Crawl action selects the shared Crawl profile");
        Call("ControllerAction", "Speed Fast"); Near(Move(neck), 100 * .025 / .225, "0/0 uses the same estimated shaft speed as the URDF");
        entry.Accels[2] = 1; head.SetServoConfiguration(config);
        double peak = Math.Sqrt(data.Limits(entry, ServoSpeed.Fast, true).Acceleration * 1000);
        Near(Move(neck), peak * .025 / 5, "Acceleration-only profile uses its calibrated full-stroke peak for jogging");
        Near(Move(neck, input: .5), peak * .025 / 10, "Unlimited Maestro speed retains proportional stick control with finite acceleration");
        entry.Accels[2] = 0; head.SetServoConfiguration(config);
        Set("_controllerConfigOpen", true);
        Call("PreviewControllerConfiguration", (object)new[] { new ControllerIntent("Action:Speed Slow", 0) }, true);
        Near(Move(neck), .125, "Mapping test mode accepts mapped shared speed selection buttons");
        Set("_controllerConfigOpen", false);
        Set("_controllerSpeed", ServoSpeed.NoChange);
        head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.Slow);
        Near(Move(neck), .125, "N/C retains the servo's existing calibrated speed selection");
        head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.Default);
        Near(Move(neck, 99.9), 100, "Calibrated target cannot overrun the native endpoint");

        entry.DefaultPwm = 1200; head.SetServoConfiguration(config);
        Near(Move(neck), .3125, "Positive travel honors an asymmetric PWM span");
        Near(Move(neck, input: -1), -1.25, "Negative travel honors its own PWM span");
        Near(Move(neck, -1), .0625, "Travel across neutral is converted in PWM space");
        entry.Reversed = true; head.SetServoConfiguration(config);
        Near(Move(neck), 1.25, "Reversal selects the correct PWM side without reversing the requested native direction");
        entry.Reversed = false; entry.DefaultPwm = 1500; head.SetServoConfiguration(config);
        data.Results.Add(new() { Control = entry.Control, Setting = ServoSpeed.Default, Fingerprint = data.Fingerprint(entry), FromPwm = 1000, ToPwm = 2000, Seconds = 20 });
        head.SetServoConfiguration(config);
        Near(Move(neck), .25, "Controller honors a matching measured increasing calibration");
        Near(Move(neck, input: -1), -.5, "Increasing evidence is not reused for the other direction");
        data.Results.Clear(); entry.Speeds[0] = 8; head.SetServoConfiguration(config);
        Near(Move(neck), 1, "Saving changed servo configuration invalidates shared cached limits");

        foreach (var control in ServoConfiguration.ControlsFor(ServoNames.EyesHorizontalRight))
        {
            var member = config.Get(control); member.MinPwm = 1000; member.DefaultPwm = 1500; member.MaxPwm = 2000;
            member.Speeds = new[] { control == RobotControls.LeftLensHorizontal ? 4 : 8, 1, 0, 2 }; member.Accels = new int[4];
        }
        head.SetServoConfiguration(config);
        Near(Move("Servo:EyesHorizontalRight"), .5, "A gang uses the slowest calibrated member");
        Near(Move("Child:EyesHorizontalRight:RightLensHorizontal"), 1, "An individual child uses its own calibrated rate");
        head.ConfigureCalibratedSpeed(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal, ServoSpeed.Slow);
        Near(Move("Servo:EyesHorizontalRight"), .125, "N/C resolves each gang member's active profile independently");

        data.Steppers.Single(s => s.Control == RobotControls.RightEyePop).ExtendSeconds = 2.2;
        data.Steppers.Single(s => s.Control == RobotControls.RightEyePop).RetractSeconds = 3.3;
        Near(Move("Servo:LeftEyePop"), 2000 * .025 / 1.1, "Individual Eye Pop uses its calibrated extension time");
        Near(Move("Servo:BothEyePop"), 2000 * .025 / 2.2, "Both Eye Pop channels share the slower calibrated extension rate");
        Near(Move("Servo:BothEyePop", 1000, -1), 1000 - 2000 * .025 / 3.3, "Retraction uses independent calibrated times");

        var engines = (Dictionary<ControllerKind, ControllerInputEngine>)typeof(MainWindow).GetField("_controllerEngines", flags).GetValue(window);
        foreach (var kind in Enum.GetValues<ControllerKind>())
        {
            var profile = ControllerProfile.Empty(kind);
            profile.Layers[0]["LeftX"] = new() { Target = neck, DeadZone = 0 };
            ControllerSample Sample(double value) => new() { Connected = true, DeviceId = 44, Values = new() { ["LeftX"] = value } };
            engines[kind].Reset(); engines[kind].Evaluate(profile, Sample(0), .025, _ => 0);
            Near(engines[kind].Evaluate(profile, Sample(1), .025, _ => 0).Single().Value, 1, kind + " production input uses the shared calibration resolver");
            var mapping = new ControllerMappingWindow(profile, () => Sample(0), _ => { }, currentValue: _ => 0,
                relativeMotion: (target, before, input, seconds) => Move(target, before, input, seconds));
            var live = (ControllerInputEngine)typeof(ControllerMappingWindow).GetField("_liveEngine", flags).GetValue(mapping);
            live.Evaluate(profile, Sample(0), .025, _ => 0);
            Near(live.Evaluate(profile, Sample(1), .025, _ => 0).Single().Value, 1, kind + " mapping test uses identical calibration");
            Check(typeof(ControllerMappingWindow).GetField("_rate", flags) == null, "Mapping inspector has no independent rate editor");
            var options = new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
            var legacy = JsonNode.Parse(JsonSerializer.Serialize(profile, options));
            legacy["Layers"][0]["LeftX"]["Rate"] = 9999;
            var migrated = legacy.Deserialize<ControllerProfile>(options); migrated.Validate();
            Check(!JsonSerializer.Serialize(migrated, options).Contains("\"Rate\""), "Legacy custom rates are ignored and omitted on the next mapping save");
            engines[kind].Reset(); engines[kind].Evaluate(migrated, Sample(0), .025, _ => 0);
            Near(engines[kind].Evaluate(migrated, Sample(1), .025, _ => 0).Single().Value, 1, "An old custom rate cannot override shared calibration");
        }
        var bare = new ControllerInputEngine(); var bareProfile = ControllerProfile.Defaults(ControllerKind.Xbox);
        bare.Evaluate(bareProfile, new() { Connected = true, Values = new() { ["LeftX"] = 0 } }, .025, _ => 0);
        Check(bare.Evaluate(bareProfile, new() { Connected = true, Values = new() { ["LeftX"] = 1 } }, .025, _ => 0).Count == 0,
            "Missing calibration source holds relative motion instead of inventing a rate");

        // New targets still enter the same calibrated acceleration pipeline.
        head.SnapCalibratedMotion = true; head.SetServo(ServoNames.NeckTurn, 0); head.SnapCalibratedMotion = false;
        head.ConfigureCalibratedSpeed(ServoNames.NeckTurn, null, ServoSpeed.Crawl);
        double target = Move(neck); head.SetServo(ServoNames.NeckTurn, target);
        Near(head.CapturePose().NeckTurn, 0, "Controller target does not bypass calibrated motion");
        head.AdvanceCalibratedMotion(.001);
        Check(head.CapturePose().NeckTurn > 0 && head.CapturePose().NeckTurn < target, "Acceleration still limits the first rendered step");
    }
}
