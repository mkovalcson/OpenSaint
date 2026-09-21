using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using ServoAnimator;

internal static partial class Program
{
    private static void PlaybackControlsChecks()
    {
        foreach (double rate in new[] { .1, 1.0, 2.0 })
        {
            var clock = new PlaybackClock { Rate = rate }; clock.Start(3);
            Check(Math.Abs(clock.AdvanceTo(2, null) - (3 + rate * 2)) < .00001, "Clock advances at selected speed");
            Check(Math.Abs(clock.AdvanceTo(3, 3 + rate * 3) - (3 + rate * 3)) < .00001, "Audio observation stays synchronized");
            var provider = new WdlResamplingSampleProvider(new RateSampleProvider(new OneSecondAudio(), rate), 48000);
            float[] buffer = new float[2048]; int total = 0, count;
            while ((count = provider.Read(buffer, 0, buffer.Length)) > 0) total += count;
            Check(Math.Abs(total / 48000.0 - 1 / rate) < .02, "Audio duration matches timeline rate");
        }
    }
    private sealed class OneSecondAudio : ISampleProvider
    {
        private int _left = 48000;
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        public int Read(float[] buffer, int offset, int count)
        { int read = Math.Min(count, _left); Array.Clear(buffer, offset, read); _left -= read; return read; }
    }
}
