using System.IO;
using System.Text.Json;

namespace ServoAnimator
{
    /// <summary>Category names are local to each Library root. Assignments live in item JSON;
    /// the catalog also retains empty categories without appearing as a Library item.</summary>
    internal sealed class LibraryCategories
    {
        private readonly string _catalogPath;
        private readonly IReadOnlyList<LibraryItemInfo> _items;
        public List<string> Names { get; } = new();
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? "none" :
            value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? "none" : value.Trim();

        public LibraryCategories(string folder, IReadOnlyList<LibraryItemInfo> items)
        {
            _catalogPath = Path.Combine(folder, ".library-categories");
            _items = items;
            Names.Add("none");
            if (File.Exists(_catalogPath))
                Names.AddRange(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_catalogPath)) ?? new());
            Names.AddRange(items.Select(i => i.Category));
            Sort();
        }

        private void Sort()
        {
            var sorted = Names.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            Names.Clear(); Names.AddRange(sorted);
        }

        private string ValidateNew(string name, string replacing = null)
        {
            name = Normalize(name);
            if (name.Length > 100 || name.Any(char.IsControl)) throw new ArgumentException("Use a category name of 1–100 characters.");
            if (Names.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                !n.Equals(replacing, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("That category already exists.");
            return name;
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_catalogPath));
            string temporary = _catalogPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(Names));
                File.Move(temporary, _catalogPath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void Add(string name)
        {
            name = ValidateNew(name);
            Names.Add(name); Sort();
            try { Save(); }
            catch { Names.Remove(name); throw; }
        }

        public void Assign(LibraryItemInfo item, string name)
        {
            name = Names.FirstOrDefault(n => n.Equals(Normalize(name), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Choose an existing category.");
            AnimationDocument.UpdateLibraryCategory(item.FullPath, name);
            item.Category = name;
            item.Modified = File.GetLastWriteTime(item.FullPath);
        }

        public void RenameOrDelete(string oldName, string newName)
        {
            if (Normalize(oldName) == "none") throw new ArgumentException("The default 'none' category cannot be renamed or deleted.");
            newName = newName == null ? "none" : ValidateNew(newName, oldName);
            var affected = _items.Where(i => i.IsValid && i.Category.Equals(oldName, StringComparison.OrdinalIgnoreCase)).ToList();
            // Read every original before changing any file. Roll back successful writes on failure.
            var originals = affected.ToDictionary(i => i, i => File.ReadAllText(i.FullPath));
            var written = new List<LibraryItemInfo>();
            var oldNames = Names.ToList();
            try
            {
                foreach (var item in affected)
                {
                    AnimationDocument.UpdateLibraryCategory(item.FullPath, newName);
                    written.Add(item);
                }
                Names.RemoveAll(n => n.Equals(oldName, StringComparison.OrdinalIgnoreCase));
                if (newName != "none") Names.Add(newName);
                Sort(); Save();
            }
            catch (Exception error)
            {
                Names.Clear(); Names.AddRange(oldNames);
                var failures = new List<Exception> { error };
                foreach (var item in written)
                    try { File.WriteAllText(item.FullPath, originals[item]); }
                    catch (Exception rollback) { failures.Add(rollback); }
                throw new AggregateException("Category update failed; original assignments were restored where possible.", failures);
            }
            foreach (var item in affected)
            {
                item.Category = newName;
                item.Modified = File.GetLastWriteTime(item.FullPath);
            }
        }
    }
}
