using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ServoAnimator
{
    public partial class MainWindow
    {
        private AnimationDocument _markerSelectionDocument;
        private IEnumerable<ServoCommand> VisibleCommands => _doc.Commands.Where(c => !_doc.HiddenCommandGroups.Contains(c.GroupId));
        private void EnsureCommandGroupNumbers()
        {
            _doc.CommandGroupNumbers ??= new();
            int next = _doc.CommandGroupNumbers.Values.DefaultIfEmpty(0).Max() + 1;
            foreach (string id in _doc.Commands.Select(c => c.GroupId).Where(id => !string.IsNullOrEmpty(id)).Distinct())
                if (!_doc.CommandGroupNumbers.ContainsKey(id)) _doc.CommandGroupNumbers[id] = next++;
        }
        internal static SkiaSharp.SKColor CommandGroupColor(int number)
            => SkiaSharp.SKColor.FromHsv((float)((157 + (number - 1) * 137.507764) % 360), 78, 84);
        private void RefreshHiddenGroupButtons()
        {
            HiddenGroupButtons.Children.Clear();
            foreach (var id in _doc.HiddenCommandGroups.Where(id => _doc.Commands.Any(c => c.GroupId == id))
                .OrderBy(id => _doc.CommandGroupNumbers[id]))
            {
                int number = _doc.CommandGroupNumbers[id];
                var color = CommandGroupColor(number);
                var button = new Button { Content = $"Show Group {number}", Margin = new Thickness(5, 0, 0, 0),
                    Padding = new Thickness(9, 5, 9, 5), Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)),
                    Foreground = Brushes.Black };
                button.Click += (_, _) => { if (IsRunning) PausePlayback(); _doc.HiddenCommandGroups.Remove(id); RefreshGroupVisibility(); };
                HiddenGroupButtons.Children.Add(button);
            }
        }
        private void RefreshGroupVisibility()
        {
            Waveform.SetMarkerSelection(Array.Empty<double>());
            _rgbSimulator.Invalidate();
            ClearCollisionCommandWarnings();
            RebuildSplineData(); RefreshMarkers(); RefreshAudioClips(); UpdateCommandsAtPointList();
        }
        private bool _showModifiedControls;
        private bool _updatingModifiedControls;

        private void InitializeCommandGroups()
        {
            Waveform.SelectedMarkersMoved += MoveSelectedCommandMarkers;
            Waveform.MarkerSelectionChanged += () =>
            {
                if (!_showModifiedControls) return;
                if (Waveform.SelectedMarkers.Count == 0)
                {
                    _showModifiedControls = false;
                    Waveform.HighlightedControlMarkers.Clear();
                    Waveform.InvalidateVisual();
                }
                UpdateCommandsAtPointList();
            };
            PreviewMouseDown += (_, e) =>
            {
                if (!IsWaveformMouseSource(e.OriginalSource as DependencyObject))
                    Waveform.SetMarkerSelection(Array.Empty<double>());
            };
        }

        private bool IsWaveformMouseSource(DependencyObject source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, Waveform)) return true;
                // Inspecting this temporary list must not end the selection it describes.
                if (_showModifiedControls && ReferenceEquals(source, CommandsAtPointList)) return true;
                if (Spline.CombinedMode && ReferenceEquals(source, AudioPlotHost)) return true;
                if (source is ContextMenu menu && ReferenceEquals(menu.PlacementTarget, Waveform)) return true;
                source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return false;
        }

        private void SyncCommandGroupSelection()
        {
            EnsureCommandGroupNumbers();
            Waveform.GroupColors = VisibleCommands.Where(c => !string.IsNullOrEmpty(c.GroupId))
                .GroupBy(c => ServoCommand.TimeKey(c.OffsetSeconds)).ToDictionary(g => g.Key,
                    g => CommandGroupColor(_doc.CommandGroupNumbers[g.First().GroupId]));
            RefreshHiddenGroupButtons();
            Waveform.CommandGroups = VisibleCommands.Where(c => !string.IsNullOrEmpty(c.GroupId)).GroupBy(c => c.GroupId)
                .Select(g => g.Select(c => ServoCommand.TimeKey(c.OffsetSeconds)).Distinct().ToArray()).ToArray();
            var keys = ReferenceEquals(_markerSelectionDocument, _doc)
                ? Waveform.SelectedMarkers.Intersect(Waveform.Markers).ToArray() : Array.Empty<double>();
            _markerSelectionDocument = _doc;
            Waveform.SetMarkerSelection(keys);
        }

        private List<ServoCommand> SelectedGroupCommands()
        {
            var keys = Waveform.SelectedMarkers.ToHashSet();
            return VisibleCommands.Where(c => keys.Contains(ServoCommand.TimeKey(c.OffsetSeconds))).ToList();
        }

        private void ShowSelectedCommandMenu()
        {
            var menu = new ContextMenu { PlacementTarget = Waveform };
            void Item(string label, Action action)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, _) => action();
                menu.Items.Add(item);
            }
            Item("Uniform offset…", UniformSelectedCommands);
            bool grouped = SelectedGroupCommands().Any(c => !string.IsNullOrEmpty(c.GroupId));
            if (grouped)
            {
                EnsureCommandGroupNumbers();
                menu.Items.Add(new MenuItem { Header = string.Join(", ", SelectedGroupCommands()
                    .Where(c => !string.IsNullOrEmpty(c.GroupId)).Select(c => _doc.CommandGroupNumbers[c.GroupId])
                    .Distinct().OrderBy(n => n).Select(n => $"Group {n}")), IsEnabled = false });
            }
            Item(grouped ? "Ungroup Commands" : "Group Commands", () => SetPersistentCommandGroup(!grouped));
            if (grouped) Item("Hide Group", () =>
            {
                if (!PrepareGroupEdit()) return;
                foreach (var id in SelectedGroupCommands().Select(c => c.GroupId).Where(id => !string.IsNullOrEmpty(id)))
                    _doc.HiddenCommandGroups.Add(id);
                RefreshGroupVisibility();
            });
            Item("Repeat…", RepeatSelectedCommands);
            Item("Show all modified controls", () =>
            {
                _showModifiedControls = true;
                UpdateCommandsAtPointList();
            });
            Item("Copy", () =>
            {
                _clipboard.Clear();
                _clipboard.AddRange(SelectedGroupCommands().Select(c => c.Clone()));
                ShowStatus($"Copied {_clipboard.Count} commands. Click a timeline position, then right-click and Paste.");
            });
            Item("Delete", () =>
            {
                if (!PrepareGroupEdit()) return;
                var selected = SelectedGroupCommands();
                ApplySelectedGroupEdit($"Delete {selected.Count} selected commands", () =>
                    _doc.Commands.RemoveAll(selected.Contains), Array.Empty<double>());
            });
            if (_clipboard.Count > 0)
            {
                menu.Items.Add(new Separator());
                Item($"Paste {_clipboard.Count} copied commands at {_cursorTime:F3} s", PasteClipboardAtCursor);
            }
            menu.IsOpen = true;
        }

        private void UpdateModifiedControlsList()
        {
            var selected = CommandsAtPointList.SelectedItem as ModifiedCommandControl;
            _updatingModifiedControls = true;
            try
            {
                CommandsAtPointList.Items.Clear();
                foreach (var control in ModifiedCommandControls.From(SelectedGroupCommands()))
                {
                    var commands = SelectedGroupCommands().Where(c => c.Servo == control.Servo && c.Control == control.Control &&
                        SplineBreakOperations.SupportsSpline(c) && !c.Disable && SplineServosEnabled().Contains(c.Servo)).ToArray();
                    control.CanSetBreaks = commands.Length > 0;
                    control.CanClearBreaks = commands.Any(c => c.BreakSpline);
                    CommandsAtPointList.Items.Add(control);
                }
                CommandsAtPointHeader.Text = $"Modified controls · {Waveform.SelectedMarkers.Count} selected times";
                CommandsAtPointHeader.ToolTip = $"{CommandsAtPointList.Items.Count} distinct controls in the selected command triangles";
                if (selected != null)
                    CommandsAtPointList.SelectedItem = CommandsAtPointList.Items.OfType<ModifiedCommandControl>()
                        .FirstOrDefault(c => c.Servo == selected.Servo && c.Control == selected.Control);
            }
            finally { _updatingModifiedControls = false; }
            CommandsAtPointList.ToolTip = "Select a control to highlight every command triangle that includes it in purple.";
            HighlightModifiedControl();
        }

        private void CommandsAtPointList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_showModifiedControls && !_updatingModifiedControls) HighlightModifiedControl();
        }

        private void HighlightModifiedControl()
        {
            Waveform.HighlightedControlMarkers.Clear();
            if (CommandsAtPointList.SelectedItem is ModifiedCommandControl control)
                Waveform.HighlightedControlMarkers.UnionWith(_doc.Commands.Where(control.Includes)
                    .Select(c => ServoCommand.TimeKey(c.OffsetSeconds)));
            Waveform.InvalidateVisual();
        }

        private bool PrepareGroupEdit()
        {
            if (!ApplyOpenCommandEditor()) return false;
            if (IsRunning) PausePlayback();
            return Waveform.SelectedMarkers.Count > 0 && SelectedGroupCommands().Count > 0;
        }

        private void SetPersistentCommandGroup(bool group)
        {
            if (!PrepareGroupEdit()) return;
            var selected = SelectedGroupCommands();
            var ids = selected.Where(c => !string.IsNullOrEmpty(c.GroupId)).Select(c => c.GroupId).ToHashSet();
            string id = group ? Guid.NewGuid().ToString("N") : "";
            ApplySelectedGroupEdit(group ? "Group Commands" : "Ungroup Commands", () =>
            {
                foreach (var c in _doc.Commands.Where(c => selected.Contains(c) || ids.Contains(c.GroupId))) c.GroupId = id;
            }, Array.Empty<double>());
        }

        private void DeleteSelectedCommandGroup()
        {
            if (!PrepareGroupEdit()) return;
            var selected = SelectedGroupCommands();
            ApplySelectedGroupEdit($"Delete {selected.Count} selected commands", () => _doc.Commands.RemoveAll(selected.Contains), Array.Empty<double>());
        }

        private void ApplySelectedGroupEdit(string description, Action edit, IEnumerable<double> resultingSelection)
        {
            var previous = Waveform.SelectedMarkers.ToArray();
            var result = resultingSelection.ToArray();
            PushUndo(description);
            edit();
            bool accepted = RefreshAfterEdit();
            Waveform.SetMarkerSelection((accepted ? result : previous).Intersect(Waveform.Markers));
            if (accepted) ShowStatus(description);
        }

        private void MoveSelectedCommandMarkers(double offset)
        {
            if (!PrepareGroupEdit()) return;
            var selected = SelectedGroupCommands();
            double minimum = selected.Min(c => c.OffsetSeconds);
            offset = Math.Max(-minimum, offset);
            var times = selected.Select(c => ServoCommand.TimeKey(c.OffsetSeconds + offset)).Distinct().ToArray();
            ApplySelectedGroupEdit($"Move {selected.Count} selected commands by {offset:0.###} s", () =>
            {
                _doc.Commands.RemoveAll(selected.Contains);
                foreach (var command in selected) command.OffsetSeconds = ServoCommand.TimeKey(command.OffsetSeconds + offset);
                _doc.Commands.AddRange(selected); // Conflicts list existing values before the newly moved values.
            }, times);
        }

        private void UniformSelectedCommands()
        {
            if (!PrepareGroupEdit()) return;
            var times = Waveform.SelectedMarkers.ToArray();
            var dialog = new CommandGroupDialog(this, repeat: false, CommandGroupOperations.MeanSpacing(times));
            if (dialog.ShowDialog() != true) return;
            var map = CommandGroupOperations.UniformTimes(times, dialog.Offset);
            if (map.Values.Any(t => !double.IsFinite(t)))
            {
                MessageBox.Show(this, "The resulting command time is too large.", "Uniform offset");
                return;
            }
            var selected = SelectedGroupCommands();
            ApplySelectedGroupEdit($"Uniform offset: {dialog.Offset:0.######} s", () =>
            {
                _doc.Commands.RemoveAll(selected.Contains);
                foreach (var command in selected) command.OffsetSeconds = map[ServoCommand.TimeKey(command.OffsetSeconds)];
                _doc.Commands.AddRange(selected);
            }, map.Values);
        }

        private void RepeatSelectedCommands()
        {
            if (!PrepareGroupEdit()) return;
            var dialog = new CommandGroupDialog(this, repeat: true, 0);
            if (dialog.ShowDialog() != true) return;
            try
            {
                var copies = CommandGroupOperations.Repeat(SelectedGroupCommands(), dialog.Repetitions, dialog.Offset);
                ApplySelectedGroupEdit($"Repeat selected commands {dialog.Repetitions} times", () => _doc.Commands.AddRange(copies), Waveform.SelectedMarkers);
            }
            catch (ArgumentException error) { MessageBox.Show(this, error.Message, "Repeat commands", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
    }
}
