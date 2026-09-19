using ServoAnimator;

internal static partial class Program
{
    private static void FrameRateCounterChecks()
    {
        foreach (int hz in new[] { 10, 15, 24, 30, 60, 75, 120, 144, 240, 360, 1000 })
        {
            var counter = new RenderFrameRateCounter();
            for (int frame = 0; frame <= hz * 3; frame++)
            {
                double wallTime = frame / (double)hz;
                var renderingTime = TimeSpan.FromSeconds(wallTime);
                counter.Record(renderingTime, wallTime);
                counter.Record(renderingTime, wallTime + .00001);
            }
            Check(Math.Abs(counter.Read(3) - hz) <= 1, $"{hz} Hz is counted once per timestamp without requiring model changes");
            Check(counter.Read(4.01) == 0, "A stalled rendering stream decays to zero using wall time");
            counter.Reset();
            Check(counter.Read(0) == 0, "A hidden/reopened view starts with an empty measurement window");
        }
        var startup = new RenderFrameRateCounter();
        startup.Record(TimeSpan.Zero, 0);
        Check(startup.Read(.001) == 0, "One initial callback cannot produce an inflated startup rate");
        for (int frame = 1; frame <= 30; frame++) startup.Record(TimeSpan.FromSeconds(frame / 60.0), frame / 60.0);
        Check(startup.Read(.5) == 60, "Startup uses elapsed intervals before a full rolling second is available");
        startup.Record(TimeSpan.Zero, 2);
        Check(startup.Read(2) == 0, "A reset WPF rendering timestamp restarts measurement safely");

        var counterHot = new RenderFrameRateCounter();
        for (int i = 0; i < 2000; i++) counterHot.Record(TimeSpan.FromTicks(i), i / 60.0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2000; i < 102000; i++) counterHot.Record(TimeSpan.FromTicks(i), i / 60.0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Recording 100,000 frames makes zero managed allocations");
        Console.WriteLine($"FPS sampling: {allocated} bytes allocated across 100,000 callbacks.");
    }
}
