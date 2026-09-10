using System.IO;

namespace ServoAnimator
{
    /// <summary>A new editor draft belongs to one Movie and is appended only after a successful save.</summary>
    internal sealed class PendingMovieSequence
    {
        public string Name { get; }
        public string MoviePath { get; set; }
        private readonly AnimationDocument _document;
        private bool _appended;

        public PendingMovieSequence(string name, string moviePath, AnimationDocument document)
        {
            Name = name;
            MoviePath = moviePath;
            _document = document;
        }

        public bool IsFor(AnimationDocument document, string moviePath) =>
            !_appended && ReferenceEquals(document, _document) &&
            !string.IsNullOrWhiteSpace(moviePath) &&
            string.Equals(MoviePath, moviePath, StringComparison.OrdinalIgnoreCase);

        public bool AppendAfterSave(AnimationDocument document, string moviePath, string savedPath,
            double duration, List<MovieSequenceItem> items)
        {
            if (!IsFor(document, moviePath) || string.IsNullOrWhiteSpace(savedPath)) return false;
            items.Add(new MovieSequenceItem
            {
                FilePath = savedPath,
                DurationSeconds = Math.Max(0.001, duration),
                Description = document.Description ?? "",
            });
            _appended = true;
            return true;
        }

        public static bool TryName(string input, out string name)
        {
            name = (input ?? "").Trim();
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
            if (name.Length == 0 || name.Length > 150 || name.EndsWith('.') || name.EndsWith(' ') ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            string stem = name.Split('.')[0].ToUpperInvariant();
            return stem is not ("CON" or "PRN" or "AUX" or "NUL") &&
                !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                  "123456789¹²³".Contains(stem[3]));
        }
    }
}
