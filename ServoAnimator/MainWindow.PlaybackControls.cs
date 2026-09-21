using System.Windows;
using NAudio.Wave;

namespace ServoAnimator;

public partial class MainWindow
{
    private ServoNames _lastInsertedServo = ServoNames.NeckTurn;
    private RobotControls? _lastInsertedChild;

    private void StopLiveDriveFromEscape()
    {
        if (!LiveDrive) return;
        if (RecordingController) StopControllerRecording("Recording stopped by Escape.");
        StopRecordingReplay();
        EndArrowPrompt();
        EndMovieBackgroundControl();
        LiveDriveBtn.IsChecked = false;
        DisableAll_Click(this, new RoutedEventArgs());
        ForEachHeadView(v => v.CalibratedMotionPaused = true);
        ShowStatus("STOP — playback stopped, servos disabled, Live Drive off.");
    }

    private void PlaybackSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_playbackClock == null || PlaybackSpeedSlider == null) return;
        bool running = IsRunning;
        if (running) PausePlayback();
        _playbackClock.Rate = Math.Clamp(PlaybackSpeedSlider.Value, .1, 2);
        if (PlaybackSpeedText != null) PlaybackSpeedText.Text = $"{_playbackClock.Rate * 100:0}%";
        if (running) StartPlaybackAt(_cursorTime);
    }
}

// Declaring the source sample rate at the requested ratio lets WDL resample
// into the device's original rate. Audio duration and the timeline then agree.
internal sealed class RateSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public WaveFormat WaveFormat { get; }
    public RateSampleProvider(ISampleProvider source, double rate)
    {
        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            (int)Math.Round(source.WaveFormat.SampleRate * rate), source.WaveFormat.Channels);
    }
    public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
}
