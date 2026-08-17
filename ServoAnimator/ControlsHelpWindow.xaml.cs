using System.Windows;

namespace ServoAnimator
{
    public partial class ControlsHelpWindow : Window
    {
        public ControlsHelpWindow() => InitializeComponent();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
