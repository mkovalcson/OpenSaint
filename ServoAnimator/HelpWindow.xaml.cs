using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ServoAnimator
{
    public partial class HelpWindow : Window
    {
        private HelpCatalog Catalog => HelpCatalog.Current;
        private bool _selecting;

        public HelpWindow()
        {
            InitializeComponent();
            TopicList.ItemsSource = Catalog.Topics;
            SearchStatus.Text = $"{Catalog.Topics.Count} topics";
        }

        public void NavigateTo(string topicId)
        {
            var topic = Catalog.Find(topicId);
            if (topic == null) return;
            _selecting = true;
            TopicList.SelectedItem = topic;
            TopicList.ScrollIntoView(topic);
            _selecting = false;
            RenderTopic(topic);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TopicList == null || SearchStatus == null) return;
            var results = Catalog.Search(SearchBox.Text);
            TopicList.ItemsSource = results;
            SearchStatus.Text = string.IsNullOrWhiteSpace(SearchBox.Text)
                ? $"{results.Count} topics"
                : $"{results.Count} matching topic{(results.Count == 1 ? "" : "s")}";
            if (results.Count > 0)
            {
                _selecting = true;
                TopicList.SelectedIndex = 0;
                _selecting = false;
                RenderTopic(results[0]);
            }
        }

        private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selecting) return;
            if (TopicList.SelectedItem is HelpTopicDefinition topic)
                RenderTopic(topic);
        }

        private void RenderTopic(HelpTopicDefinition topic)
        {
            string markdown = Catalog.Read(topic);
            var document = MarkdownToFlowDocument(markdown);

            if (topic.Related?.Length > 0)
            {
                document.Blocks.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 10, 0, 8),
                    Background = Brush("DividerBrush", Color.FromRgb(70, 76, 88))
                }));
                document.Blocks.Add(new Paragraph(new Run("See also"))
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("HeaderText", Colors.White),
                    Margin = new Thickness(0, 10, 0, 4)
                });
                foreach (string id in topic.Related)
                {
                    var related = Catalog.Find(id);
                    if (related == null) continue;
                    var link = new Hyperlink(new Run(related.Title)) { Tag = related.Id };
                    link.Click += (_, _) => NavigateTo((string)link.Tag);
                    document.Blocks.Add(new Paragraph(link) { Margin = new Thickness(10, 1, 0, 1) });
                }
            }

            DocumentViewer.Document = document;
            Title = $"Help — {topic.Title}";
        }

        private FlowDocument MarkdownToFlowDocument(string text)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = Brush("PrimaryText", Color.FromRgb(220, 224, 230)),
                PagePadding = new Thickness(14),
                LineHeight = 21
            };

            var paragraphLines = new List<string>();
            List currentList = null;
            bool inCode = false;
            StringBuilder code = new();

            void FlushParagraph()
            {
                if (paragraphLines.Count == 0) return;
                string ptext = string.Join(" ", paragraphLines).Trim();
                if (ptext.Length > 0)
                    doc.Blocks.Add(new Paragraph(InlineText(ptext)) { Margin = new Thickness(0, 3, 0, 8) });
                paragraphLines.Clear();
            }
            void FlushList()
            {
                if (currentList == null) return;
                doc.Blocks.Add(currentList);
                currentList = null;
            }
            void FlushCode()
            {
                if (code.Length == 0) return;
                doc.Blocks.Add(new Paragraph(new Run(code.ToString().TrimEnd()))
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    Background = Brush("InputBackground", Color.FromRgb(31, 34, 40)),
                    Padding = new Thickness(9),
                    Margin = new Thickness(0, 4, 0, 10)
                });
                code.Clear();
            }

            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.TrimStart().StartsWith("```"))
                {
                    FlushParagraph(); FlushList();
                    if (inCode) FlushCode();
                    inCode = !inCode;
                    continue;
                }
                if (inCode) { code.AppendLine(raw); continue; }

                string trimmed = line.Trim();
                if (trimmed.Length == 0) { FlushParagraph(); FlushList(); continue; }

                int heading = 0;
                while (heading < trimmed.Length && heading < 3 && trimmed[heading] == '#') heading++;
                if (heading > 0 && heading < trimmed.Length && trimmed[heading] == ' ')
                {
                    FlushParagraph(); FlushList();
                    string headingText = trimmed[(heading + 1)..];
                    doc.Blocks.Add(new Paragraph(InlineText(headingText))
                    {
                        FontWeight = FontWeights.SemiBold,
                        FontSize = heading == 1 ? 24 : heading == 2 ? 19 : 16,
                        Foreground = Brush(heading == 1 ? "HeaderText" : "SequenceAccent", Colors.White),
                        Margin = new Thickness(0, heading == 1 ? 0 : 10, 0, 7)
                    });
                    continue;
                }

                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    FlushParagraph();
                    currentList ??= new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(22, 2, 0, 8) };
                    currentList.ListItems.Add(new ListItem(new Paragraph(InlineText(trimmed[2..])) { Margin = new Thickness(0, 1, 0, 1) }));
                    continue;
                }

                paragraphLines.Add(trimmed);
            }
            FlushParagraph(); FlushList(); if (inCode || code.Length > 0) FlushCode();
            return doc;
        }

        private Inline InlineText(string text)
        {
            // Compact inline parser for `code` and **bold** used by the bundled help.
            var span = new Span();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        var codeSpan = new Span
                        {
                            FontFamily = new FontFamily("Consolas"),
                            Background = Brush("InputBackground", Color.FromRgb(31, 34, 40))
                        };
                        AddHighlightedText(codeSpan, text[(i + 1)..end]);
                        span.Inlines.Add(codeSpan);
                        i = end + 1; continue;
                    }
                }
                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
                {
                    int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        var bold = new Bold();
                        AddHighlightedText(bold, text[(i + 2)..end]);
                        span.Inlines.Add(bold);
                        i = end + 2; continue;
                    }
                }
                int nextCode = text.IndexOf('`', i);
                int nextBold = text.IndexOf("**", i, StringComparison.Ordinal);
                int next = new[] { nextCode, nextBold }.Where(x => x >= 0).DefaultIfEmpty(text.Length).Min();
                if (next <= i)
                {
                    AddHighlightedText(span, text[i].ToString());
                    i++;
                    continue;
                }
                AddHighlightedText(span, text[i..next]);
                i = next;
            }
            return span;
        }

        private void AddHighlightedText(Span container, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] terms = (SearchBox?.Text ?? "")
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length)
                .ToArray();
            if (terms.Length == 0)
            {
                container.Inlines.Add(new Run(text));
                return;
            }

            int pos = 0;
            while (pos < text.Length)
            {
                int bestIndex = -1;
                string bestTerm = null;
                foreach (string term in terms)
                {
                    int idx = text.IndexOf(term, pos, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0 && (bestIndex < 0 || idx < bestIndex || (idx == bestIndex && term.Length > bestTerm.Length)))
                    {
                        bestIndex = idx;
                        bestTerm = term;
                    }
                }

                if (bestIndex < 0)
                {
                    container.Inlines.Add(new Run(text[pos..]));
                    break;
                }
                if (bestIndex > pos)
                    container.Inlines.Add(new Run(text[pos..bestIndex]));

                container.Inlines.Add(new Run(text.Substring(bestIndex, bestTerm.Length))
                {
                    Background = Brush("SelectionBackground", Color.FromRgb(76, 98, 128)),
                    Foreground = Brushes.White
                });
                pos = bestIndex + bestTerm.Length;
            }
        }

        private Brush Brush(string resourceKey, Color fallback)
        {
            return TryFindResource(resourceKey) as Brush ?? new SolidColorBrush(fallback);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
