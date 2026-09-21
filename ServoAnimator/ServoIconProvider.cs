using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ServoAnimator;

/// <summary>Loads the compact ServoNames icon set once for picklists and command rows.</summary>
internal static class ServoIconProvider
{
    private static readonly IReadOnlyDictionary<ServoNames, ImageSource> Icons = BuildIcons();

    public static ImageSource For(ServoNames servo) =>
        Icons.TryGetValue(servo, out var icon) ? icon : null;

    private static IReadOnlyDictionary<ServoNames, ImageSource> BuildIcons()
    {
        var files = new Dictionary<ServoNames, string>
        {
            [ServoNames.NeckTurn] = "neck-turn.png",
            [ServoNames.NeckNodUp] = "neck-nod-up.png",
            [ServoNames.NeckTiltRight] = "neck-tilt-right.png",
            [ServoNames.FlapsOpen] = "flaps-open.png",
            [ServoNames.FlapTiltUp] = "flap-tilt-up.png",
            [ServoNames.IrisClose] = "iris-close.png",
            [ServoNames.EyesVerticalUp] = "eyes-vertical-up.png",
            [ServoNames.EyesHorizontalRight] = "eyes-horizontal-right.png",
            [ServoNames.VentsOpen] = "vents-open.png",
            [ServoNames.NoseBody] = "nose-body.png",
            [ServoNames.NoseBasket] = "nose-basket.png",
            [ServoNames.MFR_UpDown] = "mfr-up-down.png",
            [ServoNames.MFR_Rotate] = "mfr-rotate.png",
            [ServoNames.Microphone_RaiseLower] = "microphone-raise-lower.png",
            [ServoNames.Whip_Antenna_RaiseLower] = "whip-antenna-raise-lower.png",
            [ServoNames.Whip_Antenna_Rotate] = "whip-antenna-rotate.png",
            [ServoNames.LeftEyePop] = "left-eye-pop.png",
            [ServoNames.RightEyePop] = "right-eye-pop.png",
            [ServoNames.BothEyePop] = "both-eye-pop.png",
            [ServoNames.RGBCommand] = "rgb-command.png",
        };

        return files.ToDictionary(pair => pair.Key, pair => Load(pair.Value));
    }

    private static ImageSource Load(string fileName)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(
            $"pack://application:,,,/AnimationEditorPlayer;component/Assets/ControlIcons/{fileName}",
            UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
