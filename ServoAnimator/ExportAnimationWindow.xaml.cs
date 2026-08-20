// ---------------------------------------------------------------------------
// ExportAnimationWindow.xaml.cs
//
// The Export Animation JSON dialog: the animate-mode picklist, the spline
// sample-rate picklist, and the Scale ±1 checkbox (all moved here from the
// spline area) alongside the destination file picker. The three options
// still persist with the sequence: the caller seeds them from the document
// and writes the choices back on Export.
// ---------------------------------------------------------------------------

using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ServoAnimator
{
    public partial class ExportAnimationWindow : Window
    {
        private readonly int[] _hzOptions;

        /// <summary>True = "Animate individual" (expand ganged commands).</summary>
        public bool AnimateIndividual =>
            ModeCombo.SelectedIndex != 0;

        /// <summary>Chosen spline sample rate in Hz.</summary>
        public int SampleHz =>
            HzCombo.SelectedIndex >= 0 ? _hzOptions[HzCombo.SelectedIndex]
                                       : _hzOptions[0];

        /// <summary>Scale numeric values to ±1.000 / 0..1.000 on export.</summary>
        public bool ScaleValues => ScaleCheck.IsChecked == true;

        /// <summary>The chosen output path (valid when DialogResult is true).</summary>
        public string FilePath => PathBox.Text?.Trim() ?? "";

        public ExportAnimationWindow(int[] hzOptions, bool animateIndividual,
                                     int sampleHz, bool scaleValues,
                                     string defaultPath)
        {
            InitializeComponent();
            HelpSystem.EnableContextHelp(this, "files-configuration");
            _hzOptions = hzOptions;

            ModeCombo.Items.Add("Animate ganged");
            ModeCombo.Items.Add("Animate individual");
            ModeCombo.SelectedIndex = animateIndividual ? 1 : 0;

            foreach (int hz in hzOptions) HzCombo.Items.Add(hz + " Hz");
            int idx = Array.IndexOf(hzOptions, sampleHz);
            HzCombo.SelectedIndex = idx >= 0 ? idx : 0;

            ScaleCheck.IsChecked = scaleValues;
            PathBox.Text = defaultPath ?? "";
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export animation JSON (with spline samples)",
                Filter = "JSON files (*.json)|*.json",
                FileName = Path.GetFileName(FilePath),
                InitialDirectory = SafeDirectory(FilePath),
            };
            if (dlg.ShowDialog() == true)
                PathBox.Text = dlg.FileName;
        }

        private static string SafeDirectory(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }
            catch { }
            return "";
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                MessageBox.Show(this, "Choose an output file first.",
                                "Export Animation JSON", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }
    }
}
