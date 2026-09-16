using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using NAudio.Wave;

namespace ServoAnimator;

public partial class MainWindow
{
    private ControllerLibraryRun _controllerLibraryRun;
    private readonly Stopwatch _controllerLibraryClock = new();
    private double _controllerLibraryLastTime;
    private string _controllerLibraryLabel;
    private bool _controllerLibraryHasOutput;
    private readonly List<(double Start, string Path, double Duration)> _controllerLibraryAudio = new();
    private AudioFileReader _controllerLibraryReader;
    private WaveOutEvent _controllerLibraryOutput;
    private int _controllerLibraryAudioIndex = -1;
    private bool _apiLibraryOwnsOutput;
    private bool _apiLibraryUrdf, _apiLibraryPhysical;
    private string _controllerLibraryKind;

    private string ChooseControllerLibrary(string kind, Window owner)
    {
        var picker = new LibraryItemSelectionWindow(ApiLibraryRoot(kind), manageMode: false,
            itemLabel: kind == "pose" ? "Library Pose" : "Library Sequence", showAudioFiles: kind == "sequence") { Owner = owner };
        return picker.ShowDialog() == true ? picker.SelectedLibraryItem?.RelativePath : null;
    }

    private void StartControllerLibrary(ControllerIntent intent)
        => StartLibraryPlayback(intent, api: false, urdf: true, physical: true);

