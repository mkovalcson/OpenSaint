using System.Windows;
using System.Windows.Media;

namespace ServoAnimator;

public partial class MainWindow
{
    private readonly ControlConnectionState _streamDeckConnection = new();
    private readonly Dictionary<string, long> _streamDeckClients = new();
    private bool _streamDeckOwnsOutput, _streamDeckPickerPending;

    private object StreamDeckPresence(EditorApiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StreamDeckSession) || request.StreamDeckSession.Length > 128 || !request.Enabled.HasValue)
            throw new InvalidOperationException("Supply streamDeckSession and enabled for device presence.");
        RefreshStreamDeckConnection();
        if (request.Enabled == true)
        {
            if (_streamDeckClients.Count >= 16 && !_streamDeckClients.ContainsKey(request.StreamDeckSession))
                throw new InvalidOperationException("Too many Stream Deck sessions.");
            _streamDeckClients[request.StreamDeckSession] = Environment.TickCount64;
        }
        else _streamDeckClients.Remove(request.StreamDeckSession);
        RefreshStreamDeckConnection();
        return new { ok = true, state = EditorApiStatus() };
    }

    private void RefreshStreamDeckConnection()
    {
        long now = Environment.TickCount64;
        foreach (string session in _streamDeckClients.Where(p => now - p.Value > 5000).Select(p => p.Key).ToArray()) _streamDeckClients.Remove(session);
        _streamDeckConnection.Connected = EditorApiEnabled.IsChecked == true && _streamDeckClients.Count > 0;
        if (!_streamDeckConnection.Enabled) StopStreamDeckControl();
        StreamDeckButton.IsChecked = _streamDeckConnection.Enabled;
        StreamDeckDot.Fill = _streamDeckConnection.Connected ? Brushes.LimeGreen : Brushes.IndianRed;
        string status = _streamDeckConnection.Connected ? "Stream Deck connected" : "Stream Deck not connected — open the Stream Deck app with the updated Animation Editor plugin";
        StreamDeckDot.ToolTip = status;
        StreamDeckButton.ToolTip = status + (_streamDeckConnection.ManuallyDisabled ? "\nControl disabled. Click to enable." : "\nEnables automatically when connected. Click to disable control.")
            + "\nBackground destinations follow Focus Control. Physical output requires Drive HW.";
    }

    private void StopStreamDeckControl()
    {
        if (_streamDeckPickerPending) { _streamDeckPickerPending = false; CancelApiLibraryPicker(); }
        if (_streamDeckOwnsOutput && _apiLibraryOwnsOutput)
        { StopControllerLibrary(); Interlocked.Increment(ref _controllerOutputEpoch); _controllerPendingHardware.Clear(); RefreshControllerOutputPermissions(); }
    }

    private void StreamDeckEnable_Click(object sender, RoutedEventArgs e)
    {
        _streamDeckConnection.SetEnabled(StreamDeckButton.IsChecked == true);
        RefreshStreamDeckConnection();
        ShowStatus(_streamDeckConnection.ManuallyDisabled ? "Stream Deck control disabled" : _streamDeckConnection.Connected ? "Stream Deck control enabled" : "Stream Deck will enable when connected");
    }

    private void EnsureStreamDeckControl(EditorApiRequest request)
    {
        if (string.IsNullOrEmpty(request.StreamDeckSession)) return;
        RefreshStreamDeckConnection();
        if (!_streamDeckConnection.Enabled || !_streamDeckClients.ContainsKey(request.StreamDeckSession))
            throw new InvalidOperationException("Stream Deck control is disabled or disconnected. Enable its button in the Hardware section.");
    }
}
