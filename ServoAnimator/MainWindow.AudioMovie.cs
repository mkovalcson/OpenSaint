using System.Windows;

namespace ServoAnimator
{
    public partial class MainWindow
    {
        private void CreateMovieFromAudioFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateAudioMovieWindow(this, ProjectsFolder());
            if (dialog.ShowDialog() != true) return;
            if (!RequireConfigPath(dialog.ProjectFolder, "project folder")) return;
            if (!ConfirmDocumentSwitch(replaceMovie: true)) return;
            try
            {
                string root = ConfigRoot;
                string path = LoadingWindow.Wait(this,
                    Task.Run(() => AudioMovieCreator.Create(root, dialog.ProjectFolder, dialog.MovieName)),
                    "Creating movie from audio files…");
                if (LoadMovieFromPath(path, alreadyConfirmed: true))
                {
                    MovieTimeline.ZoomToFit();
                    ShowStatus($"Created movie with {_movieItems.Count} audio sequences: {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Create Movie from Audio Files",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
