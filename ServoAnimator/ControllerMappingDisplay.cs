using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ServoAnimator;

/// <summary>Read-only view of the bank actually selected on the controller.</summary>
public sealed class ControllerMappingDisplay : DockPanel
{
    private readonly Viewbox _artwork = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScrollViewer _scroll;
    internal ControllerDiagram Diagram { get; private set; }
    internal string LayerText { get; private set; }
    public ControllerMappingDisplay()
    {
        SetResourceReference(BackgroundProperty, "PanelBackground");
        _scroll = new() { Content = _artwork, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Children.Add(_scroll);
        _scroll.SizeChanged += (_, _) => ResizeArtwork();
    }
    private void ResizeArtwork()
    {
        if (Diagram != null) _artwork.Width = Math.Max(Diagram.Width * .8, _scroll.ActualWidth - 20);
    }
    internal void ShowMapping(ControllerProfile profile, ControllerSample sample)
    {
        if (Diagram?.Kind != profile.Kind)
        {
            Diagram = new(profile.Kind) { ReadOnly = true, Focusable = false };
            _artwork.Child = Diagram; ResizeArtwork();
        }
        int layer = profile.ActiveLayer(sample);
        LayerText = profile.NoMux ? "No MUX · Shoulder buttons are mapped" : ControllerCatalog.LayerNames[layer];
        if (!ReferenceEquals(Diagram.Mappings, profile.MappingForLayer(layer)) || Diagram.MappingLayer != layer || Diagram.NoMux != profile.NoMux)
        {
            Diagram.NoMux = profile.NoMux; Diagram.MappingLayer = layer;
            Diagram.LiveLayer = profile.NoMux ? -1 : layer;
            Diagram.Mappings = profile.MappingForLayer(layer); Diagram.InvalidateVisual();
        }
        Diagram.SetLiveInput(sample);
    }
}
