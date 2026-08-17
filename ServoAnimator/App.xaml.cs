using System.Windows;

namespace ServoAnimator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // App.xaml resources are loaded by Program.InitializeComponent()
            // before Run() reaches this point, so the saved palette can be
            // applied before StartupUri creates the main window.
            ThemeManager.LoadAndApply();
            base.OnStartup(e);
        }
    }
}
