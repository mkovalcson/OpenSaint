using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    /// <summary>A modal loading indicator keeps rendering and the dispatcher
    /// responsive while workers prepare data. The owner cannot edit a document
    /// midway through its replacement. Cache hits do not open a window.</summary>
    internal static class LoadingWindow
    {
        public static T Wait<T>(Window owner, Task<T> work, string message)
        {
            if (work.IsCompleted) return work.GetAwaiter().GetResult();
            bool finished = false;
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 14) });
            panel.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 8 });
            var window = new Window
            {
                Owner = owner, Title = "Loading", Content = panel, Width = 380,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            window.Closing += (_, e) => e.Cancel = !finished;
            window.Loaded += async (_, _) =>
            {
                try { await work; } catch { /* Rethrown on the caller's normal error path. */ }
                finally { finished = true; window.Close(); }
            };
            window.ShowDialog();
            return work.GetAwaiter().GetResult();
        }
    }
}
