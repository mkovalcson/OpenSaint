using System.IO;
using NAudio.Wave;
using ServoAnimator;

internal static partial class Program
{
    private static void AudioMovieCreation()
    {
        string root = Path.Combine(Path.GetTempPath(), "audio-movie-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string filename in new[] { "10 Last.wav", "2 Middle.wav", "01 First.wav", "unumbered.wav" })
            {
                using var writer = new WaveFileWriter(Path.Combine(root, filename), new WaveFormat(8000, 16, 1));
                writer.Write(new byte[16000], 0, 16000);
            }
            File.WriteAllText(Path.Combine(root, "0 Notes.txt"), "ignored");
            string moviePath = AudioMovieCreator.Create(root, root, "Test");
            var movie = MovieDocument.Load(moviePath);
            Check(movie.Sequences.Count == 3, "Only numbered audio files should become sequences.");
            Check(movie.Sequences.Select(Path.GetFileName).SequenceEqual(new[] { "01 First.wav.json", "2 Middle.wav.json", "10 Last.wav.json" }), "Audio prefixes must sort numerically.");
            foreach (string sequence in movie.Sequences)
            {
                var doc = AnimationDocument.Load(Path.Combine(root, sequence));
                Check(Math.Abs(doc.DurationSeconds - 1) < 0.001, "Sequence duration should match audio.");
                Check(File.Exists(Path.Combine(root, doc.AudioFilePath)) && !Path.IsPathRooted(doc.AudioFilePath), "Audio must resolve through portable relative paths.");
                Check(doc.Commands.Count == 0 && doc.AudioStartOffsetSeconds == 0, "Imported sequence starts with audio at zero and no commands.");
            }
            string original = File.ReadAllText(moviePath);
            bool rejected = false;
            try { AudioMovieCreator.Create(root, root, "Test"); } catch (IOException) { rejected = true; }
            Check(rejected && File.ReadAllText(moviePath) == original, "An existing movie must not be overwritten.");
            File.WriteAllText(Path.Combine(root, "3 Broken.wav"), "invalid audio");
            rejected = false;
            try { AudioMovieCreator.Create(root, root, "Broken"); } catch (IOException) { rejected = true; }
            Check(rejected && !File.Exists(Path.Combine(root, "Broken.json")) && !Directory.Exists(Path.Combine(root, "Broken.Sequences")), "Failed decoding must leave no partial movie.");
        }
        finally { Directory.Delete(root, true); }
    }
}
