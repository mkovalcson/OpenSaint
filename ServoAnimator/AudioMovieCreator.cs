using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace ServoAnimator
{
    internal static class AudioMovieCreator
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".wav", ".aiff", ".wma", ".m4a" };

        public static string[] OrderedAudioFiles(string folder) => Directory.EnumerateFiles(folder)
            .Where(p => Extensions.Contains(Path.GetExtension(p)))
            .Select(p => (Path: p, Prefix: Regex.Match(Path.GetFileName(p), @"^[0-9]+")))
            .Where(p => p.Prefix.Success)
            .OrderBy(p => BigInteger.Parse(p.Prefix.Value))
            .ThenBy(p => Path.GetFileName(p.Path), StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Path).ToArray();

        public static string Create(string configRoot, string folder, string name)
        {
            if (!PendingMovieSequence.TryName(name, out name)) throw new IOException("Invalid movie name.");
            if (!Directory.Exists(folder) || !ConfigPathService.IsWithin(configRoot, folder))
                throw new IOException("Choose a project folder inside the Configuration folder.");
            string moviePath = Path.Combine(folder, name + ".json");
            string sequencesFolder = Path.Combine(folder, name + ".Sequences");
            if (Path.Exists(moviePath) || Path.Exists(sequencesFolder))
                throw new IOException("A movie or sequence folder with this name already exists. Choose a different movie name.");
            var audioFiles = OrderedAudioFiles(folder);
            if (audioFiles.Length == 0) throw new IOException("No supported audio files beginning with numbers were found in this folder.");
            var documents = new List<AnimationDocument>();
            foreach (string audio in audioFiles)
            {
                try
                {
                    using var reader = new AudioFileReader(audio);
                    double duration = reader.TotalTime.TotalSeconds;
                    if (!double.IsFinite(duration) || duration <= 0) throw new IOException("Audio has no usable duration.");
                    documents.Add(new AnimationDocument
                    {
                        Description = Path.GetFileNameWithoutExtension(audio),
                        AudioFile = Path.GetFileName(audio), AudioFiles = Path.GetFileName(audio),
                        AudioFilePath = ConfigPathService.ToRelative(configRoot, audio),
                        DurationSeconds = duration,
                    });
                }
                catch (Exception ex) { throw new IOException($"Cannot read {Path.GetFileName(audio)}: {ex.Message}", ex); }
            }
            // Stage the complete project before publishing; a failed decode writes nothing.
            string staging = Path.Combine(folder, ".audio-movie-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            bool moved = false;
            try
            {
                var movie = new MovieDocument { Description = name };
                for (int i = 0; i < documents.Count; i++)
                {
                    // Keep the audio extension to distinguish e.g. 01.wav and 01.mp3.
                    string filename = Path.GetFileName(audioFiles[i]) + ".json";
                    documents[i].Save(Path.Combine(staging, filename));
                    movie.Sequences.Add(ConfigPathService.ToRelative(configRoot, Path.Combine(sequencesFolder, filename)));
                    movie.SequenceLoops.Add(false);
                    movie.SequenceTriggers.Add("");
                }
                string stagedMovie = Path.Combine(staging, "movie.tmp");
                movie.Save(stagedMovie);
                Directory.Move(staging, sequencesFolder);
                moved = true;
                File.Move(Path.Combine(sequencesFolder, "movie.tmp"), moviePath, overwrite: false);
                return moviePath;
            }
            catch
            {
                Directory.Delete(moved ? sequencesFolder : staging, recursive: true);
                throw;
            }
        }
    }
}
