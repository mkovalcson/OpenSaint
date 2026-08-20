using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ServoAnimator
{
    /// <summary>
    /// Application-wide native help service. Help content is loaded lazily from
    /// the Help folder only after the user requests help, so startup/playback
    /// incur no documentation parsing or search-index cost.
    /// </summary>
    public static class HelpSystem
    {
        public static readonly DependencyProperty TopicProperty = DependencyProperty.RegisterAttached(
            "Topic", typeof(string), typeof(HelpSystem), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        private static HelpWindow _window;

        /// <summary>MainWindow supplies a playback-aware predicate. When false,
        /// F1/menu help is ignored and an already-open help window is closed.</summary>
        public static Func<bool> IsHelpAvailable { get; set; } = () => true;

        public static void SetTopic(DependencyObject element, string value) => element?.SetValue(TopicProperty, value);
        public static string GetTopic(DependencyObject element) => element?.GetValue(TopicProperty) as string;

        public static void EnableContextHelp(Window window, string defaultTopic)
        {
            if (window == null) return;
            SetTopic(window, defaultTopic);
            window.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.F1 || Keyboard.Modifiers != ModifierKeys.None) return;
                if (!CanShowHelp()) { e.Handled = true; return; }
                ShowContextHelp(window);
                e.Handled = true;
            };
        }

        public static bool CanShowHelp() => IsHelpAvailable?.Invoke() != false;

        public static void ShowContents(Window owner) => ShowHelp(owner, "getting-started");

        public static void ShowContextHelp(Window owner)
        {
            string topic = ResolveTopic(Keyboard.FocusedElement as DependencyObject)
                           ?? GetTopic(owner)
                           ?? "getting-started";
            ShowHelp(owner, topic);
        }

        public static void ShowHelp(Window owner, string topicId)
        {
            if (!CanShowHelp()) return;

            // Recreate when ownership changes so Help stays above the active
            // modal configuration/editor dialog rather than hiding behind it.
            if (_window != null && _window.IsVisible && owner != null && !ReferenceEquals(_window.Owner, owner))
            {
                _window.Close();
                _window = null;
            }

            if (_window == null || !_window.IsLoaded)
            {
                _window = new HelpWindow();
                _window.Closed += (_, _) => _window = null;
                if (owner != null && owner.IsLoaded) _window.Owner = owner;
                _window.Show();
            }
            else
            {
                if (_window.WindowState == WindowState.Minimized)
                    _window.WindowState = WindowState.Normal;
                if (!_window.IsVisible) _window.Show();
                _window.Activate();
            }

            _window.NavigateTo(topicId);
        }

        public static void CloseHelpWindow()
        {
            if (_window == null) return;
            try { _window.Close(); } catch { }
            _window = null;
        }

        private static string ResolveTopic(DependencyObject start)
        {
            for (DependencyObject current = start; current != null; current = ParentOf(current))
            {
                string topic = GetTopic(current);
                if (!string.IsNullOrWhiteSpace(topic)) return topic;
            }
            return null;
        }

        private static DependencyObject ParentOf(DependencyObject child)
        {
            if (child == null) return null;
            try
            {
                var visualParent = VisualTreeHelper.GetParent(child);
                if (visualParent != null) return visualParent;
            }
            catch { }
            return LogicalTreeHelper.GetParent(child);
        }
    }

    public sealed class HelpTopicDefinition
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string File { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] Related { get; set; } = Array.Empty<string>();
        public override string ToString() => Title;
    }

    public sealed class HelpIndexDefinition
    {
        public int Version { get; set; } = 1;
        public List<HelpTopicDefinition> Topics { get; set; } = new();
    }

    /// <summary>Lazy help-content catalog and simple full-text search.</summary>
    public sealed class HelpCatalog
    {
        private static readonly Lazy<HelpCatalog> _lazy = new(() => Load());
        public static HelpCatalog Current => _lazy.Value;

        private readonly string _helpFolder;
        private readonly Dictionary<string, HelpTopicDefinition> _byId;
        private readonly Dictionary<string, string> _textCache = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<HelpTopicDefinition> Topics { get; }

        private HelpCatalog(string folder, IEnumerable<HelpTopicDefinition> topics)
        {
            _helpFolder = folder;
            Topics = topics.ToList();
            _byId = Topics.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        }

        public HelpTopicDefinition Find(string id) =>
            !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out var topic) ? topic : Topics.FirstOrDefault();

        public string Read(HelpTopicDefinition topic)
        {
            if (topic == null) return "# Help\n\nHelp topic not found.";
            if (_textCache.TryGetValue(topic.Id, out var cached)) return cached;
            try
            {
                string path = Path.Combine(_helpFolder, topic.File ?? "");
                string text = File.Exists(path)
                    ? File.ReadAllText(path)
                    : $"# {topic.Title}\n\nThe help file `{topic.File}` could not be found.";
                _textCache[topic.Id] = text;
                return text;
            }
            catch (Exception ex)
            {
                return $"# {topic.Title}\n\nUnable to load this help topic.\n\n{ex.Message}";
            }
        }

        public IReadOnlyList<HelpTopicDefinition> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Topics;
            string[] terms = query.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length == 0) return Topics;

            return Topics
                .Select(topic => new { Topic = topic, Score = Score(topic, terms) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Topic.Title)
                .Select(x => x.Topic)
                .ToList();
        }

        private int Score(HelpTopicDefinition topic, string[] terms)
        {
            string title = topic.Title ?? "";
            string keywords = string.Join(' ', topic.Keywords ?? Array.Empty<string>());
            string body = Read(topic);
            int score = 0;
            foreach (string term in terms)
            {
                if (title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 20;
                if (keywords.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 10;
                if (body.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 2;
                else return 0; // AND-style search keeps results focused.
            }
            return score;
        }

        private static HelpCatalog Load()
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "Help");
            string indexPath = Path.Combine(folder, "HelpIndex.json");
            try
            {
                if (File.Exists(indexPath))
                {
                    var index = JsonSerializer.Deserialize<HelpIndexDefinition>(File.ReadAllText(indexPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (index?.Topics?.Count > 0)
                        return new HelpCatalog(folder, index.Topics);
                }
            }
            catch { }

            return new HelpCatalog(folder, new[]
            {
                new HelpTopicDefinition { Id = "getting-started", Title = "Getting Started", File = "GettingStarted.md" }
            });
        }
    }
}
