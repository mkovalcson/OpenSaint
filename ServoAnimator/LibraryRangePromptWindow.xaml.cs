// ---------------------------------------------------------------------------
// LibraryRangePromptWindow.xaml.cs
//
// Modeless movable Create Library Sequence prompt. Because it is modeless, the
// timeline arrows remain draggable while the ten-line description is shown.
// ---------------------------------------------------------------------------

using System.Windows;
using System.Windows.Input;

namespace ServoAnimator
{
    public partial class LibraryRangePromptWindow : Window
    {
        private readonly Action<string> _accepted;
        private readonly Action _cancelled;
        private bool _wasAccepted;

        public LibraryRangePromptWindow(string description,
                                        Action<string> accepted,
                                        Action cancelled)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "animation-library");
            DescriptionBox.Text = description ?? "";
            _accepted = accepted;
            _cancelled = cancelled;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _wasAccepted = true;
            string description = DescriptionBox.Text ?? "";
            Close();
            _accepted?.Invoke(description);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!_wasAccepted)
                _cancelled?.Invoke();
        }
    }
}
