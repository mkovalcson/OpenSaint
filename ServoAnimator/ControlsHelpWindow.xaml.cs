using System.Windows;

namespace ServoAnimator
{
    public partial class ControlsHelpWindow : Window
    {
        public ControlsHelpWindow()
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "controls-hotkeys");
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