    private void StartLibraryPlayback(ControllerIntent intent, bool api, bool urdf, bool physical, bool streamDeck = false)
    {
        if (_speedCalibrationBusy) throw new InvalidOperationException("Stop speed calibration before Library playback.");
        EndMovieBackgroundControl();
        SetPlaybackControlSource(null);
        // Cancel the old sequence and its queued output BEFORE loading the replacement.
        StopPlayback();
        if (api) { _enabledController = null; ResetControllerMotion(); }
        _apiLibraryOwnsOutput = api; _apiLibraryUrdf = urdf; _apiLibraryPhysical = physical;
        _streamDeckOwnsOutput = streamDeck;
        RefreshControllerOutputPermissions();
        _controllerPositions.Clear();
        Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear();
        try
        {
            string kind = intent.Target == "Library:pose" ? "pose" : "sequence";
            string root = ApiLibraryRoot(kind);
            string path = ResolveApiLibraryName(root, intent.LibraryName);
            if (!ConfigPathService.IsWithin(root, path)) throw new InvalidDataException("Item is outside the selected Library.");
            var commands = AnimationDocument.LoadCommandsOnly(path).Select(c => c.Clone()).ToList();
            double duration = 0;
            if (kind == "pose") foreach (var c in commands) c.OffsetSeconds = 0;
            else
            {
                // Also accept older full sequence documents with primary audio.
                using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var doc = AnimationDocument.Load(path); duration = doc.DurationSeconds;
                    string audio = string.IsNullOrWhiteSpace(doc.AudioFilePath) ? doc.AudioFile : doc.AudioFilePath;
                    if (!string.IsNullOrWhiteSpace(audio)) commands.Insert(0, new ServoCommand { Servo = ServoNames.Play, TextValue = audio, OffsetSeconds = doc.AudioStartOffsetSeconds });
                }
            }
            // Validate before any output or audio is started.
            _ = new ControllerLibraryRun(commands, duration, intent.Loop && kind == "sequence");
            foreach (var c in commands.Where(c => c.Servo == ServoNames.Play))
            {
                string audio = MediaAssetCache.ResolveAudio(ConfigRoot, path, c.TextValue);
                if (audio == null)
                {
                    string candidate = Path.Combine(LibraryAudioFolder(), Path.GetFileName(c.TextValue ?? ""));
                    if (File.Exists(candidate)) audio = candidate;
                }
                if (audio == null) throw new InvalidDataException("Library audio is missing: " + c.TextValue);
                using var reader = new AudioFileReader(audio);
                double length = reader.TotalTime.TotalSeconds;
                _controllerLibraryAudio.Add((c.OffsetSeconds, audio, length)); duration = Math.Max(duration, c.OffsetSeconds + length);
            }
            _controllerLibraryAudio.Sort((a, b) => a.Start.CompareTo(b.Start));
            _controllerLibraryLabel = intent.LibraryName;
            _controllerLibraryKind = kind;
            _controllerLibraryHasOutput = true;
            _controllerLibraryRun = new ControllerLibraryRun(commands, kind == "pose" ? 0 : duration, intent.Loop && kind == "sequence");
            _controllerLibraryClock.Restart(); _controllerLibraryLastTime = 0;
            TickControllerLibrary(0);
            if (kind == "pose") StopControllerLibrary(cancelPending: false);
            ShowStatus((api ? "API Library " : "Controller Library ") + kind + ": " + intent.LibraryName + (intent.Loop && kind == "sequence" ? " · looping" : ""));
        }
        catch (Exception ex) { StopControllerLibrary(); ShowStatus("Library playback stopped: " + ex.Message); if (api) throw; }
    }

    private void TickControllerLibrary(double? elapsed = null)
    {
        var run = _controllerLibraryRun;
        if (run == null) return;
        double now = _controllerLibraryClock.Elapsed.TotalSeconds;
        var due = run.Advance(elapsed ?? Math.Max(0, now - _controllerLibraryLastTime)); _controllerLibraryLastTime = now;
        if (!AllowControllerTargets(due)) { StopControllerLibrary(); return; }
        foreach (var c in due)
        {
            if (c.Servo == ServoNames.Play) continue;
            if (c.Disable)
                QueueControllerVisual(c.Control.HasValue ? $"Child:{c.Servo}:{c.Control.Value}" : "Servo:" + c.Servo,
                    v => v.SetCalibratedControlEnabled(c.Servo, c.Control, false));
            if (!c.IsTextServo && ControllerOutputAllowed(false))
                ForEachHeadView(v =>
                {
                    ConfigureMotionView(v);
                    // Preserve explicit profiles even if several targets are coalesced
                    // into one display frame and the final target uses N/C.
                    if (!c.Disable) v.ConfigureCalibratedSpeed(c.Servo, c.Control, c.Speed);
                    v.SetCalibratedControlEnabled(c.Servo, c.Control, !c.Disable);
                });
            if (!c.Disable)
            {
                if (c.Servo == ServoNames.RGBCommand)
                {
                    var frame = _rgbSimulator.PreviewCommand(c.TextValue);
                    QueueControllerVisual("RGB", v => v.SetRgbRingFrame(frame));
                }
                else if (!c.IsTextServo)
                {
                    var row = _rows.First(r => r.Servo == c.Servo); row.Speed = c.Speed;
                    _manualPoseOverrides.Add(c.Servo);
                    if (c.Control.HasValue)
                    {
                        var child = row.Children.FirstOrDefault(r => r.Control == c.Control.Value);
                        if (child != null) child.Value = c.NumericValue;
                        QueueControllerVisual($"Child:{c.Servo}:{c.Control.Value}", v => { PrepareDirectMotion(v, c.Servo, c.Control, c.Speed, controller: true); v.SetChildServo(c.Servo, c.Control.Value, c.NumericValue); });
                    }
                    else { row.Value = c.NumericValue; QueueControllerVisual("Servo:" + c.Servo, v => { PrepareDirectMotion(v, c.Servo, null, c.Speed, controller: true); v.SetServo(c.Servo, c.NumericValue); }); }
                }
            }
            if (ControllerOutputAllowed(true) && LiveDrive && _hw.Connected)
            {
                var copy = c.Clone(); long epoch = Interlocked.Read(ref _controllerOutputEpoch);
                _hardwarePlaybackQueue.Enqueue(() =>
                {
                    if (_controllerPhysicalOutputAllowed && epoch == Interlocked.Read(ref _controllerOutputEpoch))
                        DispatchHardwareBatch(new[] { copy });
                });
            }
        }
        if (run.Restarted) StopControllerLibraryAudio();
        int audioIndex = _controllerLibraryAudio.FindLastIndex(a => a.Start <= run.Position && run.Position < a.Start + a.Duration);
        if (audioIndex != _controllerLibraryAudioIndex)
        {
            StopControllerLibraryAudio(); _controllerLibraryAudioIndex = audioIndex;
            if (audioIndex >= 0)
            {
                var audio = _controllerLibraryAudio[audioIndex];
                _controllerLibraryReader = new AudioFileReader(audio.Path);
                _controllerLibraryReader.CurrentTime = TimeSpan.FromSeconds(Math.Max(0, run.Position - audio.Start));
                _controllerLibraryOutput = new WaveOutEvent { DesiredLatency = 100, Volume = _playbackVolume };
                _controllerLibraryOutput.Init(_controllerLibraryReader); _controllerLibraryOutput.Play();
            }
        }
        if (run.Finished)
        {
            StopControllerLibrary(cancelPending: false);
            ShowStatus("Controller Library sequence finished: " + _controllerLibraryLabel);
        }
    }
    private void StopControllerLibraryAudio()
    {
        _controllerLibraryOutput?.Stop(); _controllerLibraryOutput?.Dispose(); _controllerLibraryOutput = null;
        _controllerLibraryReader?.Dispose(); _controllerLibraryReader = null; _controllerLibraryAudioIndex = -1;
    }
    private void StopControllerLibrary(bool cancelPending = true)
    {
        if (cancelPending) _safeguardTargets.Clear();
        if (cancelPending) ForEachHeadView(v => v.StopCalibratedMotion());
        if (cancelPending) _apiLibraryOwnsOutput = false;
        if (cancelPending) _controllerPreview.Clear();
        if (_controllerLibraryHasOutput && cancelPending)
        { _hardwarePlaybackQueue.ClearPending(); _controllerLibraryHasOutput = false; }
        _controllerLibraryRun = null; _controllerLibraryClock.Reset(); _controllerLibraryAudio.Clear();
        StopControllerLibraryAudio();
    }
}
