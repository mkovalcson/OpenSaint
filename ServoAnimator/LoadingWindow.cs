using System.Windows;
using System.Windows.Controls;

namespace ServoAnimator
{
    /// <summary>A modal loading indicator keeps rendering and the dispatcher
    /// responsive while workers prepare data. The owner cannot edit a document
    /// midway through its replacement. Cache hits do not open a window.</summary>
    internal static class LoadingWindow
    {
        /// <summary>Start UI capture only after a modal busy indicator has rendered;
        /// the operation can offload encoding and file I/O while its animation continues.</summary>
        public static void Run(Window owner, Func<Task> work, string message)
        {
            bool finished = false;
            Exception error = null;
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 14) });
            panel.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 10 });
            var window = new Window
            {
                Owner = owner, Title = "Saving", Content = panel, Width = 400,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
            };
            window.Closing += (_, e) => e.Cancel = !finished;
            bool started = false;
            window.ContentRendered += async (_, _) =>
            {
                if (started) return;
                started = true;
                try
                {
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    await work();
                }
                catch (Exception ex) { error = ex; }
                finally { finished = true; window.Close(); }
            };
            window.ShowDialog();
            if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

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
