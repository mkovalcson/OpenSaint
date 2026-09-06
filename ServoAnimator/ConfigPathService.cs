using System.IO;

namespace ServoAnimator
{
    /// <summary>Centralizes the portable-path rule used by project, movie,
    /// recovery, recent-file, audio, and library data. Persisted paths are
    /// relative to the selected Configuration folder; resolved paths are
    /// accepted only when they remain inside that folder.</summary>
    internal static class ConfigPathService
    {
        public static bool IsWithin(string configFolder, string path)
        {
            if (string.IsNullOrWhiteSpace(configFolder) ||
                string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string root = Path.GetFullPath(configFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(full, root, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(root + Path.DirectorySeparatorChar,
                           StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static bool TryResolve(string configFolder, string storedPath,
                                      out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(configFolder) ||
                string.IsNullOrWhiteSpace(storedPath))
                return false;

            try
            {
                string candidate = Path.IsPathRooted(storedPath)
                    ? storedPath
                    : Path.Combine(configFolder, storedPath);
                candidate = Path.GetFullPath(candidate);
                if (!IsWithin(configFolder, candidate)) return false;
                fullPath = candidate;
                return true;
            }
            catch { return false; }
        }

        public static string ToRelative(string configFolder, string path)
        {
            if (!TryResolve(configFolder, path, out string fullPath))
                throw new InvalidOperationException(
                    "The selected path must be inside the Configuration folder.");
            return Path.GetRelativePath(Path.GetFullPath(configFolder), fullPath);
        }

        public static AnimationDocument ResolveDocumentPaths(
            AnimationDocument document, string configFolder)
        {
            if (document == null) return null;
            if (TryResolve(configFolder, document.AudioFilePath, out string primary))
                document.AudioFilePath = primary;
            else if (!string.IsNullOrWhiteSpace(document.AudioFilePath))
                document.AudioFilePath = "";

            foreach (var command in document.Commands ?? new List<ServoCommand>())
            {
                if (command.Servo != ServoNames.Play) continue;
                if (TryResolve(configFolder, command.TextValue, out string audio))
                    command.TextValue = audio;
                else if (!string.IsNullOrWhiteSpace(command.TextValue))
                    command.TextValue = "";
            }
            return document;
        }

        public static AnimationDocument MakeDocumentPathsRelative(
            AnimationDocument document, string configFolder)
        {
            if (document == null) return null;
            if (!string.IsNullOrWhiteSpace(document.AudioFilePath))
                document.AudioFilePath = ToRelative(configFolder, document.AudioFilePath);

            foreach (var command in document.Commands ?? new List<ServoCommand>())
                if (command.Servo == ServoNames.Play &&
                    !string.IsNullOrWhiteSpace(command.TextValue))
                    command.TextValue = ToRelative(configFolder, command.TextValue);
            return document;
        }
    }
}
