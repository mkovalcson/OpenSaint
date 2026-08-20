using System;
using System.ComponentModel;
using System.Windows;

namespace ServoAnimator
{
    public partial class RobotHeadWindow : Window
    {
        public bool ForceClose { get; set; }
        public event Action DockRequested;

        public RobotHeadWindow()
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "urdf-viewer");
            HelpSystem.SetTopic(HeadView, "urdf-viewer");
            HeadView.DockToggleRequested += () => DockRequested?.Invoke();
            HeadView.SetDetachedHostState();
        }

        public Rect GetNormalBounds()
        {
            return WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;
        }

        public void ApplyNormalBounds(Rect bounds)
        {
            if (bounds.IsEmpty || bounds.Width < MinWidth || bounds.Height < MinHeight)
                return;

            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!ForceClose)
            {
                // Closing the detached window returns the URDF to the editor.
                e.Cancel = true;
                DockRequested?.Invoke();
            }
        }
    }
}
