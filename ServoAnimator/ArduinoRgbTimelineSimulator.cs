using System.Globalization;
using System.Windows.Media;

namespace ServoAnimator
{
    /// <summary>
    /// Deterministic timeline-time emulator for ArduinoOpenSaintRGB.ino.
    /// It mirrors the sketch's four 16-pixel rings and its single active
    /// non-blocking animation slot. Evaluation is incremental during forward
    /// playback and resets/replays only when the playhead moves backward or
    /// the RGB command list changes.
    /// </summary>
    internal sealed class ArduinoRgbTimelineSimulator
    {
        private const int NumLeds = 16;

        private readonly RingState _leftEye = new();
        private readonly RingState _leftVent = new();
        private readonly RingState _rightEye = new();
        private readonly RingState _rightVent = new();

        private Globals _g = new();
        private readonly PulseState _pulse = new();
        private readonly FadeState _fade = new();
        private readonly WipeState _wipe = new();
        private readonly TheaterState _theater = new();
        private readonly CylonState _cylon = new();
        private readonly RainbowState _rainbow = new();
        private readonly RainbowState _rainbowCycle = new();
        private readonly RainbowChaseState _rainbowChase = new();

        private List<RgbEvent> _events = new();
        private int _eventIndex;
        private long _evaluatedMs = -1;
        private int _signature;
        private string _activeFunction = "";
        private long _triggerMs;
        private bool _triggerActive;

        public void Invalidate()
        {
            _signature = int.MinValue;
            _evaluatedMs = -1;
        }

        public RgbRingFrame Evaluate(IReadOnlyList<ServoCommand> commands, double timeSeconds)
        {
            long targetMs = Math.Max(0L, (long)Math.Floor(timeSeconds * 1000.0 + 1e-7));
            int sig = ComputeSignature(commands);

            if (_evaluatedMs < 0 || sig != _signature || targetMs < _evaluatedMs)
                Reset(commands, sig);

            while (_eventIndex < _events.Count && _events[_eventIndex].TimeMs <= targetMs)
            {
                var e = _events[_eventIndex++];
                // The Arduino loop reads/parses serial before RunExistingCommands.
                // Advance any already-due animation steps strictly before the
                // incoming command time, then parse the new line at that time.
                AdvanceActiveTo(e.TimeMs, includeLimit: false);
                ParseLine(e.Text, e.TimeMs);
            }

            AdvanceActiveTo(targetMs, includeLimit: true);
            _evaluatedMs = targetMs;
            return Snapshot();
        }

        /// <summary>Preview one RGB command as if it arrived at t=0. Useful for
        /// live command editing without changing the authored timeline.</summary>
        public RgbRingFrame PreviewCommand(string command)
        {
            Reset(Array.Empty<ServoCommand>(), 0);
            ParseLine(command ?? "", 0);
            return Snapshot();
        }

        private void Reset(IReadOnlyList<ServoCommand> commands, int signature)
        {
            _leftEye.Clear();
            _leftVent.Clear();
            _rightEye.Clear();
            _rightVent.Clear();
            _g = new Globals();

            _pulse.Reset();
            _fade.Reset();
            _wipe.Reset();
            _theater.Reset();
            _cylon.Reset();
            _rainbow.Reset();
            _rainbowCycle.Reset();
            _rainbowChase.Reset();

            _activeFunction = "";
            _triggerMs = 0;
            _triggerActive = false;
            _evaluatedMs = 0;
            _eventIndex = 0;
            _signature = signature;

            _events = commands
                .Select((c, i) => new { Command = c, Index = i })
                .Where(x => x.Command.Servo == ServoNames.RGBCommand && !x.Command.Disable)
                .OrderBy(x => x.Command.OffsetSeconds)
                .ThenBy(x => x.Index)
                .Select(x => new RgbEvent(
                    Math.Max(0L, (long)Math.Round(x.Command.OffsetSeconds * 1000.0)),
                    x.Command.TextValue ?? ""))
                .ToList();
        }

