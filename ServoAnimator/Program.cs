// ---------------------------------------------------------------------------
// Program.cs
//
// Explicit application entry point. Normally WPF auto-generates Main from
// App.xaml (when its build action is "ApplicationDefinition"), but that
// detection can fail depending on how the project is imported - producing
// "Program does not contain a static 'Main' method suitable for an entry
// point". Defining Main explicitly (and pointing <StartupObject> at it in
// the .csproj) makes the entry point unambiguous in every configuration.
//
// [STAThread] is required: WPF must run on a single-threaded-apartment
// thread.
// ---------------------------------------------------------------------------

namespace ServoAnimator
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.InitializeComponent();   // loads App.xaml (StartupUri -> MainWindow)
            app.Run();
        }
    }
}
