using System.IO;
using NAudio.Wave;

namespace ServoAnimator
{
    internal sealed class VersionedFileCache<T>
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, (long Size, long Modified, long Used, Lazy<Task<T>> Work)> _entries =
            new(StringComparer.OrdinalIgnoreCase);
        private long _use;
        private readonly int _capacity;
        public VersionedFileCache(int capacity) => _capacity = capacity;

        public Task<T> Get(string path, Func<string, T> read)
        {
            path = Path.GetFullPath(path);
            var file = new FileInfo(path);
            if (!file.Exists) return Task.FromException<T>(new FileNotFoundException("File not found", path));
            long size = file.Length, modified = file.LastWriteTimeUtc.Ticks;
            lock (_gate)
            {
                if (_entries.TryGetValue(path, out var old) && old.Size == size && old.Modified == modified &&
                    (!old.Work.IsValueCreated || !old.Work.Value.IsFaulted))
                {
                    _entries[path] = (old.Size, old.Modified, ++_use, old.Work);
                    return old.Work.Value;
                }
                var work = new Lazy<Task<T>>(() => Task.Run(() => read(path)));
                _entries.Remove(path);
                _entries.Add(path, (size, modified, ++_use, work));
                while (_entries.Count > _capacity)
                    _entries.Remove(_entries.MinBy(e => e.Value.Used).Key);
                return work.Value;
            }
        }

        public void Clear() { lock (_gate) _entries.Clear(); }
    }

    internal sealed record AudioPeaks(float[] Min, float[] Max, double Duration);
    internal sealed record SequenceInfo(AnimationDocument Document, double Duration, string AudioFiles);

    /// <summary>Shared by primary audio, inserted clips and upcoming movie cues.
    /// Worker tasks never access WPF or hardware. File stamps invalidate edits.</summary>
    internal sealed class MediaAssetCache
    {
        private readonly VersionedFileCache<AudioPeaks> _audio = new(12);
        private readonly VersionedFileCache<double> _durations = new(256);
        private readonly VersionedFileCache<AnimationDocument> _sequences = new(64);
        private readonly SemaphoreSlim _decoders = new(2);

        public Task<AnimationDocument> Sequence(string path) => _sequences.Get(path, AnimationDocument.Load);
        public Task<double> Duration(string path) => _durations.Get(path, p =>
        {
            using var reader = new AudioFileReader(p);
            return reader.TotalTime.TotalSeconds;
        });
        public Task<AudioPeaks> Audio(string path) => _audio.Get(path, Decode);
        public void Clear() { _audio.Clear(); _durations.Clear(); _sequences.Clear(); }

        public static string ResolveAudio(string root, string sequencePath, string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (ConfigPathService.TryResolve(root, stored, out string full) && File.Exists(full)) return full;
            foreach (string folder in new[] { Path.GetDirectoryName(sequencePath), Path.Combine(root, "Projects") })
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                full = Path.Combine(folder, Path.GetFileName(stored));
                if (ConfigPathService.IsWithin(root, full) && File.Exists(full)) return full;
            }
            return null;
        }

        public async Task<AnimationDocument> PrepareSequence(string path, string root)
        {
            var doc = await Sequence(path).ConfigureAwait(false);
            var references = doc.Commands.Where(c => c.Servo == ServoNames.Play).Select(c => c.TextValue)
                .Append(string.IsNullOrWhiteSpace(doc.AudioFilePath) ? doc.AudioFile : doc.AudioFilePath);
            foreach (string audio in references.Select(p => ResolveAudio(root, path, p))
                         .Where(p => p != null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try { await Audio(audio).ConfigureAwait(false); }
                catch { /* Keep the document editable; the UI offers missing-file repair. */ }
            }
            return doc;
        }

        public async Task<SequenceInfo> DescribeSequence(string path, string root)
        {
            var doc = await Sequence(path).ConfigureAwait(false);
            double duration = Math.Max(0, doc.DurationSeconds);
            if (doc.Commands.Count > 0) duration = Math.Max(duration, doc.Commands.Max(c => c.OffsetSeconds));
            var sources = doc.Commands.Where(c => c.Servo == ServoNames.Play)
                .Select(c => (Path: c.TextValue, Start: c.OffsetSeconds))
                .Append((Path: string.IsNullOrWhiteSpace(doc.AudioFilePath) ? doc.AudioFile : doc.AudioFilePath,
                         Start: Math.Max(0, doc.AudioStartOffsetSeconds))).ToList();
            foreach (var source in sources)
            {
                string audio = ResolveAudio(root, path, source.Path);
                if (audio == null) continue;
                try { duration = Math.Max(duration, source.Start + await Duration(audio).ConfigureAwait(false)); }
                catch { /* Missing or unreadable audio must not hide the sequence. */ }
            }
            return new SequenceInfo(doc, Math.Max(0.05, duration), string.Join(", ", sources
                .Select(s => Path.GetFileName(s.Path)).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        private AudioPeaks Decode(string path)
        {
            _decoders.Wait();
            try
            {
                using var reader = new AudioFileReader(path);
                double duration = reader.TotalTime.TotalSeconds;
                int count = Math.Max(1, checked((int)Math.Ceiling(duration * 1000)));
                var min = new float[count];
                var max = new float[count];
                Array.Fill(min, float.MaxValue);
                Array.Fill(max, float.MinValue);
                int channels = reader.WaveFormat.Channels;
                int rate = reader.WaveFormat.SampleRate;
                var buffer = new float[rate * channels];
                long frame = 0;
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    for (int i = 0; i < read; i += channels, frame++)
                    {
                        int bucket = Math.Min(count - 1, (int)(frame * 1000 / rate));
                        for (int c = 0; c < channels && i + c < read; c++)
                        {
                            min[bucket] = Math.Min(min[bucket], buffer[i + c]);
                            max[bucket] = Math.Max(max[bucket], buffer[i + c]);
                        }
                    }
                for (int i = 0; i < count; i++)
                    if (min[i] > max[i]) min[i] = max[i] = 0;
                PeakEnvelope.For(min, max); // build summaries on the decoder worker
                return new AudioPeaks(min, max, duration);
            }
            finally { _decoders.Release(); }
        }
    }
}