        private static int ComputeSignature(IReadOnlyList<ServoCommand> commands)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < commands.Count; i++)
                {
                    var c = commands[i];
                    if (c.Servo != ServoNames.RGBCommand) continue;
                    h = h * 31 + ServoCommand.TimeKey(c.OffsetSeconds).GetHashCode();
                    h = h * 31 + StringComparer.Ordinal.GetHashCode(c.TextValue ?? "");
                    h = h * 31 + c.Disable.GetHashCode();
                    h = h * 31 + i;
                }
                return h;
            }
        }

        private void AdvanceActiveTo(long limitMs, bool includeLimit)
        {
            int guard = 0;
            while (_triggerActive &&
                   (includeLimit ? _triggerMs <= limitMs : _triggerMs < limitMs))
            {
                // Bad/zero delays in an authored command must never hang the UI.
                if (++guard > 200000)
                {
                    _triggerActive = false;
                    break;
                }

                long now = _triggerMs;
                long next = StepFunction(_activeFunction, now);
                if (next <= 0)
                {
                    _triggerActive = false;
                    break;
                }
                if (next <= now)
                    next = now + 1;
                _triggerMs = next;
            }
        }

        private void ParseLine(string line, long nowMs)
        {
            _g.ResetDefaults();

            // Saved/editor commands use normal Red,Green,Blue argument order.
            // Feed the emulator the exact same Green,Red,Blue wire format sent
            // to the physical Arduino before reproducing the sketch parser.
            string wireLine = RgbCommandWireFormat.ToArduinoWireOrder(line);
            string normalized = (wireLine ?? "").ToUpperInvariant().Replace(" ", "");
            if (string.IsNullOrWhiteSpace(normalized)) return;

            string[] token = normalized.Split(',');
            string command = token[0];

            if (command == "CYLON")
            {
                _g.DelayMs = UInt(token, 1, 40);
                _g.Pulses = UInt(token, 2, 3);
                StartFunction("CylonNonBlocking", nowMs);
                return;
            }

            if (command == "CLEAR" && token.Length == 3)
            {
                ParseTargets(token[1], token[2]);
                SetColorAll(Rgb24.Black, 0, _g.Eyes, _g.Vents, _g.Left, _g.Right);
                return;
            }

            if (command == "CLEARALL")
            {
                // The source sketch intentionally does NOT cancel Commands[0].
                SetColorAll(Rgb24.Black, 0, true, true, true, true);
                return;
            }

            if (token.Length == 4 &&
                (command == "RAINBOW" || command == "RAINBOWCYCLE" || command == "RAINBOWCHASE"))
            {
                _g.Brightness = UInt(token, 1, 200);
                ParseSide(token[2]);
                _g.DelayMs = UInt(token, 3, 40);
                StartFunction(command switch
                {
                    "RAINBOW" => "RainbowNonBlocking",
                    "RAINBOWCYCLE" => "RainbowCycleNonBlocking",
                    _ => "RainbowTheaterChaseNonBlocking",
                }, nowMs);
                return;
            }

            if (token.Length < 6) return;

            // Exact Arduino parser behavior after Green,Red,Blue transport:
            // Color(token[2], token[1], token[3]) reconstructs Red,Green,Blue.
            _g.AllColor = new Rgb24(Byte(token, 2), Byte(token, 1), Byte(token, 3));
            _g.Brightness = UInt(token, 4, 200);
            string ring = token.Length > 5 ? token[5] : "";
            string side = token.Length > 6 ? token[6] : "";
            ParseTargets(ring, side);

            switch (command)
            {
                case "SETRGBCOLOR":
                    SetColorAll(_g.AllColor, _g.Brightness,
                        _g.Eyes, _g.Vents, _g.Left, _g.Right);
                    break;

                case "COLORWIPEEYES":
                    _g.DelayMs = UInt(token, 7, 20);
                    StartFunction("ColorWipeEyesNonBlocking", nowMs);
                    break;

                case "FADE":
                    _g.DelayMs = UInt(token, 7, 20);
                    _g.FadeIn = StringAt(token, 8) == "IN";
                    _g.Step = UInt(token, 9, 1);
                    _g.LowestBrightness = UInt(token, 10, 0);
                    StartFunction("FadeColorNonBlocking", nowMs);
                    break;

                case "PULSE":
                    _g.DelayMs = UInt(token, 7, 20);
                    _g.Pulses = UInt(token, 8, 3);
                    _g.Step = UInt(token, 9, 1);
                    StartFunction("PulseColorAllNonBlocking", nowMs);
                    break;

                case "THEATERCHASE":
                    _g.DelayMs = UInt(token, 7, 40);
                    _g.Pulses = UInt(token, 8, 10);
                    StartFunction("TheaterChaseNonBlocking", nowMs);
                    break;
            }
        }

        private void StartFunction(string name, long nowMs)
        {
            _activeFunction = name;
            long next = StepFunction(name, nowMs); // MainParsing calls once immediately.
            _triggerActive = next > 0;
            _triggerMs = Math.Max(0, next);
        }

        private long StepFunction(string name, long nowMs) => name switch
        {
            "PulseColorAllNonBlocking" => PulseStep(nowMs),
            "FadeColorNonBlocking" => FadeStep(nowMs),
            "ColorWipeEyesNonBlocking" => WipeStep(nowMs),
            "TheaterChaseNonBlocking" => TheaterStep(nowMs),
            "CylonNonBlocking" => CylonStep(nowMs),
            "RainbowNonBlocking" => RainbowStep(nowMs, cycle: false),
            "RainbowCycleNonBlocking" => RainbowStep(nowMs, cycle: true),
            "RainbowTheaterChaseNonBlocking" => RainbowChaseStep(nowMs),
            _ => 0,
        };

        private long PulseStep(long now)
        {
            var s = _pulse;
            if (s.NextRun == 0 && s.Pulse == 0 && s.Level == 0)
            {
                s.Direction = 1;
                s.NextRun = now;
            }
            if (now < s.NextRun) return s.NextRun;

            SetBrightness(_g.Brightness == 0 ? 0 : Math.Clamp(s.Level, 0, 255),
                _g.Eyes, _g.Vents, _g.Left, _g.Right);
            SetPixelsAll(_g.AllColor, _g.Eyes, _g.Vents, _g.Left, _g.Right);
            Show(_g.Eyes, _g.Vents, _g.Left, _g.Right);

            int step = Math.Max(1, _g.Step);
            s.Level += step * s.Direction;
            if (s.Level >= _g.Brightness)
            {
                s.Level = _g.Brightness;
                s.Direction = -1;
            }
            else if (s.Level <= 0)
            {
                s.Level = 0;
                s.Direction = 1;
                s.Pulse++;
                if (s.Pulse >= _g.Pulses)
                {
                    s.Reset();
                    SetColorAll(Rgb24.Black, 0, _g.Eyes, _g.Vents, _g.Left, _g.Right);
                    return 0;
                }
            }

            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long FadeStep(long now)
        {
            var s = _fade;
            int step = Math.Max(1, _g.Step);
            if (!s.Initialized)
            {
                s.Level = _g.FadeIn ? _g.LowestBrightness : _g.Brightness;
                s.Direction = _g.FadeIn ? step : -step;
                s.NextRun = now;
                s.Initialized = true;
            }
            if (now < s.NextRun) return s.NextRun;

            s.Level += s.Direction;
            if (!_g.FadeIn && s.Level <= _g.LowestBrightness)
            {
                s.Level = _g.LowestBrightness;
                s.Initialized = false;
            }
            if (_g.FadeIn && s.Level >= _g.Brightness)
            {
                s.Level = _g.Brightness;
                s.Initialized = false;
            }

            SetBrightness(Math.Clamp(s.Level, 0, 255), _g.Eyes, _g.Vents, _g.Left, _g.Right);
            SetPixelsAll(_g.AllColor, _g.Eyes, _g.Vents, _g.Left, _g.Right);
            Show(_g.Eyes, _g.Vents, _g.Left, _g.Right);

            if (!s.Initialized)
            {
                s.NextRun = 0;
                return 0;
            }
            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long WipeStep(long now)
        {
            var s = _wipe;
            if (!s.Initialized)
            {
                SetBrightness(_g.Brightness, true, false, _g.Left, _g.Right);
                s.I = 0;
                s.NextRun = now;
                s.Initialized = true;
            }
            if (now < s.NextRun) return s.NextRun;

            if (s.I >= 0 && s.I < NumLeds)
            {
                if (_g.Left) { _leftEye.SetPixel(s.I, _g.AllColor); _leftEye.Show(); }
                if (_g.Right) { _rightEye.SetPixel(s.I, _g.AllColor); _rightEye.Show(); }
            }
            s.I++;
            if (s.I >= NumLeds)
            {
                s.Reset();
                // Exact sketch: ColorWipeEyes completion clears all four rings.
                SetColorAll(Rgb24.Black, 0, true, true, true, true);
                return 0;
            }
            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long TheaterStep(long now)
        {
            var s = _theater;
            if (s.J == 0 && s.Q == 0 && s.NextRun == 0)
            {
                SetBrightness(_g.Brightness, true, false, _g.Left, _g.Right);
                s.NextRun = now;
            }
            if (now < s.NextRun) return s.NextRun;

            for (int i = 0; i < NumLeds; i += 3)
            {
                int n = i + s.Q;
                if (n >= NumLeds) continue;
                if (_g.Left) _leftEye.SetPixel(n, _g.AllColor);
                if (_g.Right) _rightEye.SetPixel(n, _g.AllColor);
            }
            if (_g.Left) _leftEye.Show();
            if (_g.Right) _rightEye.Show();

            // The sketch clears the NeoPixel RAM after show(), but deliberately
            // does not call show() again until the next phase.
            for (int i = 0; i < NumLeds; i += 3)
            {
                int n = i + s.Q;
                if (n >= NumLeds) continue;
                if (_g.Left) _leftEye.SetPixel(n, Rgb24.Black);
                if (_g.Right) _rightEye.SetPixel(n, Rgb24.Black);
            }

            s.Q++;
            if (s.Q >= 3)
            {
                s.Q = 0;
                s.J++;
                if (s.J >= _g.Pulses)
                {
                    s.Reset();
                    SetColorAll(Rgb24.Black, 0, true, true, true, true);
                    return 0;
                }
            }
            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long CylonStep(long now)
        {
            var s = _cylon;
            const int leftOffset = 3;
            const int rightOffset = 2;
            const int span = 7;
            const int brightness = 200;

            if (!s.Initialized)
            {
                _leftEye.SetBrightness(brightness);
                _rightEye.SetBrightness(brightness);
                s.CycleCount = 0;
                s.Phase = 0;
                s.Q = 0;
                s.NextRun = now;
                s.Initialized = true;
            }
            if (now < s.NextRun) return s.NextRun;

            Rgb24 color = new(0, 255, 0); // exact Color(0,255,0) in the sketch
            switch (s.Phase)
            {
                case 0:
                    _rightEye.SetPixel(s.Q + rightOffset, color);
                    if (s.Q > 0) _rightEye.SetPixel(s.Q + rightOffset - 1, Rgb24.Black);
                    _rightEye.Show();
                    s.Q++;
                    if (s.Q >= span)
                    {
                        _rightEye.SetPixel(span - 1 + rightOffset, Rgb24.Black);
                        _rightEye.Show();
                        s.Q = 0; s.Phase++;
                    }
                    break;
                case 1:
                    _leftEye.SetPixel(s.Q + leftOffset, color);
                    if (s.Q > 0) _leftEye.SetPixel(s.Q + leftOffset - 1, Rgb24.Black);
                    _leftEye.Show();
                    s.Q++;
                    if (s.Q >= span)
                    {
                        _leftEye.SetPixel(span - 1 + leftOffset, Rgb24.Black);
                        _leftEye.Show();
                        s.Q = span - 1; s.Phase++;
                    }
                    break;
                case 2:
                    _leftEye.SetPixel(s.Q + leftOffset, color);
                    _leftEye.SetPixel(s.Q + leftOffset + 1, Rgb24.Black);
                    _leftEye.Show();
                    s.Q--;
                    if (s.Q < 0)
                    {
                        _leftEye.SetPixel(leftOffset, Rgb24.Black);
                        _leftEye.Show();
                        s.Q = span - 1; s.Phase++;
                    }
                    break;
                case 3:
                    _rightEye.SetPixel(s.Q + rightOffset, color);
                    _rightEye.SetPixel(s.Q + rightOffset + 1, Rgb24.Black);
                    _rightEye.Show();
                    s.Q--;
                    if (s.Q < 0)
                    {
                        _rightEye.SetPixel(rightOffset, Rgb24.Black);
                        _rightEye.Show();
                        s.Q = 0; s.Phase = 0; s.CycleCount++;
                        if (s.CycleCount >= _g.Pulses)
                        {
                            SetColorAll(Rgb24.Black, 0, true, true, true, true);
                            s.Reset();
                            return 0;
                        }
                    }
                    break;
            }

            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long RainbowStep(long now, bool cycle)
        {
            var s = cycle ? _rainbowCycle : _rainbow;
            if (!s.Initialized || s.Finished)
            {
                SetBrightness(_g.Brightness, true, false, _g.Left, _g.Right);
                s.J = 0;
                s.NextRun = now;
                s.Initialized = true;
                s.Finished = false;
            }
            if (now < s.NextRun) return s.NextRun;

            for (int i = 0; i < NumLeds; i++)
            {
                int colorIndex = cycle
                    ? ((i * 256 / NumLeds) + s.J) & 255
                    : (i + s.J) & 255;
                var c = Wheel((byte)colorIndex);
                if (_g.Left) _leftEye.SetPixel(i, c);
                if (_g.Right) _rightEye.SetPixel(i, c);
            }
            if (_g.Left) _leftEye.Show();
            if (_g.Right) _rightEye.Show();

            s.J++;
            int finish = cycle ? 256 * 3 : 256;
            if (s.J >= finish)
            {
                s.J = 0;
                s.NextRun = 0;
                s.Finished = true;
                s.Initialized = false;
                if (cycle)
                    SetColorAll(Rgb24.Black, 0, true, true, true, true);
                return 0;
            }

            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private long RainbowChaseStep(long now)
        {
            var s = _rainbowChase;
            if (!s.Initialized)
            {
                SetBrightness(_g.Brightness, true, false, _g.Left, _g.Right);
                s.J = 0; s.Q = 0; s.NextRun = now; s.Initialized = true;
            }
            if (now < s.NextRun) return s.NextRun;

            for (int i = 0; i < NumLeds; i += 3)
            {
                int n = i + s.Q;
                if (n >= NumLeds) continue;
                var c = Wheel((byte)((i + s.J) % 255));
                if (_g.Left) _leftEye.SetPixel(n, c);
                if (_g.Right) _rightEye.SetPixel(n, c);
            }
            if (_g.Left) _leftEye.Show();
            if (_g.Right) _rightEye.Show();
            for (int i = 0; i < NumLeds; i += 3)
            {
                int n = i + s.Q;
                if (n >= NumLeds) continue;
                if (_g.Left) _leftEye.SetPixel(n, Rgb24.Black);
                if (_g.Right) _rightEye.SetPixel(n, Rgb24.Black);
            }

            s.Q++;
            if (s.Q >= 3)
            {
                s.Q = 0;
                s.J++;
                if (s.J >= 256)
                {
                    s.J = 0;
                    s.Initialized = false;
                    s.NextRun = 0;
                    // Exact sketch: final displayed chase frame remains visible.
                    return 0;
                }
            }
            s.NextRun = now + Math.Max(1, _g.DelayMs);
            return s.NextRun;
        }

        private static Rgb24 Wheel(byte wheelPos)
        {
            int p = 255 - wheelPos;
            if (p < 85)
                return new Rgb24(255 - p * 3, 0, p * 3);
            if (p < 170)
            {
                p -= 85;
                return new Rgb24(0, p * 3, 255 - p * 3);
            }
            p -= 170;
            return new Rgb24(p * 3, 255 - p * 3, 0);
        }

        private void ParseTargets(string ring, string side)
        {
            string r = (ring ?? "").ToUpperInvariant();
            _g.Eyes = r == "EYES" || r == "BOTH";
            _g.Vents = r == "VENTS" || r == "BOTH";
            ParseSide(side);
        }

        private void ParseSide(string side)
        {
            string s = (side ?? "").ToUpperInvariant();
            _g.Left = s == "LEFT" || s == "LR";
            _g.Right = s == "RIGHT" || s == "LR";
        }

        private void SetColorAll(Rgb24 color, int brightness,
                                 bool eyes, bool vents, bool left, bool right)
        {
            SetBrightness(brightness, eyes, vents, left, right);
            SetPixelsAll(color, eyes, vents, left, right);
            Show(eyes, vents, left, right);
        }

        private void SetBrightness(int brightness, bool eyes, bool vents, bool left, bool right)
        {
            int b = Math.Clamp(brightness, 0, 255);
            if (eyes)
            {
                if (left) _leftEye.SetBrightness(b);
                if (right) _rightEye.SetBrightness(b);
            }
            if (vents)
            {
                if (left) _leftVent.SetBrightness(b);
                if (right) _rightVent.SetBrightness(b);
            }
        }

        private void SetPixelsAll(Rgb24 color, bool eyes, bool vents, bool left, bool right)
        {
            if (eyes)
            {
                if (left) _leftEye.SetAllPixels(color);
                if (right) _rightEye.SetAllPixels(color);
            }
            if (vents)
            {
                if (left) _leftVent.SetAllPixels(color);
                if (right) _rightVent.SetAllPixels(color);
            }
        }

        private void Show(bool eyes, bool vents, bool left, bool right)
        {
            if (eyes)
            {
                if (left) _leftEye.Show();
                if (right) _rightEye.Show();
            }
            if (vents)
            {
                if (left) _leftVent.Show();
                if (right) _rightVent.Show();
            }
        }

        private RgbRingFrame Snapshot() => new(
            _leftEye.DisplayColors(), _leftVent.DisplayColors(),
            _rightEye.DisplayColors(), _rightVent.DisplayColors());

        private static byte Byte(string[] t, int i, int fallback = 0) =>
            (byte)Math.Clamp(Int(t, i, fallback), 0, 255);
        private static int UInt(string[] t, int i, int fallback) =>
            Math.Max(0, Int(t, i, fallback));
        private static int Int(string[] t, int i, int fallback)
        {
            if (i < 0 || i >= t.Length) return fallback;
            return int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v : fallback;
        }
        private static string StringAt(string[] t, int i) =>
            i >= 0 && i < t.Length ? t[i] : "";

        private readonly record struct RgbEvent(long TimeMs, string Text);

        private sealed class Globals
        {
            public bool Left, Right, Eyes, Vents, FadeIn;
            public int DelayMs, Pulses, Step, Brightness, LowestBrightness;
            public Rgb24 AllColor;
            public Globals() => ResetDefaults();
            public void ResetDefaults()
            {
                Left = Right = Eyes = Vents = FadeIn = true;
                DelayMs = 40; Pulses = 3; Step = 1; Brightness = 200;
                LowestBrightness = 0; AllColor = Rgb24.Black;
            }
        }

        private sealed class RingState
        {
            private readonly Rgb24[] _buffer = Enumerable.Repeat(Rgb24.Black, NumLeds).ToArray();
            private readonly Rgb24[] _display = Enumerable.Repeat(Rgb24.Black, NumLeds).ToArray();
            private int _brightness;
            public void Clear()
            {
                Array.Fill(_buffer, Rgb24.Black);
                Array.Fill(_display, Rgb24.Black);
                _brightness = 0;
            }
            public void SetBrightness(int b) => _brightness = Math.Clamp(b, 0, 255);
            public void SetPixel(int i, Rgb24 c)
            {
                if ((uint)i < (uint)NumLeds) _buffer[i] = c;
            }
            public void SetAllPixels(Rgb24 c) => Array.Fill(_buffer, c);
            public void Show()
            {
                for (int i = 0; i < NumLeds; i++)
                    _display[i] = _buffer[i].Scale(_brightness / 255.0);
            }
            public Color[] DisplayColors()
            {
                var result = new Color[NumLeds];
                for (int i = 0; i < NumLeds; i++) result[i] = _display[i].ToColor();
                return result;
            }
        }

        private readonly record struct Rgb24(int R, int G, int B)
        {
            public static readonly Rgb24 Black = new(0, 0, 0);
            public Rgb24 Scale(double s) => new(
                (int)Math.Round(Math.Clamp(R * s, 0, 255)),
                (int)Math.Round(Math.Clamp(G * s, 0, 255)),
                (int)Math.Round(Math.Clamp(B * s, 0, 255)));
            public Color ToColor() => Color.FromRgb(
                (byte)Math.Clamp(R, 0, 255),
                (byte)Math.Clamp(G, 0, 255),
                (byte)Math.Clamp(B, 0, 255));
        }

        private sealed class PulseState { public int Level, Pulse, Direction = 1; public long NextRun; public void Reset(){Level=Pulse=0;Direction=1;NextRun=0;} }
        private sealed class FadeState { public int Level, Direction; public bool Initialized; public long NextRun; public void Reset(){Level=Direction=0;Initialized=false;NextRun=0;} }
        private sealed class WipeState { public int I; public bool Initialized; public long NextRun; public void Reset(){I=0;Initialized=false;NextRun=0;} }
        private sealed class TheaterState { public int J,Q; public long NextRun; public void Reset(){J=Q=0;NextRun=0;} }
        private sealed class CylonState { public bool Initialized; public int CycleCount,Phase,Q; public long NextRun; public void Reset(){Initialized=false;CycleCount=Phase=Q=0;NextRun=0;} }
        private sealed class RainbowState { public bool Initialized,Finished; public int J; public long NextRun; public void Reset(){Initialized=Finished=false;J=0;NextRun=0;} }
        private sealed class RainbowChaseState { public bool Initialized; public int J,Q; public long NextRun; public void Reset(){Initialized=false;J=Q=0;NextRun=0;} }
    }

    public sealed class RgbRingFrame
    {
        public RgbRingFrame(Color[] leftEye, Color[] leftVent, Color[] rightEye, Color[] rightVent)
        {
            LeftEye = leftEye; LeftVent = leftVent;
            RightEye = rightEye; RightVent = rightVent;
        }
        public Color[] LeftEye { get; }
        public Color[] LeftVent { get; }
        public Color[] RightEye { get; }
        public Color[] RightVent { get; }
    }
}
