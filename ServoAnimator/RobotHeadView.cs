// ---------------------------------------------------------------------------
// RobotHeadView.cs
//
// Native WPF 3-D URDF preview for ServoAnimator.  The view loads the
// primitive geometry and joint tree from Models/johnny5_head.urdf, then maps
// the application's existing ServoNames/RobotControls values onto the URDF
// joints.  NeckTurn, NeckNodUp and NeckTiltRight form a yaw/pitch/roll chain,
// so the complete head assembly moves rigidly in 3-D rather than growing the
// old 2-D side/top/bottom rectangles.
// ---------------------------------------------------------------------------

using System.Globalization;
using System.IO;
using Path = System.IO.Path;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ServoAnimator
{
    /// <summary>Logical values currently represented by the URDF pose editor.
    /// Left/Right eye and flap values use the robot's physical left/right names,
    /// matching RobotControls rather than screen-side mirroring.</summary>
    public sealed class RobotPoseSnapshot
    {
        public bool LRJoined { get; set; } = true;
        public double LeftEyeHorizontal { get; set; }
        public double RightEyeHorizontal { get; set; }
        public double LeftEyeVertical { get; set; }
        public double RightEyeVertical { get; set; }
        public double LeftIris { get; set; }
        public double RightIris { get; set; }
        public double LeftTopFlapOpen { get; set; }
        public double RightTopFlapOpen { get; set; }
        public double LeftBottomFlapOpen { get; set; }
        public double RightBottomFlapOpen { get; set; }
        public double LeftTopFlapTilt { get; set; }
        public double RightTopFlapTilt { get; set; }
        public double LeftVent { get; set; }
        public double RightVent { get; set; }
        public ServoNames? NeckOwner { get; set; }
        public double NeckNod { get; set; }
        public double NeckTilt { get; set; }
        public double NeckTurn { get; set; }
        public double NoseBody { get; set; }
        public double NoseBasket { get; set; }
        public double LeftEyePop { get; set; }
        public double RightEyePop { get; set; }
        public double WhipRaiseLower { get; set; }
        public double WhipRotate { get; set; }
        public double MfrUpDown { get; set; }
        public double MfrRotate { get; set; }
        public double MicrophoneRaiseLower { get; set; }
        public string RgbCommand { get; set; } = "";

        public RobotPoseSnapshot Clone() => (RobotPoseSnapshot)MemberwiseClone();
    }

    public sealed partial class RobotHeadView : Grid
    {
        private const double Deg = Math.PI / 180.0;

        // Iris geometry (inches converted to metres for the URDF/WPF scene).
        // The blue iris is a true annulus. Its outer diameter is fixed while
        // IrisClose changes only the inner opening. The full-size backing
        // disc stays behind the annulus as a black light baffle. When the
        // front NeoPixel eye ring emits, it becomes a smoky translucent
        // diffuser with a soft blended emissive tint.
        private const double InchToMetres = 0.0254;
        private const double IrisOuterDiameterInches = 1.80;
        private const double IrisDefaultInnerDiameterInches = 0.90;
        private const double IrisMinimumInnerDiameterInches = 0.30;
        private const double IrisMinimumAperturePercent = 10.0;
        private const double IrisDefaultAperturePercent = 55.0;
        private const double IrisMaximumAperturePercent = 100.0;
        private const double IrisOuterRadiusMetres =
            IrisOuterDiameterInches * InchToMetres / 2.0;
        private const double IrisThicknessMetres = 0.003175;

        // Whip-antenna automatic folding geometry, measured from the supplied
        // colored STEP assemblies and the rendered SimplifiedHead2 head-top part.
        // All values are millimetres in the whip assembly/head local Z direction.
        // The ASME B18.8.2 pin center is 2.921 mm above the whip assembly origin;
        // the flat shoulder at the top of the lower linkage is at 0.000 mm.
        // The visible flat top around the whip opening is CAD Y=59.181999 mm,
        // which maps directly to URDF head Z.  The lift-joint origin itself is
        // already 1.764 mm above head_link Z=0.
        private const double WhipHeadTopHeightMm = 59.181999;
        private const double WhipLiftJointOriginHeightMm = 1.764;
        private const double WhipHingeLocalHeightMm = 2.921;
        private const double WhipLowerFlatLocalHeightMm = 0.000;
        private const double WhipFoldStartLiftMm =
            WhipHeadTopHeightMm - WhipLiftJointOriginHeightMm - WhipHingeLocalHeightMm;
        private const double WhipFoldEndLiftMm =
            WhipHeadTopHeightMm - WhipLiftJointOriginHeightMm - WhipLowerFlatLocalHeightMm;

        private readonly Viewport3D _viewport = new();
        private readonly PerspectiveCamera _camera = new();
        private readonly TextBlock _status = new();
        private readonly TextBlock _fps = new() { Text = "fps: 0", IsHitTestVisible = false };
        private bool _fpsSubscribed;
        private TimeSpan _fpsLastFrame = TimeSpan.MinValue;
        private readonly System.Diagnostics.Stopwatch _fpsClock = new();
        private readonly System.Windows.Threading.DispatcherTimer _fpsTimer = new(System.Windows.Threading.DispatcherPriority.Render)
        { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly Queue<double> _fpsFrameTimes = new();
        private (long Pose, int Mouth, int Rgb, bool HasRgb) _fpsLastVisualState;
        private readonly StackPanel _bottomControls = new();
        private readonly Button _recenterButton = new();
        private readonly Button _cameraMinus90Button = new();
        private readonly Button _cameraPlus90Button = new();
        private readonly Button _driveToggleButton = new();
        private readonly Button _collisionToggleButton = new();
        private readonly Button _dockToggleButton = new();
        private readonly Grid _cameraRow = new();
        private readonly Thumb _verticalResizeHandle = new();

        // Pose editor overlay.  Controls are ordinary 2-D WPF chrome projected
        // onto meaningful URDF link locations, so they remain grab-able while
        // the underlying model continues to render as native WPF 3-D.
        private readonly Canvas _poseOverlay = new();
        private readonly Button _poseButton = new();
        private readonly Button _faceResetButton = new();
        private readonly Button _libraryPoseSaveButton = new();
        private readonly Button _libraryPoseLoadButton = new();
        private readonly Button _lrModeButton = new();
        private readonly TextBox _poseRgbCommandBox = new();
        private readonly Button _poseRgbBuildButton = new();
        private readonly Ellipse _leftEyeTargetCircle = new();
        private readonly Ellipse _rightEyeTargetCircle = new();
        private readonly Thumb _leftEyeGazeHandle = new();
        private readonly Thumb _rightEyeGazeHandle = new();
        private readonly Button _leftEyeGazeResetButton = new();
        private readonly Button _rightEyeGazeResetButton = new();
        private readonly Slider _joinedIrisSlider = new();
        private readonly Slider _leftIrisSlider = new();
        private readonly Slider _rightIrisSlider = new();
        private readonly Slider _joinedFlapOpenSlider = new();
        private readonly Slider _leftFlapOpenSlider = new();
        private readonly Slider _rightFlapOpenSlider = new();
        private readonly Slider _noseBodySlider = new();
        private readonly Slider _noseBasketSlider = new();
        private readonly Slider _neckNodSlider = new();
        private readonly Slider _neckTiltSlider = new();
        private readonly Slider _joinedEyePopSlider = new();
        private readonly Slider _leftEyePopSlider = new();
        private readonly Slider _rightEyePopSlider = new();
        private readonly Slider _whipRaiseLowerSlider = new();
        private readonly Slider _mfrUpDownSlider = new();
        private readonly Slider _microphoneRaiseLowerSlider = new();
        private readonly System.Windows.Shapes.Path _ventArcPath = new();
        private readonly Thumb _ventArcHandle = new();
        private Point _ventArcCenter;
        private double _ventArcRadius = 1.0;
        private readonly System.Windows.Shapes.Path _rightVentArcPath = new();
        private readonly Thumb _rightVentArcHandle = new();
        private Point _rightVentArcCenter;
        private double _rightVentArcRadius = 1.0;
        private readonly Canvas _neckTurnDial = new();
        private readonly Line _neckTurnDialPointer = new();
        private readonly Thumb _neckTurnDialHandle = new();
        private readonly TextBox _neckTurnDialEditor = new();
        private readonly Button _neckTurnDialResetButton = new();
        private readonly Canvas _whipRotateDial = new();
        private readonly Line _whipRotateDialPointer = new();
        private readonly Thumb _whipRotateDialHandle = new();
        private readonly TextBox _whipRotateDialEditor = new();
        private readonly Button _whipRotateDialResetButton = new();
        private readonly Canvas _mfrRotateDial = new();
        private readonly Line _mfrRotateDialPointer = new();
        private readonly Thumb _mfrRotateDialHandle = new();
        private readonly TextBox _mfrRotateDialEditor = new();
        private readonly Button _mfrRotateDialResetButton = new();
        private readonly Dictionary<string, Thumb> _poseThumbs = new(StringComparer.Ordinal);
        private readonly Dictionary<Slider, Button> _poseSliderResetButtons = new();
        private readonly Button _ventResetButton = new();
        private readonly Button _rightVentResetButton = new();
        private readonly RobotPoseSnapshot _pose = new();
        private bool _poseEditEnabled;
        private bool _lrJoined = true;
        private bool _poseInternalUpdate;
        private bool _updatingPoseUi;

        private bool _hostIsDocked = true;
        private bool _urdfDriveEnabled = true;
        private bool _collisionWarningsEnabled = true;
        private bool _suppressCollisionRefresh;
        private UrdfScene _scene;
        private UrdfConfiguration _urdfConfiguration = UrdfConfiguration.CreateDefault();
        private ServoConfiguration _servoConfiguration = ServoConfiguration.CreateDefault();

        // Playback pose cache: unchanged controls do not reassign WPF 3-D
        // transforms, rebuild iris meshes, or trigger downstream collision work.
        private readonly Dictionary<(ServoNames Servo, RobotControls Control), double>
            _lastControlValues = new();
        private long _poseRevision;
        private long _lastCollisionRefreshMs;
        private readonly System.Windows.Threading.DispatcherTimer _deferredCollisionTimer =
            new(System.Windows.Threading.DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(67),
            };
        private readonly double[] _mouthLevels = new double[14];
        private int _lastMouthStep = -1;
        private int _lastRgbFrameHash;
        private bool _hasRgbFrameHash;
        private double _lastLeftIrisRadius = double.NaN;
        private double _lastRightIrisRadius = double.NaN;
        private ServoNames? _lastAppliedNeckMode;
        private double _lastAppliedNeckLeft = double.NaN;
        private double _lastAppliedNeckRight = double.NaN;
        private double _lastAppliedNeckTurn = double.NaN;

        private Point _lastMouse;
        private bool _orbiting;
        private double _cameraYaw = 0;
        private double _cameraPitch = 0;
        private double _cameraDistance = 1.15;
        private double _openingCameraYaw;
        private double _openingCameraPitch;
        private double _openingCameraDistance = 1.15;
        private bool _openingCameraCaptured;
        // The camera continues to orbit the same vertical line as the physical
        // NeckTurn joint, but its target Z is calculated for each frame so the
        // physical neck base stays at a fixed screen-space height. This keeps
        // the model anchored 35 pixels above the bottom of the URDF viewport
        // during wheel zooming and docked/undocked viewport resizing.
        private static readonly Point3D NeckBaseScreenAnchor =
            new(0.123658047, 0.000000084, 0.131272970);
        private const double NeckBaseBottomAnchor = 35.0;
        private const double FallbackCameraTargetZ = 0.300;

        // Library Pose thumbnails intentionally frame the head, expression
        // mechanisms, and neck while excluding the tall accessory antennas and
        // microphone.  The current pose of every included link is respected.
        private static readonly string[] LibraryPoseThumbnailLinks =
        {
            "base_link", "neck_yaw_link", "neck_pitch_link", "head_offset_link", "head_link",
            "left_eye_pop_link", "left_eye_v_link", "left_eye_h_link",
            "left_pupil_link", "left_pupil_inner_link",
            "right_eye_pop_link", "right_eye_v_link", "right_eye_h_link",
            "right_pupil_link", "right_pupil_inner_link",
            "left_fabco_body_link", "left_fabco_piston_link",
            "left_bottom_ball_link", "left_top_ball_link",
            "right_fabco_body_link", "right_fabco_piston_link",
            "right_bottom_ball_link", "right_top_ball_link",
            "nose_body_link", "nose_basket_link",
            "left_top_tilt_link", "left_top_carrier_link", "left_top_flap_link",
            "right_top_tilt_link", "right_top_carrier_link", "right_top_flap_link",
            "left_bottom_flap_link", "right_bottom_flap_link",
            "left_eye_vent_link", "right_eye_vent_link",
            "left_eye_vent_fin1_link", "left_eye_vent_fin2_link",
            "left_eye_vent_fin3_link", "left_eye_vent_fin4_link", "left_eye_vent_fin5_link",
            "right_eye_vent_fin1_link", "right_eye_vent_fin2_link",
            "right_eye_vent_fin3_link", "right_eye_vent_fin4_link", "right_eye_vent_fin5_link",
        };

        // NeckNodUp and NeckTiltRight are two logical modes that take turns
        // owning the SAME physical NeckTiltLeft/NeckTiltRight actuator pair.
        // Keep one shared pair of logical child values; _activeNeckMode tells
        // ApplyNeckPose whether that pair currently represents Nod or Tilt.
        private double _neckLeft;
        private double _neckRight;
        private ServoNames? _activeNeckMode;
        private double _leftEyePopLogical;
        private double _rightEyePopLogical;

        // CAD-derived neck linkage reference points, in neck_yaw_link metres.
        // The head nod/tilt pivot is the intersection of the two Lovejoy Solid U-Joint hinge axes.  The
        // lower Delrin balls remain fixed in the yawing neck base; the upper
        // balls are attached to the head and therefore move with nod/tilt.
        private static readonly Point3D NeckUniversalPivot = new(0.000000000, 0.000000000, 0.109029515);
        private static readonly Point3D LeftLowerBall = new(0.026678547, 0.016176236, 0.016069421);
        private static readonly Point3D LeftUpperBallNeutral = new(0.065090325, 0.078298321, 0.112657778);
        private static readonly Point3D RightLowerBall = new(0.026678472, -0.016176323, 0.016069421);
        private static readonly Point3D RightUpperBallNeutral = new(0.065090331, -0.078298331, 0.112657794);

        public RobotHeadView()
        {
            _deferredCollisionTimer.Tick += (_, _) =>
            {
                _deferredCollisionTimer.Stop();
                RefreshCollisionState();
            };

            // Neutral studio backdrop preserves contrast with the model's dark details.
            SetResourceReference(BackgroundProperty, "ViewportBackground");

            _camera.FieldOfView = 38;
            _viewport.Camera = _camera;
            Children.Add(_viewport);

            // The transparent Canvas itself does not consume clicks away from
            // its children, so normal camera orbit/zoom continues to work when
            // Pose mode is off or when the pointer is between pose controls.
            Children.Add(_poseOverlay);
            InitializePoseEditorOverlay();

            _status.Text = "URDF 3-D head\nDrag to orbit\nMouse wheel to zoom\nDouble-click to reset";
            _status.Foreground = new SolidColorBrush(Color.FromArgb(205, 225, 232, 242));
            _status.Background = new SolidColorBrush(Color.FromArgb(120, 10, 12, 16));
            _status.Padding = new Thickness(9, 5, 9, 5);
            _status.Margin = new Thickness(0);
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.TextAlignment = TextAlignment.Right;
            _status.IsHitTestVisible = false;

            // Bottom-left URDF controls, stacked vertically in the requested order:
            // Collision Warning, Drive, UnDock/Dock, camera turn/Recenter row.
            _bottomControls.Orientation = Orientation.Vertical;
            _bottomControls.HorizontalAlignment = HorizontalAlignment.Left;
            _bottomControls.VerticalAlignment = VerticalAlignment.Bottom;
            _bottomControls.Margin = new Thickness(10);

            _libraryPoseSaveButton.Content = "Library +";
            _libraryPoseSaveButton.Padding = new Thickness(5, 2, 5, 2);
            _libraryPoseSaveButton.Margin = new Thickness(0, 0, 0, 3);
            _libraryPoseSaveButton.ToolTip = "Save the current URDF Pose as a reusable Library Pose";
            _libraryPoseSaveButton.Visibility = Visibility.Collapsed;
            _libraryPoseSaveButton.Click += (_, _) => LibraryPoseSaveRequested?.Invoke(this);
            _bottomControls.Children.Add(_libraryPoseSaveButton);

            _libraryPoseLoadButton.Content = "Library Load";
            _libraryPoseLoadButton.Padding = new Thickness(5, 2, 5, 2);
            _libraryPoseLoadButton.Margin = new Thickness(0, 0, 0, 3);
            _libraryPoseLoadButton.ToolTip = "Load a Library Pose into the URDF Pose editor";
            _libraryPoseLoadButton.Visibility = Visibility.Collapsed;
            _libraryPoseLoadButton.Click += (_, _) => LibraryPoseLoadRequested?.Invoke(this);
            _bottomControls.Children.Add(_libraryPoseLoadButton);

            _collisionToggleButton.Padding = new Thickness(5, 2, 5, 2);
            _collisionToggleButton.Margin = new Thickness(0, 0, 0, 3);
            _collisionToggleButton.Click += (_, _) =>
                SetCollisionWarningsEnabled(!_collisionWarningsEnabled);
            UpdateCollisionToggleButton();
            _bottomControls.Children.Add(_collisionToggleButton);

            // Drive defaults On. Turning it Off freezes all servo/timeline/RGB/
            // mouth-driven model updates while camera controls remain active.
            _driveToggleButton.Padding = new Thickness(5, 2, 5, 2);
            _driveToggleButton.Margin = new Thickness(0, 0, 0, 3);
            _driveToggleButton.Click += (_, _) =>
            {
                _urdfDriveEnabled = !_urdfDriveEnabled;
                UpdateDriveToggleButton();
            };
            UpdateDriveToggleButton();
            _bottomControls.Children.Add(_driveToggleButton);

            _dockToggleButton.Padding = new Thickness(5, 2, 5, 2);
            _dockToggleButton.Margin = new Thickness(0, 0, 0, 3);
            _dockToggleButton.Click += (_, _) => DockToggleRequested?.Invoke();
            _bottomControls.Children.Add(_dockToggleButton);

            // Camera turn/recenter row: left 90°, Recenter, right 90°.
            // Stretch Recenter so the final arrow shares the UnDock right edge.
            _cameraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _cameraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _cameraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _cameraRow.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth))
            {
                Source = _dockToggleButton
            });
            _cameraRow.SizeChanged += (_, _) => UpdatePoseOverlayLayout();
            _cameraRow.Margin = new Thickness(0, 0, 0, 3);

            _cameraMinus90Button.Content = "←";
            _cameraMinus90Button.Padding = new Thickness(5, 2, 5, 2);
            _cameraMinus90Button.Margin = new Thickness(0, 0, 3, 0);
            _cameraMinus90Button.ToolTip = "Turn the camera 90° to the left";
            _cameraMinus90Button.Click += (_, _) => TurnCameraDegrees(-90.0);
            _cameraRow.Children.Add(_cameraMinus90Button);

            _recenterButton.Content = "Recenter";
            _recenterButton.Padding = new Thickness(5, 2, 5, 2);
            _recenterButton.Margin = new Thickness(0, 0, 3, 0);
            _recenterButton.ToolTip = "Set camera yaw and pitch to 0° while preserving zoom";
            _recenterButton.Click += (_, _) => RecenterCamera();
            Grid.SetColumn(_recenterButton, 1);
            _cameraRow.Children.Add(_recenterButton);

            _cameraPlus90Button.Content = "→";
            _cameraPlus90Button.Padding = new Thickness(5, 2, 5, 2);
            _cameraPlus90Button.ToolTip = "Turn the camera 90° to the right";
            _cameraPlus90Button.Click += (_, _) => TurnCameraDegrees(90.0);
            Grid.SetColumn(_cameraPlus90Button, 2);
            _cameraRow.Children.Add(_cameraPlus90Button);
            _bottomControls.Children.Add(_cameraRow);
            Children.Add(_bottomControls);

            // Keep the URDF legend at the lower-right, independent of the controls.
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.VerticalAlignment = VerticalAlignment.Bottom;
            _status.Margin = new Thickness(0);
            _fps.Foreground = _status.Foreground;
            _fps.Background = _status.Background;
            _fps.Padding = new Thickness(9, 3, 9, 3);
            _fps.Margin = new Thickness(0, 0, 0, 3);
            _fps.HorizontalAlignment = HorizontalAlignment.Right;
            _fps.TextAlignment = TextAlignment.Right;
            var legend = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10), IsHitTestVisible = false
            };
            legend.Children.Add(_fps); legend.Children.Add(_status); Children.Add(legend);

            // Small bottom-center drag handle for continuously resizing the
            // embedded URDF pane downward. It is hidden in the detached window,
            // where the normal window resize controls are sufficient.
            _verticalResizeHandle.Width = 54;
            _verticalResizeHandle.Height = 9;
            _verticalResizeHandle.HorizontalAlignment = HorizontalAlignment.Center;
            _verticalResizeHandle.VerticalAlignment = VerticalAlignment.Bottom;
            _verticalResizeHandle.Margin = new Thickness(0, 0, 0, 2);
            _verticalResizeHandle.Cursor = Cursors.SizeNS;
            _verticalResizeHandle.Background = new SolidColorBrush(Color.FromArgb(185, 55, 65, 75));
            _verticalResizeHandle.ToolTip = "Drag vertically to resize the embedded URDF model";
            _verticalResizeHandle.DragDelta += (_, e) =>
            {
                if (_hostIsDocked)
                    VerticalResizeDeltaRequested?.Invoke(e.VerticalChange);
            };
            Children.Add(_verticalResizeHandle);

            SetDockedHostState();

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            LostMouseCapture += (_, _) => _orbiting = false;

            // Recalculate camera framing whenever the available viewport changes
            // so the neck base remains 35 pixels above its bottom edge.
            SizeChanged += (_, _) => { UpdateCamera(); UpdatePoseOverlayLayout(); };

            ResetCamera();
            LoadUrdf();
            Loaded += (_, _) =>
            {
                UpdateCamera();
                UpdatePoseOverlayLayout();
                CaptureOpeningCameraIfNeeded();
                UpdateFpsSubscription();
            };
            IsVisibleChanged += (_, _) => UpdateFpsSubscription();
            Unloaded += (_, _) => StopFpsCounter();
        }

        private void UpdateFpsSubscription()
        {
            if (!IsLoaded || !IsVisible) { StopFpsCounter(); return; }
            if (_fpsSubscribed) return;
            _fpsSubscribed = true;
            _fpsLastVisualState = (_poseRevision, _lastMouthStep, _lastRgbFrameHash, _hasRgbFrameHash);
            _fpsClock.Restart();
            _fpsTimer.Tick += RefreshFps;
            _fpsTimer.Start();
            CompositionTarget.Rendering += MeasureFps;
        }

        private void StopFpsCounter()
        {
            if (_fpsSubscribed) CompositionTarget.Rendering -= MeasureFps;
            _fpsSubscribed = false; _fpsFrameTimes.Clear();
            _fpsTimer.Stop(); _fpsTimer.Tick -= RefreshFps; _fpsClock.Reset();
            _fpsLastFrame = TimeSpan.MinValue; _fps.Text = "fps: 0";
        }

        private void MeasureFps(object sender, EventArgs e)
        {
            // WPF can raise Rendering more than once for the same frame.
            if (e is not RenderingEventArgs frame || frame.RenderingTime == _fpsLastFrame) return;
            _fpsLastFrame = frame.RenderingTime;
            // Count a changed URDF frame once, regardless of how many joints
            // changed or how often the rest of the WPF editor renders.
            var state = (_poseRevision, _lastMouthStep, _lastRgbFrameHash, _hasRgbFrameHash);
            if (state == _fpsLastVisualState) return;
            _fpsLastVisualState = state;
            _fpsFrameTimes.Enqueue(_fpsClock.Elapsed.TotalSeconds);
        }

        private void RefreshFps(object sender, EventArgs e)
        {
            double seconds = _fpsClock.Elapsed.TotalSeconds;
            if (seconds <= 0) return;
            while (_fpsFrameTimes.TryPeek(out double at) && at <= seconds - 1) _fpsFrameTimes.Dequeue();
            // A rolling second avoids 28/32 oscillation when 30 updates are
            // displayed in quarter-second reporting intervals.
            _fps.Text = "fps: " + Math.Round(_fpsFrameTimes.Count / Math.Min(seconds, 1)).ToString("0", CultureInfo.InvariantCulture);
        }

        // ================================================================
        #region Pose editor overlay

        public bool PoseEditorActive => _poseEditEnabled;
        public bool PoseLRJoined => _lrJoined;
        public event Action<RobotPoseSnapshot> PoseEdited;
        public event Action<bool> PoseModeChanged;
        public event Action<string> PoseRgbCommandChanged;
        public event Action<RobotHeadView> LibraryPoseSaveRequested;
        public event Action<RobotHeadView> LibraryPoseLoadRequested;

        public RobotPoseSnapshot CapturePose() 
        {
            if (_poseEditEnabled)
                _pose.RgbCommand = (_poseRgbCommandBox.Text ?? string.Empty).Trim();
            var copy = _pose.Clone();
            copy.LRJoined = _lrJoined;
            return copy;
        }

        public void SetPoseRgbCommand(string command)
        {
            _pose.RgbCommand = command ?? string.Empty;
            if (!_poseRgbCommandBox.IsKeyboardFocusWithin)
                _poseRgbCommandBox.Text = _pose.RgbCommand;
        }

        /// <summary>Load a complete reusable Library Pose into the Pose editor and URDF model.
        /// The caller owns RGB hardware/preview synchronization.</summary>
        public void LoadPoseSnapshot(RobotPoseSnapshot snapshot)
        {
            if (snapshot == null) return;
            CopyPoseValues(snapshot, _pose);
            _lrJoined = snapshot.LRJoined;
            _pose.LRJoined = _lrJoined;
            if (!_poseEditEnabled)
                _poseEditEnabled = true;
            UpdatePoseModeButtons();
            _poseRgbCommandBox.Text = _pose.RgbCommand ?? string.Empty;
            ApplyPoseEditorState(notify: false);
            UpdatePoseOverlayLayout();
            PoseModeChanged?.Invoke(true);
        }

        /// <summary>Used when docking/undocking so an in-progress pose draft
        /// follows the visible URDF view instead of being replaced by timeline
        /// playback during the host transition.</summary>
        public void CopyPoseEditorFrom(RobotHeadView source)
        {
            if (source == null) return;
            var p = source.CapturePose();
            CopyPoseValues(p, _pose);
            _lrJoined = p.LRJoined;
            _poseEditEnabled = source.PoseEditorActive;
            UpdatePoseModeButtons();
            if (_poseEditEnabled)
                ApplyPoseEditorState(notify: false);
            UpdatePoseOverlayLayout();
        }

        private static void CopyPoseValues(RobotPoseSnapshot from, RobotPoseSnapshot to)
        {
            to.LRJoined = from.LRJoined;
            to.LeftEyeHorizontal = from.LeftEyeHorizontal;
            to.RightEyeHorizontal = from.RightEyeHorizontal;
            to.LeftEyeVertical = from.LeftEyeVertical;
            to.RightEyeVertical = from.RightEyeVertical;
            to.LeftIris = from.LeftIris;
            to.RightIris = from.RightIris;
            to.LeftTopFlapOpen = from.LeftTopFlapOpen;
            to.RightTopFlapOpen = from.RightTopFlapOpen;
            to.LeftBottomFlapOpen = from.LeftBottomFlapOpen;
            to.RightBottomFlapOpen = from.RightBottomFlapOpen;
            to.LeftTopFlapTilt = from.LeftTopFlapTilt;
            to.RightTopFlapTilt = from.RightTopFlapTilt;
            to.LeftVent = from.LeftVent;
            to.RightVent = from.RightVent;
            to.NeckOwner = from.NeckOwner;
            to.NeckNod = from.NeckNod;
            to.NeckTilt = from.NeckTilt;
            to.NeckTurn = from.NeckTurn;
            to.NoseBody = from.NoseBody;
            to.NoseBasket = from.NoseBasket;
            to.LeftEyePop = from.LeftEyePop;
            to.RightEyePop = from.RightEyePop;
            to.WhipRaiseLower = from.WhipRaiseLower;
            to.WhipRotate = from.WhipRotate;
            to.MfrUpDown = from.MfrUpDown;
            to.MfrRotate = from.MfrRotate;
            to.MicrophoneRaiseLower = from.MicrophoneRaiseLower;
            to.RgbCommand = from.RgbCommand ?? "";
        }

        private void InitializePoseEditorOverlay()
        {
            _poseOverlay.Background = null;
            _poseOverlay.HorizontalAlignment = HorizontalAlignment.Stretch;
            _poseOverlay.VerticalAlignment = VerticalAlignment.Stretch;

            _poseButton.Content = "Pose";
            _poseButton.Padding = new Thickness(7, 2, 7, 2);
            _poseButton.ToolTip = "Show or hide direct-manipulation pose controls";
            _poseButton.Click += (_, _) =>
            {
                _poseEditEnabled = !_poseEditEnabled;
                UpdatePoseModeButtons();
                UpdatePoseOverlayLayout();
                PoseModeChanged?.Invoke(_poseEditEnabled);
            };
            _poseOverlay.Children.Add(_poseButton);

            _faceResetButton.Content = "Face Reset";
            _faceResetButton.Padding = new Thickness(7, 2, 7, 2);
            _faceResetButton.ToolTip = "Reset the facial pose to neutral without changing the neck";
            _faceResetButton.Visibility = Visibility.Collapsed;
            _faceResetButton.Click += (_, _) => ResetFacePose();
            _poseOverlay.Children.Add(_faceResetButton);

            _lrModeButton.Content = "LR Joined";
            _lrModeButton.Padding = new Thickness(7, 2, 7, 2);
            _lrModeButton.ToolTip = "Joined: eye/flap pose controls move both sides together. Split: left/right controls are independent.";
            _lrModeButton.Click += (_, _) => ToggleLrMode();
            _poseOverlay.Children.Add(_lrModeButton);

            _poseRgbCommandBox.Width = 250;
            // 25% taller than the original 25 px field for easier RGB command editing.
            _poseRgbCommandBox.Height = 31.25;
            _poseRgbCommandBox.VerticalContentAlignment = VerticalAlignment.Center;
            _poseRgbCommandBox.ToolTip = "RGB command included when this Pose is inserted";
            _poseRgbCommandBox.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || !_poseEditEnabled) return;
                CommitPoseRgbText(preview: true);
                Keyboard.ClearFocus();
                e.Handled = true;
            };
            _poseRgbCommandBox.LostKeyboardFocus += (_, _) =>
            {
                if (_poseEditEnabled) CommitPoseRgbText(preview: true);
            };
            _poseOverlay.Children.Add(_poseRgbCommandBox);

            _poseRgbBuildButton.Content = "Build";
            _poseRgbBuildButton.Padding = new Thickness(8, 2, 8, 2);
            _poseRgbBuildButton.ToolTip = "Build RGB Command and preview it on the URDF";
            _poseRgbBuildButton.Click += (_, _) => BuildPoseRgbCommand();
            _poseOverlay.Children.Add(_poseRgbBuildButton);

            ConfigureTargetCircle(_leftEyeTargetCircle);
            ConfigureTargetCircle(_rightEyeTargetCircle);
            _poseOverlay.Children.Add(_leftEyeTargetCircle);
            _poseOverlay.Children.Add(_rightEyeTargetCircle);

            ConfigureGazeHandle(_leftEyeGazeHandle, "Left eye gaze");
            ConfigureGazeHandle(_rightEyeGazeHandle, "Right eye gaze");
            _leftEyeGazeHandle.DragDelta += (_, e) => DragEyeGaze(isLeft: true, e.HorizontalChange, e.VerticalChange);
            _rightEyeGazeHandle.DragDelta += (_, e) => DragEyeGaze(isLeft: false, e.HorizontalChange, e.VerticalChange);
            _poseOverlay.Children.Add(_leftEyeGazeHandle);
            _poseOverlay.Children.Add(_rightEyeGazeHandle);

            ConfigureEyeGazeResetButton(_leftEyeGazeResetButton, "Reset left eye gimbal", () =>
            {
                _pose.LeftEyeHorizontal = 0;
                _pose.LeftEyeVertical = 0;
            });
            ConfigureEyeGazeResetButton(_rightEyeGazeResetButton, "Reset eye gimbal", () =>
            {
                if (_lrJoined)
                {
                    _pose.LeftEyeHorizontal = _pose.RightEyeHorizontal = 0;
                    _pose.LeftEyeVertical = _pose.RightEyeVertical = 0;
                }
                else
                {
                    _pose.RightEyeHorizontal = 0;
                    _pose.RightEyeVertical = 0;
                }
            });

            ConfigureIrisSlider(_joinedIrisSlider, "Both irises");
            ConfigureIrisSlider(_leftIrisSlider, "Left iris");
            ConfigureIrisSlider(_rightIrisSlider, "Right iris");
            _joinedIrisSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.LeftIris = _pose.RightIris = e.NewValue;
                ApplyPoseEditorState();
            };
            _leftIrisSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.LeftIris = e.NewValue;
                ApplyPoseEditorState();
            };
            _rightIrisSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.RightIris = e.NewValue;
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_joinedIrisSlider);
            _poseOverlay.Children.Add(_leftIrisSlider);
            _poseOverlay.Children.Add(_rightIrisSlider);
            AddPoseSliderResetButton(_joinedIrisSlider, "Reset both irises", () => _pose.LeftIris = _pose.RightIris = 0);
            AddPoseSliderResetButton(_leftIrisSlider, "Reset left iris", () => _pose.LeftIris = 0);
            AddPoseSliderResetButton(_rightIrisSlider, "Reset right iris", () => _pose.RightIris = 0);

            // Flap Open/Close is represented by a vertical slider centered on
            // the robot-head face: up opens and down closes. Joined mode moves all
            // four flaps together; Split mode exposes one vertical slider per side.
            ConfigurePoseSlider(_joinedFlapOpenSlider, "All flaps: Open up / Close down", -100, 100, Orientation.Vertical, 28, 132);
            _joinedFlapOpenSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                double v = Clamp100(e.NewValue);
                _pose.LeftTopFlapOpen = _pose.RightTopFlapOpen =
                    _pose.LeftBottomFlapOpen = _pose.RightBottomFlapOpen = v;
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_joinedFlapOpenSlider);

            ConfigurePoseSlider(_leftFlapOpenSlider, "Left flaps: Open up / Close down", -100, 100, Orientation.Vertical, 28, 132);
            _leftFlapOpenSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.LeftTopFlapOpen = _pose.LeftBottomFlapOpen = Clamp100(e.NewValue);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_leftFlapOpenSlider);

            ConfigurePoseSlider(_rightFlapOpenSlider, "Right flaps: Open up / Close down", -100, 100, Orientation.Vertical, 28, 132);
            _rightFlapOpenSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.RightTopFlapOpen = _pose.RightBottomFlapOpen = Clamp100(e.NewValue);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_rightFlapOpenSlider);
            AddPoseSliderResetButton(_joinedFlapOpenSlider, "Reset all flap open/close", () =>
            {
                _pose.LeftTopFlapOpen = _pose.RightTopFlapOpen = 0;
                _pose.LeftBottomFlapOpen = _pose.RightBottomFlapOpen = 0;
            });
            AddPoseSliderResetButton(_leftFlapOpenSlider, "Reset left flap open/close", () =>
                _pose.LeftTopFlapOpen = _pose.LeftBottomFlapOpen = 0);
            AddPoseSliderResetButton(_rightFlapOpenSlider, "Reset right flap open/close", () =>
                _pose.RightTopFlapOpen = _pose.RightBottomFlapOpen = 0);

            // Top-flap tilt remains attached directly to the corresponding flap.
            AddScalarPoseThumb("flapTiltLeft", "T", "Left upper-flap tilt", () => _pose.LeftTopFlapTilt,
                v => { _pose.LeftTopFlapTilt = Clamp100(v); }, 2.0, vertical: true);
            AddScalarPoseThumb("flapTiltRight", "T", "Right upper-flap tilt (joined: both upper flaps)", () => _pose.RightTopFlapTilt,
                v =>
                {
                    v = Clamp100(v);
                    if (_lrJoined) _pose.LeftTopFlapTilt = _pose.RightTopFlapTilt = v;
                    else _pose.RightTopFlapTilt = v;
                }, 2.0, vertical: true);

            // Nose Body and Nose Basket use dedicated vertical sliders rather
            // than floating grab thumbs. The Basket slider is intentionally half
            // the height of the Body slider.
            ConfigurePoseSlider(_noseBodySlider, "Nose body", -100, 100,
                Orientation.Vertical, 28, 110);
            _noseBodySlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.NoseBody = Clamp100(e.NewValue);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_noseBodySlider);

            ConfigurePoseSlider(_noseBasketSlider, "Nose basket", 0, 100,
                Orientation.Vertical, 28, 55);
            _noseBasketSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.NoseBasket = Math.Clamp(e.NewValue, 0, 100);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_noseBasketSlider);
            AddPoseSliderResetButton(_noseBodySlider, "Reset Nose Body", () => _pose.NoseBody = 0);
            AddPoseSliderResetButton(_noseBasketSlider, "Reset Nose Basket", () => _pose.NoseBasket = 0);

            InitializeVentArcControl();

            // NeckNodUp and NeckTiltRight take turns owning the same child
            // actuators. Moving either slider explicitly transfers ownership.
            ConfigurePoseSlider(_neckNodSlider, "Neck nod", -100, 100, Orientation.Vertical, 28, 132);
            _neckNodSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.NeckOwner = ServoNames.NeckNodUp;
                _pose.NeckNod = Clamp100(e.NewValue);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_neckNodSlider);

            ConfigurePoseSlider(_neckTiltSlider, "Neck tilt", -100, 100, Orientation.Horizontal, 112, 28);
            _neckTiltSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.NeckOwner = ServoNames.NeckTiltRight;
                _pose.NeckTilt = Clamp100(e.NewValue);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_neckTiltSlider);
            AddPoseSliderResetButton(_neckNodSlider, "Reset Neck Nod", () =>
            {
                _pose.NeckOwner = ServoNames.NeckNodUp;
                _pose.NeckNod = 0;
            });
            AddPoseSliderResetButton(_neckTiltSlider, "Reset Neck Tilt", () =>
            {
                _pose.NeckOwner = ServoNames.NeckTiltRight;
                _pose.NeckTilt = 0;
            });

            // Eye Pop is a vertical slider anchored at the bottom-front of the
            // head. Joined mode uses one center slider; Split exposes both sides.
            ConfigurePoseSlider(_joinedEyePopSlider, "Both eye pop", 0, 2000, Orientation.Vertical, 28, 108);
            _joinedEyePopSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.LeftEyePop = _pose.RightEyePop = e.NewValue;
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_joinedEyePopSlider);

            ConfigurePoseSlider(_leftEyePopSlider, "Left eye pop", 0, 2000, Orientation.Vertical, 28, 108);
            _leftEyePopSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.LeftEyePop = e.NewValue;
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_leftEyePopSlider);

            ConfigurePoseSlider(_rightEyePopSlider, "Right eye pop", 0, 2000, Orientation.Vertical, 28, 108);
            _rightEyePopSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.RightEyePop = e.NewValue;
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_rightEyePopSlider);
            AddPoseSliderResetButton(_joinedEyePopSlider, "Reset both Eye Pop values", () => _pose.LeftEyePop = _pose.RightEyePop = 0);
            AddPoseSliderResetButton(_leftEyePopSlider, "Reset left Eye Pop", () => _pose.LeftEyePop = 0);
            AddPoseSliderResetButton(_rightEyePopSlider, "Reset right Eye Pop", () => _pose.RightEyePop = 0);

            // Accessory controls are anchored to viewport edges rather than
            // moving projected hardware, keeping them reachable while orbiting.
            ConfigurePoseSlider(_whipRaiseLowerSlider, "Whip antenna up/down", 0, 100,
                Orientation.Vertical, 28, 118);
            _whipRaiseLowerSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.WhipRaiseLower = Math.Clamp(e.NewValue, 0, 100);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_whipRaiseLowerSlider);
            AddPoseSliderResetButton(_whipRaiseLowerSlider, "Reset whip antenna height", () => _pose.WhipRaiseLower = 0);

            ConfigurePoseSlider(_mfrUpDownSlider, "MFRC antenna up/down", 0, 100,
                Orientation.Vertical, 28, 118);
            _mfrUpDownSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.MfrUpDown = Math.Clamp(e.NewValue, 0, 100);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_mfrUpDownSlider);
            AddPoseSliderResetButton(_mfrUpDownSlider, "Reset MFRC antenna height", () => _pose.MfrUpDown = 0);

            ConfigurePoseSlider(_microphoneRaiseLowerSlider, "Microphone up/down", 0, 100,
                Orientation.Vertical, 28, 100);
            _microphoneRaiseLowerSlider.ValueChanged += (_, e) =>
            {
                if (_updatingPoseUi || !_poseEditEnabled) return;
                _pose.MicrophoneRaiseLower = Math.Clamp(e.NewValue, 0, 100);
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(_microphoneRaiseLowerSlider);
            AddPoseSliderResetButton(_microphoneRaiseLowerSlider, "Reset microphone height", () => _pose.MicrophoneRaiseLower = 0);

            InitializeWhipRotateDial();
            InitializeMfrRotateDial();
            InitializeNeckTurnDial();
            ApplyPoseControlRangesFromUrdf();
            UpdatePoseModeButtons();
        }

        private Button AddPoseSliderResetButton(Slider slider, string toolTip, Action resetAction)
        {
            var button = new Button
            {
                Content = "↺",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                ToolTip = toolTip,
                Visibility = Visibility.Collapsed,
                FontSize = 11
            };
            button.Click += (_, _) =>
            {
                if (!_poseEditEnabled) return;
                resetAction();
                ApplyPoseEditorState();
            };
            _poseSliderResetButtons[slider] = button;
            _poseOverlay.Children.Add(button);
            return button;
        }

        private void PlaceSliderResetButton(Slider slider)
        {
            if (!_poseSliderResetButtons.TryGetValue(slider, out var button)) return;
            if (slider.Visibility != Visibility.Visible)
            {
                button.Visibility = Visibility.Collapsed;
                return;
            }

            button.Visibility = Visibility.Visible;
            double left = Canvas.GetLeft(slider);
            double top = Canvas.GetTop(slider);
            if (double.IsNaN(left) || double.IsNaN(top)) return;

            // Reset controls always sit on the OUTSIDE of the model relative to
            // their slider.  Use the slider's direction from the viewport center
            // so the button never blocks the part of the robot the slider edits.
            Point sliderCenter = new(left + slider.Width * .5, top + slider.Height * .5);
            Point modelCenter = new(ActualWidth * .5, ActualHeight * .5);
            Vector outward = sliderCenter - modelCenter;

            // Side-by-side controls near the face center still need opposite-side
            // reset buttons, so only fall back to vertical placement when the
            // slider is essentially centered horizontally.
            Point buttonCenter;
            const double gap = 4.0;
            if (Math.Abs(outward.X) > 8.0)
            {
                double sign = Math.Sign(outward.X);
                buttonCenter = new Point(
                    sliderCenter.X + sign * (slider.Width * .5 + button.Width * .5 + gap),
                    sliderCenter.Y);
            }
            else
            {
                double sign = outward.Y < 0 ? -1.0 : 1.0;
                buttonCenter = new Point(
                    sliderCenter.X,
                    sliderCenter.Y + sign * (slider.Height * .5 + button.Height * .5 + gap));
            }

            // Keep the button reachable at the display edges while preserving the
            // outward-side intent.
            buttonCenter.X = Math.Clamp(buttonCenter.X, button.Width * .5 + 2.0,
                                        ActualWidth - button.Width * .5 - 2.0);
            buttonCenter.Y = Math.Clamp(buttonCenter.Y, button.Height * .5 + 2.0,
                                        ActualHeight - button.Height * .5 - 2.0);
            SetCanvasCenter(button, buttonCenter);
        }

        private void ResetFacePose()
        {
            if (!_poseEditEnabled) return;
            _pose.LeftEyeHorizontal = _pose.RightEyeHorizontal = 0;
            _pose.LeftEyeVertical = _pose.RightEyeVertical = 0;
            _pose.LeftIris = _pose.RightIris = 0;
            _pose.LeftTopFlapOpen = _pose.RightTopFlapOpen = 0;
            _pose.LeftBottomFlapOpen = _pose.RightBottomFlapOpen = 0;
            _pose.LeftTopFlapTilt = _pose.RightTopFlapTilt = 0;
            _pose.LeftVent = _pose.RightVent = 0;
            _pose.NoseBody = 0;
            _pose.NoseBasket = 0;
            _pose.LeftEyePop = _pose.RightEyePop = 0;

            // Face Reset also clears the Pose RGB draft.  Publish an empty RGB
            // update so the host can apply Arduino ClearAll to the URDF preview
            // (and Live Drive hardware) without storing "ClearAll" as the pose command.
            _pose.RgbCommand = string.Empty;
            _poseRgbCommandBox.Text = string.Empty;

            ApplyPoseEditorState();
            PoseRgbCommandChanged?.Invoke(string.Empty);
        }

        private void ConfigureTargetCircle(Ellipse e)
        {
            e.Stroke = new SolidColorBrush(Color.FromArgb(205, 20, 85, 115));
            e.StrokeThickness = 1.5;
            e.Fill = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
            e.IsHitTestVisible = false;
        }

        private static ControlTemplate PoseThumbTemplate(string label)
        {
            var template = new ControlTemplate(typeof(Thumb));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(225, 250, 250, 250)));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(20, 80, 110)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, label);
            text.SetValue(TextBlock.ForegroundProperty, Brushes.Black);
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            text.SetValue(TextBlock.FontSizeProperty, label.Length > 1 ? 9.0 : 11.0);
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(text);
            template.VisualTree = border;
            return template;
        }

        private Thumb CreatePoseThumb(string label, string toolTip)
        {
            var thumb = new Thumb
            {
                Width = label.Length > 1 ? 27 : 22,
                Height = 22,
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Template = PoseThumbTemplate(label),
            };
            return thumb;
        }

        private void ConfigureGazeHandle(Thumb thumb, string toolTip)
        {
            thumb.Width = thumb.Height = 19;
            thumb.Cursor = Cursors.SizeAll;
            thumb.ToolTip = toolTip + ": drag anywhere inside the eye target circle";
            thumb.Template = PoseThumbTemplate("•");
        }

        private void ConfigureEyeGazeResetButton(Button button, string toolTip, Action resetAction)
        {
            button.Content = "↺";
            button.Width = button.Height = 20;
            button.Padding = new Thickness(0);
            button.FontSize = 11;
            button.ToolTip = toolTip;
            button.Visibility = Visibility.Collapsed;
            button.Click += (_, _) =>
            {
                if (!_poseEditEnabled) return;
                resetAction();
                ApplyPoseEditorState();
            };
            _poseOverlay.Children.Add(button);
        }

        private void ConfigureIrisSlider(Slider slider, string toolTip)
        {
            slider.SetResourceReference(StyleProperty, "UrdfPoseSlider");
            slider.Minimum = -100;
            slider.Maximum = 100;
            slider.Width = 86;
            slider.Height = 20;
            slider.IsMoveToPointEnabled = true;
            slider.ToolTip = toolTip + " (-100 open, +100 closed)";
        }

        private static void ConfigurePoseSlider(Slider slider, string toolTip,
            double minimum, double maximum, Orientation orientation, double width, double height)
        {
            slider.SetResourceReference(StyleProperty, "UrdfPoseSlider");
            slider.Minimum = minimum;
            slider.Maximum = maximum;
            slider.Orientation = orientation;
            slider.Width = width;
            slider.Height = height;
            slider.IsMoveToPointEnabled = true;
            slider.ToolTip = toolTip;
        }

        private void InitializeVentArcControl()
        {
            void ConfigureArc(System.Windows.Shapes.Path path, Thumb handle, string toolTip, bool robotLeft)
            {
                path.Stroke = new SolidColorBrush(Color.FromArgb(220, 25, 85, 115));
                path.StrokeThickness = 3.0;
                path.StrokeStartLineCap = PenLineCap.Round;
                path.StrokeEndLineCap = PenLineCap.Round;
                path.IsHitTestVisible = false;
                _poseOverlay.Children.Add(path);

                handle.Width = handle.Height = 21;
                handle.Cursor = Cursors.Hand;
                handle.ToolTip = toolTip;
                handle.Template = PoseThumbTemplate("V");
                handle.DragDelta += (_, e) => DragVentArc(robotLeft, e.HorizontalChange, e.VerticalChange);
                _poseOverlay.Children.Add(handle);
            }

            ConfigureArc(_ventArcPath, _ventArcHandle,
                "Left eye vent: drag along the outer eye-tube arc", robotLeft: true);
            ConfigureArc(_rightVentArcPath, _rightVentArcHandle,
                "Right eye vent: drag along the mirrored outer eye-tube arc", robotLeft: false);

            void ConfigureReset(Button button, string toolTip, Action reset)
            {
                button.Content = "↺";
                button.Width = button.Height = 20;
                button.Padding = new Thickness(0);
                button.FontSize = 11;
                button.ToolTip = toolTip;
                button.Visibility = Visibility.Collapsed;
                button.Click += (_, _) =>
                {
                    if (!_poseEditEnabled) return;
                    reset();
                    ApplyPoseEditorState();
                };
                _poseOverlay.Children.Add(button);
            }

            ConfigureReset(_ventResetButton, "Reset left/both eye vents", () =>
            {
                if (_lrJoined) _pose.LeftVent = _pose.RightVent = 0;
                else _pose.LeftVent = 0;
            });
            ConfigureReset(_rightVentResetButton, "Reset right eye vent", () => _pose.RightVent = 0);
        }

        private void DragVentArc(bool robotLeft, double dx, double dy)
        {
            if (!_poseEditEnabled) return;

            double currentValue = _lrJoined
                ? Average(_pose.LeftVent, _pose.RightVent)
                : (robotLeft ? _pose.LeftVent : _pose.RightVent);
            if (!TryProjectOuterEyeTubeArcPoint(robotLeft, currentValue, out Point current))
                return;

            Point candidate = new(current.X + dx, current.Y + dy);
            double bestValue = currentValue;
            double bestDistance2 = double.MaxValue;

            // Find the closest point on the true projected 3-D outer eye-tube
            // quarter-circle. This keeps drag behavior correct under camera orbit,
            // head motion, perspective and LR mirroring.
            for (int i = 0; i <= 100; i++)
            {
                if (!TryProjectOuterEyeTubeArcPoint(robotLeft, i, out Point p)) continue;
                double ddx = p.X - candidate.X;
                double ddy = p.Y - candidate.Y;
                double d2 = ddx * ddx + ddy * ddy;
                if (d2 < bestDistance2)
                {
                    bestDistance2 = d2;
                    bestValue = i;
                }
            }

            if (_lrJoined)
                _pose.LeftVent = _pose.RightVent = bestValue;
            else if (robotLeft)
                _pose.LeftVent = bestValue;
            else
                _pose.RightVent = bestValue;

            ApplyPoseEditorState();
        }

        /// <summary>Project one point on the physical outer edge of an eye tube.
        /// Value 100 is the top of the tube and value 0 is its outward side.</summary>
        private bool TryProjectOuterEyeTubeArcPoint(bool robotLeft, double value, out Point screen)
        {
            double yCenter = robotLeft ? 0.0998181 : -0.0998396;
            const double xCenter = 0.1376250;
            const double radius = 0.04445; // 88.9 mm OD / 2
            double t = Math.Clamp(value, 0, 100) / 100.0;
            double theta = t * Math.PI * 0.5; // side -> top
            double outwardY = (robotLeft ? 1.0 : -1.0) * radius * Math.Cos(theta);
            double z = radius * Math.Sin(theta);
            return TryProjectLinkPoint("head_link",
                new Point3D(xCenter, yCenter + outwardY, z), out screen);
        }

        private Point VentArcPoint(bool robotLeft, double value)
        {
            if (TryProjectOuterEyeTubeArcPoint(robotLeft, value, out Point p)) return p;
            Point center = robotLeft ? _ventArcCenter : _rightVentArcCenter;
            double radius = robotLeft ? _ventArcRadius : _rightVentArcRadius;
            double v = Math.Clamp(value, 0, 100) / 100.0;
            double angle = robotLeft ? -90.0 * v : -180.0 + 90.0 * v;
            double r = angle * Deg;
            return new Point(center.X + Math.Cos(r) * radius,
                             center.Y + Math.Sin(r) * radius);
        }

        private void UpdateVentArc(bool robotLeft)
        {
            var path = robotLeft ? _ventArcPath : _rightVentArcPath;
            var handle = robotLeft ? _ventArcHandle : _rightVentArcHandle;
            var reset = robotLeft ? _ventResetButton : _rightVentResetButton;

            bool visible = robotLeft || !_lrJoined;
            path.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            handle.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            reset.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) return;

            // Draw the control directly on the projected physical outer edge of
            // the CAD eye tube instead of approximating it with a screen-space circle.
            var points = new List<Point>();
            for (int value = 100; value >= 0; value -= 5)
                if (TryProjectOuterEyeTubeArcPoint(robotLeft, value, out Point p))
                    points.Add(p);

            if (points.Count < 2)
            {
                path.Visibility = handle.Visibility = reset.Visibility = Visibility.Collapsed;
                return;
            }

            var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
            for (int i = 1; i < points.Count; i++)
                figure.Segments.Add(new LineSegment(points[i], true));
            path.Width = ActualWidth;
            path.Height = ActualHeight;
            Canvas.SetLeft(path, 0);
            Canvas.SetTop(path, 0);
            path.Data = new PathGeometry(new[] { figure });

            double valueNow = _lrJoined
                ? Average(_pose.LeftVent, _pose.RightVent)
                : (robotLeft ? _pose.LeftVent : _pose.RightVent);
            Point handlePoint = VentArcPoint(robotLeft, valueNow);
            SetCanvasCenter(handle, handlePoint);

            Vector outward = handlePoint - new Point(ActualWidth * .5, ActualHeight * .5);
            if (outward.Length < 1) outward = new Vector(robotLeft ? 1 : -1, -1);
            outward.Normalize();
            Point resetCenter = handlePoint + outward * 24.0;
            resetCenter.X = Math.Clamp(resetCenter.X, 12.0, ActualWidth - 12.0);
            resetCenter.Y = Math.Clamp(resetCenter.Y, 12.0, ActualHeight - 12.0);
            SetCanvasCenter(reset, resetCenter);
        }

        /// <summary>
        /// Project the actual CAD outer eye-tube circle. The SimplifiedHead2 tube
        /// meshes are approximately 88.9 mm OD, so a 44.45 mm radius in the
        /// head-link Y/Z plane follows the visible outer tube rather than the
        /// smaller inner eye-motion target.
        /// </summary>
        private bool TryOuterEyeTubeTarget(bool robotLeft, out Point center, out double radius)
        {
            center = new Point();
            radius = 0;

            double y = robotLeft ? 0.0998181 : -0.0998396;
            var localCenter = new Point3D(0.1376250, y, 0.0);
            const double physicalRadius = 0.04445;

            if (!TryProjectLinkPoint("head_link", localCenter, out center))
                return false;

            bool a = TryProjectLinkPoint("head_link",
                new Point3D(localCenter.X, localCenter.Y + physicalRadius, localCenter.Z), out Point py);
            bool b = TryProjectLinkPoint("head_link",
                new Point3D(localCenter.X, localCenter.Y, localCenter.Z + physicalRadius), out Point pz);

            double ry = a ? (py - center).Length : 0;
            double rz = b ? (pz - center).Length : 0;
            int count = (a ? 1 : 0) + (b ? 1 : 0);
            if (count == 0) return false;

            radius = Math.Clamp((ry + rz) / count, 24, 130);
            return true;
        }

        private void InitializeNeckTurnDial()
        {
            // v1.13.0: 50% larger than the previous dial.  Zero remains at six
            // o'clock so the pointer reads like the front of the head viewed from above.
            _neckTurnDial.Width = 156;
            _neckTurnDial.Height = 168;
            _neckTurnDial.ToolTip = "NeckTurn: drag the dial handle or edit the calibrated angle in degrees";

            var ring = new Ellipse
            {
                Width = 114,
                Height = 114,
                Stroke = new SolidColorBrush(Color.FromArgb(230, 25, 75, 100)),
                StrokeThickness = 2.5,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
            };
            Canvas.SetLeft(ring, 21);
            Canvas.SetTop(ring, 0);
            _neckTurnDial.Children.Add(ring);

            // Zero reference is deliberately at six o'clock (straight down).
            var zeroTick = new Line
            {
                X1 = 78, Y1 = 100.5, X2 = 78, Y2 = 114,
                Stroke = new SolidColorBrush(Color.FromRgb(25, 75, 100)),
                StrokeThickness = 3.5
            };
            _neckTurnDial.Children.Add(zeroTick);

            _neckTurnDialPointer.X1 = 78;
            _neckTurnDialPointer.Y1 = 57;
            _neckTurnDialPointer.Stroke = Brushes.DarkRed;
            _neckTurnDialPointer.StrokeThickness = 3.5;
            _neckTurnDialPointer.StrokeStartLineCap = PenLineCap.Round;
            _neckTurnDialPointer.StrokeEndLineCap = PenLineCap.Round;
            _neckTurnDial.Children.Add(_neckTurnDialPointer);

            _neckTurnDialHandle.Width = _neckTurnDialHandle.Height = 22;
            _neckTurnDialHandle.Cursor = Cursors.Hand;
            _neckTurnDialHandle.ToolTip = "Drag to turn the neck";
            _neckTurnDialHandle.Template = PoseThumbTemplate("•");
            _neckTurnDialHandle.DragDelta += (_, e) =>
            {
                if (!_poseEditEnabled) return;
                double hx = Canvas.GetLeft(_neckTurnDialHandle) + _neckTurnDialHandle.Width / 2.0 + e.HorizontalChange;
                double hy = Canvas.GetTop(_neckTurnDialHandle) + _neckTurnDialHandle.Height / 2.0 + e.VerticalChange;
                double angle = Math.Atan2(hx - 78.0, hy - 57.0) / Deg;
                SetNeckTurnFromDegrees(angle);
            };
            _neckTurnDial.Children.Add(_neckTurnDialHandle);

            // Reset lives in the center of the circle as requested. It resets only
            // NeckTurn; Nod/Tilt remain untouched.
            _neckTurnDialResetButton.Content = "↺";
            _neckTurnDialResetButton.Width = 28;
            _neckTurnDialResetButton.Height = 24;
            _neckTurnDialResetButton.Padding = new Thickness(0);
            _neckTurnDialResetButton.FontSize = 12;
            _neckTurnDialResetButton.ToolTip = "Reset NeckTurn to 0°";
            _neckTurnDialResetButton.Click += (_, _) =>
            {
                if (!_poseEditEnabled) return;
                _pose.NeckTurn = LogicalNeckTurnFromDegrees(0);
                ApplyPoseEditorState();
            };
            Canvas.SetLeft(_neckTurnDialResetButton, 78 - 14);
            Canvas.SetTop(_neckTurnDialResetButton, 57 - 12);
            _neckTurnDial.Children.Add(_neckTurnDialResetButton);

            _neckTurnDialEditor.Width = 84;
            _neckTurnDialEditor.Height = 32;
            _neckTurnDialEditor.HorizontalContentAlignment = HorizontalAlignment.Right;
            _neckTurnDialEditor.VerticalContentAlignment = VerticalAlignment.Center;
            _neckTurnDialEditor.ToolTip = "Editable calibrated NeckTurn angle in degrees";
            Canvas.SetLeft(_neckTurnDialEditor, 36);
            Canvas.SetTop(_neckTurnDialEditor, 130);
            _neckTurnDialEditor.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                ApplyNeckTurnEditorValue();
                Keyboard.ClearFocus();
                e.Handled = true;
            };
            _neckTurnDialEditor.LostKeyboardFocus += (_, _) => ApplyNeckTurnEditorValue();
            _neckTurnDial.Children.Add(_neckTurnDialEditor);
            _poseOverlay.Children.Add(_neckTurnDial);
        }

        private void ApplyNeckTurnEditorValue()
        {
            if (!_poseEditEnabled) return;
            string text = (_neckTurnDialEditor.Text ?? string.Empty).Replace("°", string.Empty).Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double degrees) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
            {
                SetNeckTurnFromDegrees(degrees);
            }
            else
            {
                UpdateNeckTurnDial();
            }
        }

        private void SetNeckTurnFromDegrees(double degrees)
        {
            _pose.NeckTurn = LogicalNeckTurnFromDegrees(degrees);
            ApplyPoseEditorState();
        }

        private double NeckTurnVisualDegrees(double logicalValue) =>
            -Motion(ServoNames.NeckTurn, RobotControls.NeckTurn, logicalValue);

        private double LogicalNeckTurnFromDegrees(double degrees)
        {
            const double logicalMin = -100.0, logicalMax = 100.0;
            double physicalMin = NeckTurnVisualDegrees(logicalMin);
            double physicalMax = NeckTurnVisualDegrees(logicalMax);
            double target = Math.Clamp(degrees,
                Math.Min(physicalMin, physicalMax), Math.Max(physicalMin, physicalMax));
            bool increasing = physicalMax >= physicalMin;
            double lo = logicalMin, hi = logicalMax;
            for (int i = 0; i < 56; i++)
            {
                double mid = (lo + hi) * 0.5;
                double physical = NeckTurnVisualDegrees(mid);
                if ((physical < target) == increasing) lo = mid;
                else hi = mid;
            }
            return Clamp100((lo + hi) * 0.5);
        }

        private void UpdateNeckTurnDial()
        {
            double degrees = NeckTurnVisualDegrees(_pose.NeckTurn);
            double radians = degrees * Deg;
            const double cx = 78.0, cy = 57.0, length = 43.5;
            double hx = cx + Math.Sin(radians) * length;
            double hy = cy + Math.Cos(radians) * length;
            _neckTurnDialPointer.X2 = hx;
            _neckTurnDialPointer.Y2 = hy;
            Canvas.SetLeft(_neckTurnDialHandle, hx - _neckTurnDialHandle.Width / 2.0);
            Canvas.SetTop(_neckTurnDialHandle, hy - _neckTurnDialHandle.Height / 2.0);
            if (!_neckTurnDialEditor.IsKeyboardFocusWithin)
                _neckTurnDialEditor.Text = $"{degrees:0.#}°";
        }

        private void InitializeWhipRotateDial()
        {
            _whipRotateDial.Width = 104;
            _whipRotateDial.Height = 112;
            _whipRotateDial.ToolTip = "Whip antenna rotate: drag the dial handle or edit the calibrated angle";

            var ring = new Ellipse
            {
                Width = 76,
                Height = 76,
                Stroke = new SolidColorBrush(Color.FromArgb(230, 25, 75, 100)),
                StrokeThickness = 2.0,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
            };
            Canvas.SetLeft(ring, 14);
            Canvas.SetTop(ring, 0);
            _whipRotateDial.Children.Add(ring);

            var zeroTick = new Line
            {
                X1 = 52, Y1 = 67, X2 = 52, Y2 = 76,
                Stroke = new SolidColorBrush(Color.FromRgb(25, 75, 100)),
                StrokeThickness = 3.0
            };
            _whipRotateDial.Children.Add(zeroTick);

            _whipRotateDialPointer.X1 = 52;
            _whipRotateDialPointer.Y1 = 38;
            _whipRotateDialPointer.Stroke = Brushes.DarkRed;
            _whipRotateDialPointer.StrokeThickness = 3.0;
            _whipRotateDialPointer.StrokeStartLineCap = PenLineCap.Round;
            _whipRotateDialPointer.StrokeEndLineCap = PenLineCap.Round;
            _whipRotateDial.Children.Add(_whipRotateDialPointer);

            _whipRotateDialHandle.Width = _whipRotateDialHandle.Height = 18;
            _whipRotateDialHandle.Cursor = Cursors.Hand;
            _whipRotateDialHandle.ToolTip = "Drag to rotate the whip antenna";
            _whipRotateDialHandle.Template = PoseThumbTemplate("•");
            _whipRotateDialHandle.DragDelta += (_, e) =>
            {
                if (!_poseEditEnabled) return;
                double hx = Canvas.GetLeft(_whipRotateDialHandle) + _whipRotateDialHandle.Width / 2.0 + e.HorizontalChange;
                double hy = Canvas.GetTop(_whipRotateDialHandle) + _whipRotateDialHandle.Height / 2.0 + e.VerticalChange;
                double angle = Math.Atan2(hx - 52.0, hy - 38.0) / Deg;
                SetWhipRotateFromDegrees(angle);
            };
            _whipRotateDial.Children.Add(_whipRotateDialHandle);

            _whipRotateDialResetButton.Content = "↺";
            _whipRotateDialResetButton.Width = 24;
            _whipRotateDialResetButton.Height = 22;
            _whipRotateDialResetButton.Padding = new Thickness(0);
            _whipRotateDialResetButton.FontSize = 11;
            _whipRotateDialResetButton.ToolTip = "Reset whip rotation to 0°";
            _whipRotateDialResetButton.Click += (_, _) =>
            {
                if (!_poseEditEnabled) return;
                _pose.WhipRotate = LogicalWhipRotateFromDegrees(0);
                ApplyPoseEditorState();
            };
            Canvas.SetLeft(_whipRotateDialResetButton, 40);
            Canvas.SetTop(_whipRotateDialResetButton, 27);
            _whipRotateDial.Children.Add(_whipRotateDialResetButton);

            _whipRotateDialEditor.Width = 66;
            _whipRotateDialEditor.Height = 29;
            _whipRotateDialEditor.HorizontalContentAlignment = HorizontalAlignment.Right;
            _whipRotateDialEditor.VerticalContentAlignment = VerticalAlignment.Center;
            _whipRotateDialEditor.ToolTip = "Editable calibrated whip antenna angle in degrees";
            Canvas.SetLeft(_whipRotateDialEditor, 19);
            Canvas.SetTop(_whipRotateDialEditor, 81);
            _whipRotateDialEditor.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                ApplyWhipRotateEditorValue();
                Keyboard.ClearFocus();
                e.Handled = true;
            };
            _whipRotateDialEditor.LostKeyboardFocus += (_, _) => ApplyWhipRotateEditorValue();
            _whipRotateDial.Children.Add(_whipRotateDialEditor);
            _poseOverlay.Children.Add(_whipRotateDial);
        }

        private void ApplyWhipRotateEditorValue()
        {
            if (!_poseEditEnabled) return;
            string text = (_whipRotateDialEditor.Text ?? string.Empty).Replace("°", string.Empty).Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double degrees) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
            {
                SetWhipRotateFromDegrees(degrees);
            }
            else
            {
                UpdateWhipRotateDial();
            }
        }

        private void SetWhipRotateFromDegrees(double degrees)
        {
            _pose.WhipRotate = LogicalWhipRotateFromDegrees(degrees);
            ApplyPoseEditorState();
        }

        private double WhipRotateVisualDegrees(double logicalValue) =>
            -Motion(ServoNames.Whip_Antenna_Rotate, RobotControls.Whip_Antenna_Rotate, logicalValue);

        private double LogicalWhipRotateFromDegrees(double degrees)
        {
            const double logicalMin = -100.0, logicalMax = 100.0;
            double physicalMin = WhipRotateVisualDegrees(logicalMin);
            double physicalMax = WhipRotateVisualDegrees(logicalMax);
            double target = Math.Clamp(degrees,
                Math.Min(physicalMin, physicalMax), Math.Max(physicalMin, physicalMax));
            bool increasing = physicalMax >= physicalMin;
            double lo = logicalMin, hi = logicalMax;
            for (int i = 0; i < 56; i++)
            {
                double mid = (lo + hi) * 0.5;
                double physical = WhipRotateVisualDegrees(mid);
                if ((physical < target) == increasing) lo = mid;
                else hi = mid;
            }
            return Clamp100((lo + hi) * 0.5);
        }

        private void UpdateWhipRotateDial()
        {
            double degrees = WhipRotateVisualDegrees(_pose.WhipRotate);
            double radians = degrees * Deg;
            const double cx = 52.0, cy = 38.0, length = 29.0;
            double hx = cx + Math.Sin(radians) * length;
            double hy = cy + Math.Cos(radians) * length;
            _whipRotateDialPointer.X2 = hx;
            _whipRotateDialPointer.Y2 = hy;
            Canvas.SetLeft(_whipRotateDialHandle, hx - _whipRotateDialHandle.Width / 2.0);
            Canvas.SetTop(_whipRotateDialHandle, hy - _whipRotateDialHandle.Height / 2.0);
            if (!_whipRotateDialEditor.IsKeyboardFocusWithin)
                _whipRotateDialEditor.Text = $"{degrees:0.#}°";
        }

        private void InitializeMfrRotateDial()
        {
            _mfrRotateDial.Width = 104;
            _mfrRotateDial.Height = 112;
            _mfrRotateDial.ToolTip = "MFRC antenna rotate: drag the dial handle or edit the calibrated angle";

            var ring = new Ellipse
            {
                Width = 76,
                Height = 76,
                Stroke = new SolidColorBrush(Color.FromArgb(230, 25, 75, 100)),
                StrokeThickness = 2.0,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
            };
            Canvas.SetLeft(ring, 14);
            Canvas.SetTop(ring, 0);
            _mfrRotateDial.Children.Add(ring);

            var zeroTick = new Line
            {
                X1 = 52, Y1 = 67, X2 = 52, Y2 = 76,
                Stroke = new SolidColorBrush(Color.FromRgb(25, 75, 100)),
                StrokeThickness = 3.0
            };
            _mfrRotateDial.Children.Add(zeroTick);

            _mfrRotateDialPointer.X1 = 52;
            _mfrRotateDialPointer.Y1 = 38;
            _mfrRotateDialPointer.Stroke = Brushes.DarkRed;
            _mfrRotateDialPointer.StrokeThickness = 3.0;
            _mfrRotateDialPointer.StrokeStartLineCap = PenLineCap.Round;
            _mfrRotateDialPointer.StrokeEndLineCap = PenLineCap.Round;
            _mfrRotateDial.Children.Add(_mfrRotateDialPointer);

            _mfrRotateDialHandle.Width = _mfrRotateDialHandle.Height = 18;
            _mfrRotateDialHandle.Cursor = Cursors.Hand;
            _mfrRotateDialHandle.ToolTip = "Drag to rotate the MFRC antenna";
            _mfrRotateDialHandle.Template = PoseThumbTemplate("•");
            _mfrRotateDialHandle.DragDelta += (_, e) =>
            {
                if (!_poseEditEnabled) return;
                double hx = Canvas.GetLeft(_mfrRotateDialHandle) + _mfrRotateDialHandle.Width / 2.0 + e.HorizontalChange;
                double hy = Canvas.GetTop(_mfrRotateDialHandle) + _mfrRotateDialHandle.Height / 2.0 + e.VerticalChange;
                double angle = Math.Atan2(hx - 52.0, hy - 38.0) / Deg;
                SetMfrRotateFromDegrees(angle);
            };
            _mfrRotateDial.Children.Add(_mfrRotateDialHandle);

            _mfrRotateDialResetButton.Content = "↺";
            _mfrRotateDialResetButton.Width = 24;
            _mfrRotateDialResetButton.Height = 22;
            _mfrRotateDialResetButton.Padding = new Thickness(0);
            _mfrRotateDialResetButton.FontSize = 11;
            _mfrRotateDialResetButton.ToolTip = "Reset MFRC rotation to 0°";
            _mfrRotateDialResetButton.Click += (_, _) =>
            {
                if (!_poseEditEnabled) return;
                _pose.MfrRotate = LogicalMfrRotateFromDegrees(0);
                ApplyPoseEditorState();
            };
            Canvas.SetLeft(_mfrRotateDialResetButton, 40);
            Canvas.SetTop(_mfrRotateDialResetButton, 27);
            _mfrRotateDial.Children.Add(_mfrRotateDialResetButton);

            _mfrRotateDialEditor.Width = 66;
            _mfrRotateDialEditor.Height = 29;
            _mfrRotateDialEditor.HorizontalContentAlignment = HorizontalAlignment.Right;
            _mfrRotateDialEditor.VerticalContentAlignment = VerticalAlignment.Center;
            _mfrRotateDialEditor.ToolTip = "Editable calibrated MFRC antenna angle in degrees";
            Canvas.SetLeft(_mfrRotateDialEditor, 19);
            Canvas.SetTop(_mfrRotateDialEditor, 81);
            _mfrRotateDialEditor.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                ApplyMfrRotateEditorValue();
                Keyboard.ClearFocus();
                e.Handled = true;
            };
            _mfrRotateDialEditor.LostKeyboardFocus += (_, _) => ApplyMfrRotateEditorValue();
            _mfrRotateDial.Children.Add(_mfrRotateDialEditor);
            _poseOverlay.Children.Add(_mfrRotateDial);
        }

        private void ApplyMfrRotateEditorValue()
        {
            if (!_poseEditEnabled) return;
            string text = (_mfrRotateDialEditor.Text ?? string.Empty).Replace("°", string.Empty).Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double degrees) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
                SetMfrRotateFromDegrees(degrees);
            else
                UpdateMfrRotateDial();
        }

        private void SetMfrRotateFromDegrees(double degrees)
        {
            _pose.MfrRotate = LogicalMfrRotateFromDegrees(degrees);
            ApplyPoseEditorState();
        }

        private double MfrRotateVisualDegrees(double logicalValue) =>
            -Motion(ServoNames.MFR_Rotate, RobotControls.MFR_Rotate, logicalValue);

        private double LogicalMfrRotateFromDegrees(double degrees)
        {
            const double logicalMin = -100.0, logicalMax = 100.0;
            double physicalMin = MfrRotateVisualDegrees(logicalMin);
            double physicalMax = MfrRotateVisualDegrees(logicalMax);
            double target = Math.Clamp(degrees,
                Math.Min(physicalMin, physicalMax), Math.Max(physicalMin, physicalMax));
            bool increasing = physicalMax >= physicalMin;
            double lo = logicalMin, hi = logicalMax;
            for (int i = 0; i < 56; i++)
            {
                double mid = (lo + hi) * 0.5;
                double physical = MfrRotateVisualDegrees(mid);
                if ((physical < target) == increasing) lo = mid;
                else hi = mid;
            }
            return Clamp100((lo + hi) * 0.5);
        }

        private void UpdateMfrRotateDial()
        {
            double degrees = MfrRotateVisualDegrees(_pose.MfrRotate);
            double radians = degrees * Deg;
            const double cx = 52.0, cy = 38.0, length = 29.0;
            double hx = cx + Math.Sin(radians) * length;
            double hy = cy + Math.Cos(radians) * length;
            _mfrRotateDialPointer.X2 = hx;
            _mfrRotateDialPointer.Y2 = hy;
            Canvas.SetLeft(_mfrRotateDialHandle, hx - _mfrRotateDialHandle.Width / 2.0);
            Canvas.SetTop(_mfrRotateDialHandle, hy - _mfrRotateDialHandle.Height / 2.0);
            if (!_mfrRotateDialEditor.IsKeyboardFocusWithin)
                _mfrRotateDialEditor.Text = $"{degrees:0.#}°";
        }

        private void AddScalarPoseThumb(string key, string label, string toolTip,
            Func<double> getter, Action<double> setter, double unitsPerPixel, bool vertical)
        {
            var thumb = CreatePoseThumb(label, toolTip);
            thumb.DragDelta += (_, e) =>
            {
                if (!_poseEditEnabled) return;
                double delta = vertical ? -e.VerticalChange : e.HorizontalChange;
                setter(getter() + delta * unitsPerPixel);
                ApplyPoseEditorState();
            };
            _poseThumbs[key] = thumb;
            _poseOverlay.Children.Add(thumb);
        }

        private void ToggleLrMode()
        {
            if (!_poseEditEnabled) return;
            if (_lrJoined)
            {
                _lrJoined = false; // retain the current matching values, then diverge freely
            }
            else
            {
                _lrJoined = true;
                double h = Average(_pose.LeftEyeHorizontal, _pose.RightEyeHorizontal);
                double v = Average(_pose.LeftEyeVertical, _pose.RightEyeVertical);
                double iris = Average(_pose.LeftIris, _pose.RightIris);
                double open = Average(_pose.LeftTopFlapOpen, _pose.RightTopFlapOpen,
                                      _pose.LeftBottomFlapOpen, _pose.RightBottomFlapOpen);
                double tilt = Average(_pose.LeftTopFlapTilt, _pose.RightTopFlapTilt);
                _pose.LeftEyeHorizontal = _pose.RightEyeHorizontal = h;
                _pose.LeftEyeVertical = _pose.RightEyeVertical = v;
                _pose.LeftIris = _pose.RightIris = iris;
                _pose.LeftTopFlapOpen = _pose.RightTopFlapOpen = open;
                _pose.LeftBottomFlapOpen = _pose.RightBottomFlapOpen = open;
                _pose.LeftTopFlapTilt = _pose.RightTopFlapTilt = tilt;
                ApplyPoseEditorState();
            }
            _pose.LRJoined = _lrJoined;
            UpdatePoseModeButtons();
            UpdatePoseOverlayLayout();
        }

        private void CommitPoseRgbText(bool preview)
        {
            _pose.RgbCommand = (_poseRgbCommandBox.Text ?? string.Empty).Trim();
            if (preview && !string.IsNullOrWhiteSpace(_pose.RgbCommand))
                PoseRgbCommandChanged?.Invoke(_pose.RgbCommand);
        }

        private void BuildPoseRgbCommand()
        {
            if (!_poseEditEnabled) return;
            CommitPoseRgbText(preview: false);
            var builder = new RgbBuilderWindow(_pose.RgbCommand)
            {
                Owner = Window.GetWindow(this)
            };
            if (builder.ShowDialog() != true) return;
            _pose.RgbCommand = builder.ResultText ?? string.Empty;
            _poseRgbCommandBox.Text = _pose.RgbCommand;
            if (!string.IsNullOrWhiteSpace(_pose.RgbCommand))
                PoseRgbCommandChanged?.Invoke(_pose.RgbCommand);
        }

        /// <summary>
        /// Pose controls use the same normalized authoring ranges that feed the
        /// calibrated URDF mapping.  Changing URDF Min/Max/Zero therefore changes
        /// the physical endpoints reached by these controls without allowing the
        /// Pose editor to exceed those calibrated endpoints.
        /// </summary>
        private void ApplyPoseControlRangesFromUrdf()
        {
            void Range(Slider slider, ServoNames servo)
            {
                var r = UrdfConfiguration.TestInputRange(servo);
                slider.Minimum = r.Min;
                slider.Maximum = r.Max;
            }

            Range(_joinedFlapOpenSlider, ServoNames.FlapsOpen);
            Range(_leftFlapOpenSlider, ServoNames.FlapsOpen);
            Range(_rightFlapOpenSlider, ServoNames.FlapsOpen);
            Range(_noseBodySlider, ServoNames.NoseBody);
            Range(_noseBasketSlider, ServoNames.NoseBasket);
            Range(_neckNodSlider, ServoNames.NeckNodUp);
            Range(_neckTiltSlider, ServoNames.NeckTiltRight);
            Range(_whipRaiseLowerSlider, ServoNames.Whip_Antenna_RaiseLower);
            Range(_mfrUpDownSlider, ServoNames.MFR_UpDown);
            Range(_microphoneRaiseLowerSlider, ServoNames.Microphone_RaiseLower);

            // EyePop's timeline authoring convention is 0..2000 while the URDF
            // calibration maps that entire interval onto its configured mm endpoints.
            _joinedEyePopSlider.Minimum = _leftEyePopSlider.Minimum = _rightEyePopSlider.Minimum = 0;
            _joinedEyePopSlider.Maximum = _leftEyePopSlider.Maximum = _rightEyePopSlider.Maximum = 2000;
        }

        private void UpdatePoseModeButtons()
        {
            _poseButton.Content = _poseEditEnabled ? "Pose: On" : "Pose";
            _poseButton.SetResourceReference(Button.BackgroundProperty, _poseEditEnabled ? "SequenceAccentSurface" : "ControlBackground");
            _poseButton.SetResourceReference(Button.BorderBrushProperty, _poseEditEnabled ? "SequenceAccent" : "ControlBorder");
            _poseButton.SetResourceReference(Button.ForegroundProperty, "PrimaryText");
            _lrModeButton.Content = _lrJoined ? "LR Joined" : "LR Split";
            _lrModeButton.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
            _poseRgbCommandBox.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
            _poseRgbBuildButton.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
            _faceResetButton.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
            _libraryPoseSaveButton.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
            _libraryPoseLoadButton.Visibility = _poseEditEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private static double Clamp100(double v) => Math.Clamp(v, -100, 100);
        private static double Average(params double[] values) => values.Length == 0 ? 0 : values.Average();

        private void DragEyeGaze(bool isLeft, double dx, double dy)
        {
            if (!_poseEditEnabled) return;
            double radius = EyeTargetRadius(isLeft);
            if (radius < 4) radius = 35;

            double h = isLeft ? _pose.LeftEyeHorizontal : _pose.RightEyeHorizontal;
            double v = isLeft ? _pose.LeftEyeVertical : _pose.RightEyeVertical;
            // Horizontal grab direction is intentionally reversed relative
            // to the logical EyesHorizontalRight sign used by the command grid.
            double x = -h / 100.0 * radius + dx;
            double y = -v / 100.0 * radius + dy;
            double len = Math.Sqrt(x * x + y * y);
            if (len > radius)
            {
                x *= radius / len;
                y *= radius / len;
            }
            h = Clamp100(-x / radius * 100.0);
            v = Clamp100(-y / radius * 100.0);

            if (_lrJoined)
            {
                _pose.LeftEyeHorizontal = _pose.RightEyeHorizontal = h;
                _pose.LeftEyeVertical = _pose.RightEyeVertical = v;
            }
            else if (isLeft)
            {
                _pose.LeftEyeHorizontal = h;
                _pose.LeftEyeVertical = v;
            }
            else
            {
                _pose.RightEyeHorizontal = h;
                _pose.RightEyeVertical = v;
            }
            ApplyPoseEditorState();
        }

        private void ApplyPoseEditorState(bool notify = true)
        {
            if (_scene == null) return;
            _poseInternalUpdate = true;
            _suppressCollisionRefresh = true;
            try
            {
                SetControl(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal, _pose.LeftEyeHorizontal);
                SetControl(ServoNames.EyesHorizontalRight, RobotControls.RightLensHorizontal, _pose.RightEyeHorizontal);
                SetControl(ServoNames.EyesVerticalUp, RobotControls.LeftLensVertical, _pose.LeftEyeVertical);
                SetControl(ServoNames.EyesVerticalUp, RobotControls.RightLensVertical, _pose.RightEyeVertical);
                SetControl(ServoNames.IrisClose, RobotControls.LeftIris, _pose.LeftIris);
                SetControl(ServoNames.IrisClose, RobotControls.RightIris, _pose.RightIris);
                SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, _pose.LeftTopFlapOpen);
                SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, _pose.RightTopFlapOpen);
                SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, _pose.LeftBottomFlapOpen);
                SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, _pose.RightBottomFlapOpen);
                SetControl(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, _pose.LeftTopFlapTilt);
                SetControl(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, _pose.RightTopFlapTilt);
                SetControl(ServoNames.VentsOpen, RobotControls.LeftEyeVent, _pose.LeftVent);
                SetControl(ServoNames.VentsOpen, RobotControls.RightEyeVent, _pose.RightVent);

                SetSharedNeckState(_pose.NeckOwner, _pose.NeckNod, _pose.NeckTilt);
                ApplyNeckPose(_pose.NeckTurn);

                SetServo(ServoNames.NoseBody, _pose.NoseBody);
                SetServo(ServoNames.NoseBasket, _pose.NoseBasket);
                SetServo(ServoNames.LeftEyePop, _pose.LeftEyePop);
                SetServo(ServoNames.RightEyePop, _pose.RightEyePop);
                SetServo(ServoNames.Whip_Antenna_RaiseLower, _pose.WhipRaiseLower);
                SetServo(ServoNames.Whip_Antenna_Rotate, _pose.WhipRotate);
                SetServo(ServoNames.MFR_UpDown, _pose.MfrUpDown);
                SetServo(ServoNames.MFR_Rotate, _pose.MfrRotate);
                SetServo(ServoNames.Microphone_RaiseLower, _pose.MicrophoneRaiseLower);
            }
            finally
            {
                _poseInternalUpdate = false;
                _suppressCollisionRefresh = false;
            }
            RefreshCollisionState();
            UpdatePoseOverlayLayout();
            if (notify) PoseEdited?.Invoke(CapturePose());
        }

        /// <summary>Capture the ordinary timeline/grid pose that is currently
        /// being pushed into this view.  Pose mode freezes that snapshot as the
        /// starting point for direct manipulation.</summary>
        private void CaptureIncomingPose(double eyeHLeftScreen, double eyeHRightScreen,
                            double eyeVLeftScreen, double eyeVRightScreen,
                            double irisLeftScreen, double irisRightScreen,
                            double topFlapLeftScreen, double topFlapRightScreen,
                            double bottomFlapLeftScreen, double bottomFlapRightScreen,
                            double tiltLeftScreen, double tiltRightScreen,
                            double ventsLeftScreen, double ventsRightScreen,
                            double neckTilt, double neckNod, ServoNames? neckOwner, double neckTurn,
                            double whip, double mic, double mfr, double noseBody, double noseBasket,
                            double leftEyePop, double rightEyePop, double whipRotate, double mfrRotate)
        {
            // SetPose arguments use SCREEN sides. Convert back to physical
            // RobotControls Left/Right for the pose snapshot.
            _pose.LeftEyeHorizontal = eyeHRightScreen;
            _pose.RightEyeHorizontal = eyeHLeftScreen;
            _pose.LeftEyeVertical = eyeVRightScreen;
            _pose.RightEyeVertical = eyeVLeftScreen;
            _pose.LeftIris = irisRightScreen;
            _pose.RightIris = irisLeftScreen;
            _pose.LeftTopFlapOpen = topFlapRightScreen;
            _pose.RightTopFlapOpen = topFlapLeftScreen;
            _pose.LeftBottomFlapOpen = bottomFlapRightScreen;
            _pose.RightBottomFlapOpen = bottomFlapLeftScreen;
            _pose.LeftTopFlapTilt = tiltRightScreen;
            _pose.RightTopFlapTilt = tiltLeftScreen;
            _pose.LeftVent = ventsRightScreen;
            _pose.RightVent = ventsLeftScreen;
            _pose.NeckTilt = neckTilt;
            _pose.NeckNod = neckNod;
            _pose.NeckOwner = neckOwner;
            _pose.NeckTurn = neckTurn;
            _pose.WhipRaiseLower = whip;
            _pose.MicrophoneRaiseLower = mic;
            _pose.MfrUpDown = mfr;
            _pose.NoseBody = noseBody;
            _pose.NoseBasket = noseBasket;
            _pose.LeftEyePop = leftEyePop;
            _pose.RightEyePop = rightEyePop;
            _pose.WhipRotate = whipRotate;
            _pose.MfrRotate = mfrRotate;
            _pose.LRJoined = _lrJoined;
        }

        private void UpdatePoseStateForServo(ServoNames servo, double value)
        {
            switch (servo)
            {
                case ServoNames.EyesHorizontalRight: _pose.LeftEyeHorizontal = _pose.RightEyeHorizontal = value; break;
                case ServoNames.EyesVerticalUp: _pose.LeftEyeVertical = _pose.RightEyeVertical = value; break;
                case ServoNames.IrisClose: _pose.LeftIris = _pose.RightIris = value; break;
                case ServoNames.FlapsOpen:
                    _pose.LeftTopFlapOpen = _pose.RightTopFlapOpen = _pose.LeftBottomFlapOpen = _pose.RightBottomFlapOpen = value; break;
                case ServoNames.FlapTiltUp: _pose.LeftTopFlapTilt = _pose.RightTopFlapTilt = value; break;
                case ServoNames.VentsOpen: _pose.LeftVent = _pose.RightVent = value; break;
                case ServoNames.NeckTurn: _pose.NeckTurn = value; break;
                case ServoNames.NeckNodUp: _pose.NeckOwner = servo; _pose.NeckNod = value; break;
                case ServoNames.NeckTiltRight: _pose.NeckOwner = servo; _pose.NeckTilt = value; break;
                case ServoNames.NoseBody: _pose.NoseBody = value; break;
                case ServoNames.NoseBasket: _pose.NoseBasket = value; break;
                case ServoNames.LeftEyePop: _pose.LeftEyePop = value; break;
                case ServoNames.RightEyePop: _pose.RightEyePop = value; break;
                case ServoNames.BothEyePop: _pose.LeftEyePop = _pose.RightEyePop = value; break;
                case ServoNames.Whip_Antenna_RaiseLower: _pose.WhipRaiseLower = value; break;
                case ServoNames.Whip_Antenna_Rotate: _pose.WhipRotate = value; break;
                case ServoNames.MFR_UpDown: _pose.MfrUpDown = value; break;
                case ServoNames.MFR_Rotate: _pose.MfrRotate = value; break;
                case ServoNames.Microphone_RaiseLower: _pose.MicrophoneRaiseLower = value; break;
            }
        }

        private void UpdatePoseStateForChild(ServoNames parent, RobotControls control, double value)
        {
            switch (control)
            {
                case RobotControls.NeckTurn: _pose.NeckTurn = value; break;
                case RobotControls.LeftEyePop: _pose.LeftEyePop = value; break;
                case RobotControls.RightEyePop: _pose.RightEyePop = value; break;
                case RobotControls.NoseBody: _pose.NoseBody = value; break;
                case RobotControls.NoseBasket: _pose.NoseBasket = value; break;
                case RobotControls.Whip_Antenna_RaiseLower: _pose.WhipRaiseLower = value; break;
                case RobotControls.Whip_Antenna_Rotate: _pose.WhipRotate = value; break;
                case RobotControls.MFR_UpDown: _pose.MfrUpDown = value; break;
                case RobotControls.MFR_Rotate: _pose.MfrRotate = value; break;
                case RobotControls.Microphone_RaiseLower: _pose.MicrophoneRaiseLower = value; break;
                case RobotControls.LeftLensHorizontal: _pose.LeftEyeHorizontal = value; break;
                case RobotControls.RightLensHorizontal: _pose.RightEyeHorizontal = value; break;
                case RobotControls.LeftLensVertical: _pose.LeftEyeVertical = value; break;
                case RobotControls.RightLensVertical: _pose.RightEyeVertical = value; break;
                case RobotControls.LeftIris: _pose.LeftIris = value; break;
                case RobotControls.RightIris: _pose.RightIris = value; break;
                case RobotControls.BrowLeftTopOpen: _pose.LeftTopFlapOpen = value; break;
                case RobotControls.BrowRightTopOpen: _pose.RightTopFlapOpen = value; break;
                case RobotControls.BrowLeftBottomOpen: _pose.LeftBottomFlapOpen = value; break;
                case RobotControls.BrowRightBottomOpen: _pose.RightBottomFlapOpen = value; break;
                case RobotControls.BrowLeftTopTilt: _pose.LeftTopFlapTilt = value; break;
                case RobotControls.BrowRightTopTilt: _pose.RightTopFlapTilt = value; break;
                case RobotControls.LeftEyeVent: _pose.LeftVent = value; break;
                case RobotControls.RightEyeVent: _pose.RightVent = value; break;
            }
            if (parent is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
            {
                _pose.NeckOwner = parent;
                if (parent == ServoNames.NeckNodUp) _pose.NeckNod = value;
                else _pose.NeckTilt = value;
            }
        }

        private void UpdatePoseOverlayLayout()
        {
            if (ActualWidth < 2 || ActualHeight < 2) return;

            Point arrowCenter;
            try
            {
                arrowCenter = _cameraPlus90Button.TranslatePoint(
                    new Point(Math.Max(0, _cameraPlus90Button.ActualWidth / 2),
                              Math.Max(0, _cameraPlus90Button.ActualHeight / 2)), this);
            }
            catch { arrowCenter = new Point(150, ActualHeight - 22); }
            var bottomHandleCenter = new Point(ActualWidth / 2.0, ActualHeight - 6.0);
            Point poseButtonCenter = new((arrowCenter.X + bottomHandleCenter.X) / 2.0,
                                        (arrowCenter.Y + bottomHandleCenter.Y) / 2.0);
            SetCanvasCenter(_poseButton, poseButtonCenter);
            double poseWidth = Math.Max(52.0, _poseButton.ActualWidth);
            double faceWidth = Math.Max(72.0, _faceResetButton.ActualWidth);
            SetCanvasCenter(_faceResetButton,
                new Point(poseButtonCenter.X + poseWidth * .5 + faceWidth * .5 + 6.0,
                          poseButtonCenter.Y));

            if (!_poseEditEnabled)
            {
                foreach (UIElement child in _poseOverlay.Children)
                    if (!ReferenceEquals(child, _poseButton)) child.Visibility = Visibility.Collapsed;
                return;
            }

            _lrModeButton.Visibility = Visibility.Visible;
            SetCanvasCenter(_lrModeButton, new Point(ActualWidth / 2.0, 52));

            _poseRgbCommandBox.Visibility = Visibility.Visible;
            _poseRgbBuildButton.Visibility = Visibility.Visible;
            if (!_poseRgbCommandBox.IsKeyboardFocusWithin)
                _poseRgbCommandBox.Text = _pose.RgbCommand ?? string.Empty;
            double rgbRowWidth = _poseRgbCommandBox.Width + Math.Max(54.0, _poseRgbBuildButton.ActualWidth) + 6.0;
            double rgbLeft = Math.Max(6.0, (ActualWidth - rgbRowWidth) / 2.0);
            Canvas.SetLeft(_poseRgbCommandBox, rgbLeft);
            Canvas.SetTop(_poseRgbCommandBox, 8.0);
            Canvas.SetLeft(_poseRgbBuildButton, rgbLeft + _poseRgbCommandBox.Width + 6.0);
            Canvas.SetTop(_poseRgbBuildButton, 8.0);

            bool leftOk = TryEyeTarget(robotLeft: true, out Point leftEye, out double leftRadius);
            bool rightOk = TryEyeTarget(robotLeft: false, out Point rightEye, out double rightRadius);
            if (!leftOk) { leftEye = new Point(ActualWidth * .60, ActualHeight * .35); leftRadius = 36; }
            if (!rightOk) { rightEye = new Point(ActualWidth * .40, ActualHeight * .35); rightRadius = 36; }

            // Joined operation intentionally uses the robot RIGHT eye target.
            // Split operation exposes both independent eye targets.
            PlaceEyeTarget(_leftEyeTargetCircle, _leftEyeGazeHandle, leftEye, leftRadius,
                _pose.LeftEyeHorizontal, _pose.LeftEyeVertical, visible: !_lrJoined);
            PlaceEyeTarget(_rightEyeTargetCircle, _rightEyeGazeHandle, rightEye, rightRadius,
                _pose.RightEyeHorizontal, _pose.RightEyeVertical, visible: true);

            // Gimbal reset buttons sit just outside each visible gaze circle on the
            // circle edge nearest the inside/center of the robot head.
            Point eyeInside = new((leftEye.X + rightEye.X) * .5, (leftEye.Y + rightEye.Y) * .5);
            PlaceEyeGazeResetButton(_leftEyeGazeResetButton, leftEye, leftRadius, eyeInside, !_lrJoined);
            PlaceEyeGazeResetButton(_rightEyeGazeResetButton, rightEye, rightRadius, eyeInside, true);

            _updatingPoseUi = true;
            try
            {
                _joinedIrisSlider.Value = Average(_pose.LeftIris, _pose.RightIris);
                _leftIrisSlider.Value = _pose.LeftIris;
                _rightIrisSlider.Value = _pose.RightIris;
                _joinedFlapOpenSlider.Value = Average(_pose.LeftTopFlapOpen, _pose.RightTopFlapOpen,
                                                       _pose.LeftBottomFlapOpen, _pose.RightBottomFlapOpen);
                _leftFlapOpenSlider.Value = Average(_pose.LeftTopFlapOpen, _pose.LeftBottomFlapOpen);
                _rightFlapOpenSlider.Value = Average(_pose.RightTopFlapOpen, _pose.RightBottomFlapOpen);
                _noseBodySlider.Value = _pose.NoseBody;
                _noseBasketSlider.Value = _pose.NoseBasket;
                _neckNodSlider.Value = _pose.NeckNod;
                _neckTiltSlider.Value = _pose.NeckTilt;
                _joinedEyePopSlider.Value = Average(_pose.LeftEyePop, _pose.RightEyePop);
                _leftEyePopSlider.Value = _pose.LeftEyePop;
                _rightEyePopSlider.Value = _pose.RightEyePop;
                _whipRaiseLowerSlider.Value = _pose.WhipRaiseLower;
                _mfrUpDownSlider.Value = _pose.MfrUpDown;
                _microphoneRaiseLowerSlider.Value = _pose.MicrophoneRaiseLower;
            }
            finally { _updatingPoseUi = false; }

            // Joined iris placement is the opposite eye from the joined gaze
            // handle: under the robot LEFT eye. Split mode has one per eye.
            _joinedIrisSlider.Visibility = _lrJoined ? Visibility.Visible : Visibility.Collapsed;
            SetCanvasCenter(_joinedIrisSlider, new Point(leftEye.X, leftEye.Y + leftRadius + 12));
            _leftIrisSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            _rightIrisSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            SetCanvasCenter(_leftIrisSlider, new Point(leftEye.X, leftEye.Y + leftRadius + 12));
            SetCanvasCenter(_rightIrisSlider, new Point(rightEye.X, rightEye.Y + rightRadius + 12));
            PlaceSliderResetButton(_joinedIrisSlider);
            PlaceSliderResetButton(_leftIrisSlider);
            PlaceSliderResetButton(_rightIrisSlider);

            // Top-flap tilt remains attached to the actual flap panels.
            Point leftTop = ProjectLinkOr("left_top_flap_link", new Point(ActualWidth * .62, ActualHeight * .29));
            Point rightTop = ProjectLinkOr("right_top_flap_link", new Point(ActualWidth * .38, ActualHeight * .29));
            PlaceThumb("flapTiltRight", Offset(rightTop, -12, 14), true);
            PlaceThumb("flapTiltLeft", Offset(leftTop, 12, 14), !_lrJoined);

            // NoseBody, NoseBasket and Flap Open/Close are anchored directly to
            // the HEAD frame, not to the moving NoseBody. Their centers use the
            // midpoint of the imported head-link visual bounds, so they stay
            // vertically centered on the complete robot head. Their lateral spacing
            // matches the neutral NoseBody opening without following nose motion.
            const double faceControlX = 0.1771615; // NoseBody origin + neutral basket-opening X
            const double faceControlZ = -0.01755391; // midpoint of head-link visual Z bounds
            const double faceControlHalfWidth = 0.024;
            Point noseOpening = TryProjectLinkPoint("head_link",
                new Point3D(faceControlX, 0, faceControlZ), out Point headFaceCenter)
                ? headFaceCenter : new Point(ActualWidth * .50, (leftEye.Y + rightEye.Y) * .5);
            Point noseOpeningRobotLeft = TryProjectLinkPoint("head_link",
                new Point3D(faceControlX, +faceControlHalfWidth, faceControlZ), out Point headFaceLeft)
                ? headFaceLeft : new Point(noseOpening.X + 30.0, noseOpening.Y);
            Point noseOpeningRobotRight = TryProjectLinkPoint("head_link",
                new Point3D(faceControlX, -faceControlHalfWidth, faceControlZ), out Point headFaceRight)
                ? headFaceRight : new Point(noseOpening.X - 30.0, noseOpening.Y);

            // Nose Body and Basket remain unchanged when LR Split is active.
            _noseBodySlider.Visibility = Visibility.Visible;
            _noseBasketSlider.Visibility = Visibility.Visible;
            SetCanvasCenter(_noseBodySlider, noseOpeningRobotRight);
            SetCanvasCenter(_noseBasketSlider, noseOpeningRobotLeft);

            _joinedFlapOpenSlider.Visibility = _lrJoined ? Visibility.Visible : Visibility.Collapsed;
            _leftFlapOpenSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            _rightFlapOpenSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            SetCanvasCenter(_joinedFlapOpenSlider, noseOpening);

            // In Split mode the left/right Open/Close sliders are side-by-side at
            // the face center rather than following the Nose Body/Basket controls.
            Vector faceLeftRight = noseOpeningRobotLeft - noseOpeningRobotRight;
            if (faceLeftRight.Length < 1) faceLeftRight = new Vector(1, 0);
            faceLeftRight.Normalize();
            SetCanvasCenter(_leftFlapOpenSlider, noseOpening + faceLeftRight * 18.0);
            SetCanvasCenter(_rightFlapOpenSlider, noseOpening - faceLeftRight * 18.0);
            PlaceSliderResetButton(_noseBodySlider);
            PlaceSliderResetButton(_noseBasketSlider);
            PlaceSliderResetButton(_joinedFlapOpenSlider);
            PlaceSliderResetButton(_leftFlapOpenSlider);
            PlaceSliderResetButton(_rightFlapOpenSlider);

            // Joined mode uses the robot-left Vent arc for both vents. LR Split
            // adds the mirrored quarter-arc on the opposite outer eye tube so
            // left and right vents can be posed independently.
            UpdateVentArc(robotLeft: true);
            UpdateVentArc(robotLeft: false);

            // Eye Pop controls sit 15 mm outside the physical head sides (5 mm
            // farther out than v1.13.1). Their BOTTOM edge is screen-aligned to
            // the projected top-center of the physical mouth, so they stay in the
            // requested vertical relationship as the head/camera moves.
            const double headFrontX = 0.18525;
            const double headSideY = 0.144747;
            const double eyePopOutsideMm = 0.015;
            const double mouthTopFrontX = 0.103474;
            const double mouthTopZ = -0.050475;

            Point mouthTop = TryProjectLinkPoint("head_link",
                new Point3D(mouthTopFrontX, 0, mouthTopZ), out Point projectedMouthTop)
                ? projectedMouthTop : new Point(ActualWidth * .50, ActualHeight * .60);

            Point eyePopRobotLeft = TryProjectLinkPoint("head_link",
                new Point3D(headFrontX, +(headSideY + eyePopOutsideMm), mouthTopZ), out Point projectedEyePopLeft)
                ? projectedEyePopLeft : new Point(ActualWidth * .80, mouthTop.Y);
            Point eyePopRobotRight = TryProjectLinkPoint("head_link",
                new Point3D(headFrontX, -(headSideY + eyePopOutsideMm), mouthTopZ), out Point projectedEyePopRight)
                ? projectedEyePopRight : new Point(ActualWidth * .20, mouthTop.Y);

            // Use the physical side points only for X. The common Y is derived
            // from the mouth-top screen coordinate minus half the slider height,
            // which aligns each vertical slider's bottom with the mouth top.
            double eyePopCenterY = mouthTop.Y - _joinedEyePopSlider.Height * .5;
            eyePopRobotLeft.Y = eyePopCenterY;
            eyePopRobotRight.Y = eyePopCenterY;
            eyePopRobotLeft.X -= 50.0;
            eyePopRobotRight.X -= 50.0;

            _joinedEyePopSlider.Visibility = _lrJoined ? Visibility.Visible : Visibility.Collapsed;
            _leftEyePopSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            _rightEyePopSlider.Visibility = !_lrJoined ? Visibility.Visible : Visibility.Collapsed;
            SetCanvasCenter(_joinedEyePopSlider, eyePopRobotRight);
            SetCanvasCenter(_leftEyePopSlider, eyePopRobotLeft);
            SetCanvasCenter(_rightEyePopSlider, eyePopRobotRight);
            PlaceSliderResetButton(_joinedEyePopSlider);
            PlaceSliderResetButton(_leftEyePopSlider);
            PlaceSliderResetButton(_rightEyePopSlider);

            // Antenna groups remain pinned to the top edge at every viewport size.
            const double edgeInset = 8.0;
            const double controlGap = 8.0;
            double whipX = edgeInset + _whipRotateDial.Width + controlGap + _whipRaiseLowerSlider.Width * .5;
            double mfrX = ActualWidth - edgeInset - _mfrRotateDial.Width - controlGap - _mfrUpDownSlider.Width * .5;
            double micX = Math.Max(18.0, rgbLeft - 12.0 - _microphoneRaiseLowerSlider.Width * .5);
            const double antennaTop = 8.0;

            _whipRaiseLowerSlider.Visibility = Visibility.Visible;
            _mfrUpDownSlider.Visibility = Visibility.Visible;
            _microphoneRaiseLowerSlider.Visibility = Visibility.Visible;
            SetCanvasCenter(_whipRaiseLowerSlider,
                new Point(whipX, antennaTop + _whipRaiseLowerSlider.Height * .5));
            SetCanvasCenter(_mfrUpDownSlider,
                new Point(mfrX, antennaTop + _mfrUpDownSlider.Height * .5));
            SetCanvasCenter(_microphoneRaiseLowerSlider,
                new Point(micX, 8.0 + _microphoneRaiseLowerSlider.Height * .5));
            PlaceSliderResetButton(_microphoneRaiseLowerSlider);

            Point whipDialCenter = new(edgeInset + _whipRotateDial.Width * .5,
                                       antennaTop + _whipRotateDial.Height * .5);
            _whipRotateDial.Visibility = Visibility.Visible;
            UpdateWhipRotateDial();
            SetCanvasCenter(_whipRotateDial, whipDialCenter);

            Point mfrDialCenter = new(ActualWidth - edgeInset - _mfrRotateDial.Width * .5,
                                      antennaTop + _mfrRotateDial.Height * .5);
            _mfrRotateDial.Visibility = Visibility.Visible;
            UpdateMfrRotateDial();
            SetCanvasCenter(_mfrRotateDial, mfrDialCenter);

            // Keep height resets below their sliders, inside the viewport edges
            // and clear of the adjacent rotation dials.
            void PlaceAntennaReset(Slider slider)
            {
                if (!_poseSliderResetButtons.TryGetValue(slider, out Button reset)) return;
                reset.Visibility = Visibility.Visible;
                SetCanvasCenter(reset, new Point(Canvas.GetLeft(slider) + slider.Width * .5,
                    Canvas.GetTop(slider) + slider.Height + reset.Height * .5 + 4.0));
            }
            PlaceAntennaReset(_whipRaiseLowerSlider);
            PlaceAntennaReset(_mfrUpDownSlider);

            // Neck controls are anchored to the physical Fabco neck assembly.
            // Tilt follows the centerline of the robot-left cylinder. Nod sits at
            // the front-middle of the assembly, midway between the two cylinders.
            Point leftCylinder = TryProjectFabcoCylinderMidpoint(left: true, out Point leftFabco)
                ? leftFabco : new Point(ActualWidth * .47, ActualHeight * .68);
            Point rightCylinder = TryProjectFabcoCylinderMidpoint(left: false, out Point rightFabco)
                ? rightFabco : new Point(ActualWidth * .53, ActualHeight * .68);
            Point neckFrontMiddle = new((leftCylinder.X + rightCylinder.X) * .5,
                                        (leftCylinder.Y + rightCylinder.Y) * .5);
            _neckNodSlider.Visibility = Visibility.Visible;
            _neckTiltSlider.Visibility = Visibility.Visible;
            SetCanvasCenter(_neckNodSlider, neckFrontMiddle);
            SetCanvasCenter(_neckTiltSlider, leftCylinder);
            PlaceSliderResetButton(_neckNodSlider);
            PlaceSliderResetButton(_neckTiltSlider);

            // Editable NeckTurn dial: 75% of the way from the bottom-center
            // resize-handle reference toward the lower-right URDF legend. In an
            // undocked window the geometric resize-handle location remains the
            // center reference even though the embedded handle itself is hidden.
            Point resizeCenter = new(ActualWidth / 2.0, ActualHeight - 6.0);
            Point legendCenter;
            try
            {
                double sw = Math.Max(1.0, _status.ActualWidth);
                double sh = Math.Max(1.0, _status.ActualHeight);
                legendCenter = _status.TranslatePoint(new Point(sw / 2.0, sh / 2.0), this);
                if (double.IsNaN(legendCenter.X) || double.IsNaN(legendCenter.Y))
                    throw new InvalidOperationException();
            }
            catch
            {
                legendCenter = new Point(ActualWidth - 90, ActualHeight - 45);
            }
            const double dialTowardLegend = 0.75;
            Point dialCenter = new(
                resizeCenter.X + (legendCenter.X - resizeCenter.X) * dialTowardLegend - 50.0,
                resizeCenter.Y + (legendCenter.Y - resizeCenter.Y) * dialTowardLegend);
            dialCenter.X = Math.Clamp(dialCenter.X, _neckTurnDial.Width * .5 + 4,
                                      ActualWidth - _neckTurnDial.Width * .5 - 4);
            dialCenter.Y = Math.Clamp(dialCenter.Y, _neckTurnDial.Height * .5 + 4,
                                      ActualHeight - _neckTurnDial.Height * .5 - 4);
            _neckTurnDial.Visibility = Visibility.Visible;
            UpdateNeckTurnDial();
            SetCanvasCenter(_neckTurnDial, dialCenter);
        }

        /// <summary>
        /// Projects the center and physical robot-left/right lips of the NoseBody
        /// opening. The NoseBasket joint origin is the opening center in
        /// nose_body_link. Robot-left is +Y and robot-right is -Y in this URDF,
        /// matching the eye/neck naming used elsewhere in the model.
        /// </summary>
        private bool TryNoseOpeningGeometry(out Point center, out Point robotLeft, out Point robotRight)
        {
            center = robotLeft = robotRight = new Point();
            var openingCenter = new Point3D(0.0452575, 0.0, 0.0240635);
            const double openingHalfWidth = 0.024;
            if (!TryProjectLinkPoint("nose_body_link", openingCenter, out center))
                return false;

            bool leftOk = TryProjectLinkPoint("nose_body_link",
                new Point3D(openingCenter.X, +openingHalfWidth, openingCenter.Z), out robotLeft);
            bool rightOk = TryProjectLinkPoint("nose_body_link",
                new Point3D(openingCenter.X, -openingHalfWidth, openingCenter.Z), out robotRight);
            return leftOk && rightOk;
        }

        /// <summary>
        /// Projects the physical midpoint of a Fabco cylinder using the CAD ball
        /// centers. The cylinder-body link receives the live Fabco swivel transform,
        /// so this point follows the actual cylinder during neck nod/tilt and yaw.
        /// </summary>
        private bool TryProjectFabcoCylinderMidpoint(bool left, out Point screen)
        {
            Point3D lower = left ? LeftLowerBall : RightLowerBall;
            Point3D upper = left ? LeftUpperBallNeutral : RightUpperBallNeutral;
            var midpoint = new Point3D((lower.X + upper.X) * .5,
                                       (lower.Y + upper.Y) * .5,
                                       (lower.Z + upper.Z) * .5);
            return TryProjectLinkPoint(left ? "left_fabco_body_link" : "right_fabco_body_link",
                                       midpoint, out screen);
        }

        private double EyeTargetRadius(bool robotLeft)
        {
            if (TryEyeTarget(robotLeft, out _, out double radius)) return radius;
            return 36;
        }

        private bool TryEyeTarget(bool robotLeft, out Point center, out double radius)
        {
            center = new Point(); radius = 0;
            string link = robotLeft ? "left_eye_pop_link" : "right_eye_pop_link";
            const double eyeCenterX = 0.0354142;
            if (!TryProjectLinkPoint(link, new Point3D(eyeCenterX, 0, 0), out center)) return false;
            double physicalRadius = 0.027; // inner eye-tube working circle
            bool a = TryProjectLinkPoint(link, new Point3D(eyeCenterX, physicalRadius, 0), out Point py);
            bool b = TryProjectLinkPoint(link, new Point3D(eyeCenterX, 0, physicalRadius), out Point pz);
            double ry = a ? (py - center).Length : 0;
            double rz = b ? (pz - center).Length : 0;
            radius = Math.Clamp((ry + rz) / Math.Max(1, (a ? 1 : 0) + (b ? 1 : 0)), 18, 80);
            return true;
        }

        private void PlaceEyeGazeResetButton(Button button, Point circleCenter, double radius,
                                                  Point insideCenter, bool visible)
        {
            button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) return;
            Vector inward = insideCenter - circleCenter;
            if (inward.Length < 1) inward = new Vector(circleCenter.X < ActualWidth * .5 ? 1 : -1, 0);
            inward.Normalize();
            // Button center is just beyond the circle outline toward the inside of the head.
            Point p = circleCenter + inward * (radius + button.Width * .5 + 3.0);
            p.X = Math.Clamp(p.X, button.Width * .5 + 2.0, ActualWidth - button.Width * .5 - 2.0);
            p.Y = Math.Clamp(p.Y, button.Height * .5 + 2.0, ActualHeight - button.Height * .5 - 2.0);
            SetCanvasCenter(button, p);
        }

        private void PlaceEyeTarget(Ellipse circle, Thumb handle, Point center, double radius,
                                    double h, double v, bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            circle.Visibility = vis;
            handle.Visibility = vis;
            if (!visible) return;
            circle.Width = circle.Height = radius * 2;
            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);
            double x = center.X - Clamp100(h) / 100.0 * radius;
            double y = center.Y - Clamp100(v) / 100.0 * radius;
            // Existing timeline data can contain H/V values whose vector exceeds
            // the circular UI envelope. Display those at the nearest edge.
            Vector vector = new(x - center.X, y - center.Y);
            if (vector.Length > radius && vector.Length > .001)
            {
                vector.Normalize(); vector *= radius;
                x = center.X + vector.X; y = center.Y + vector.Y;
            }
            SetCanvasCenter(handle, new Point(x, y));
        }

        private Point ProjectLinkOr(string linkName, Point fallback) =>
            TryProjectLinkPoint(linkName, new Point3D(), out Point p) ? p : fallback;

        private bool TryProjectLinkPoint(string linkName, Point3D localPoint, out Point screen)
        {
            screen = new Point();
            if (_scene == null || !_scene.TryTransformLinkPoint(linkName, localPoint, out Point3D world)) return false;
            return TryProjectWorldPoint(world, out screen);
        }

        private bool TryProjectWorldPoint(Point3D world, out Point screen)
        {
            screen = new Point();
            double width = ActualWidth, height = ActualHeight;
            if (width <= 1 || height <= 1) return false;

            Vector3D forward = _camera.LookDirection;
            if (forward.LengthSquared < 1e-12) return false;
            forward.Normalize();
            Vector3D up = _camera.UpDirection;
            if (up.LengthSquared < 1e-12) up = new Vector3D(0, 0, 1);
            up.Normalize();
            Vector3D right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared < 1e-12) return false;
            right.Normalize();
            up = Vector3D.CrossProduct(right, forward);
            up.Normalize();

            Vector3D q = world - _camera.Position;
            double depth = Vector3D.DotProduct(q, forward);
            if (depth <= .001) return false;
            double tanH = Math.Tan(_camera.FieldOfView * Deg * .5);
            double tanV = tanH * height / width;
            if (tanH <= 1e-9 || tanV <= 1e-9) return false;
            double nx = Vector3D.DotProduct(q, right) / (depth * tanH);
            double ny = Vector3D.DotProduct(q, up) / (depth * tanV);
            screen = new Point((nx + 1) * width * .5, (1 - ny) * height * .5);
            return double.IsFinite(screen.X) && double.IsFinite(screen.Y);
        }

        private void PlaceThumb(string key, Point center, bool visible)
        {
            if (!_poseThumbs.TryGetValue(key, out var thumb)) return;
            thumb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible) SetCanvasCenter(thumb, center);
        }

        private static Point Offset(Point p, double x, double y) => new(p.X + x, p.Y + y);

        private static void SetCanvasCenter(FrameworkElement element, Point center)
        {
            double w = element.ActualWidth > 0 ? element.ActualWidth : (double.IsNaN(element.Width) ? 0 : element.Width);
            double h = element.ActualHeight > 0 ? element.ActualHeight : (double.IsNaN(element.Height) ? 0 : element.Height);
            Canvas.SetLeft(element, center.X - w / 2.0);
            Canvas.SetTop(element, center.Y - h / 2.0);
        }

        #endregion

        private void LoadUrdf()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Models", "johnny5_head.urdf");
                if (!File.Exists(path))
                    path = Path.Combine(AppContext.BaseDirectory, "johnny5_head.urdf");

                _scene = UrdfScene.Load(path);
                InvalidatePoseCaches();
                var root = new Model3DGroup();
                root.Children.Add(new AmbientLight(Color.FromRgb(92, 96, 108)));
                root.Children.Add(new DirectionalLight(Color.FromRgb(235, 240, 255), new Vector3D(-1.0, 0.4, -0.5)));
                root.Children.Add(new DirectionalLight(Color.FromRgb(125, 145, 175), new Vector3D(0.6, -0.8, -0.2)));

                // Supplemental key light above and to the viewer's left of the
                // neutral forward-facing model.  With the front camera on +X,
                // screen-left is -Y and screen-up is +Z.
                root.Children.Add(new PointLight(Color.FromRgb(245, 248, 255),
                    new Point3D(0.70, -0.50, 0.90))
                {
                    Range = 3.0,
                    ConstantAttenuation = 0.55,
                    LinearAttenuation = 0.20,
                    QuadraticAttenuation = 0.05,
                });

                // Additional upper-left fill light requested in v1.3.1.  It is
                // farther left and slightly higher than the existing key light
                // to brighten the upper-left head/neck surfaces without changing
                // the existing scene-light balance elsewhere.
                root.Children.Add(new PointLight(Color.FromRgb(225, 235, 255),
                    new Point3D(0.45, -0.90, 1.15))
                {
                    Range = 3.2,
                    ConstantAttenuation = 0.70,
                    LinearAttenuation = 0.22,
                    QuadraticAttenuation = 0.05,
                });
                root.Children.Add(_scene.RootModel);
                _viewport.Children.Add(new ModelVisual3D { Content = root });
                _status.Text = "URDF 3-D head\nDrag to orbit\nMouse wheel to zoom\nDouble-click to reset";

                // The imported CAD is authored in its neutral mechanical pose.
                // Keep nose body/basket at zero until the sequence or grid moves them.
                SetServo(ServoNames.NoseBody, 0);
                SetServo(ServoNames.NoseBasket, 0);
                SetServo(ServoNames.IrisClose, 0);
                SetMouth(0);
                UpdatePoseOverlayLayout();
            }
            catch (Exception ex)
            {
                _status.Text = $"Unable to load URDF model: {ex.Message}";
                _status.Foreground = Brushes.OrangeRed;
            }
        }

        public void SetPose(double eyeHLeft, double eyeHRight,
                            double eyeVLeft, double eyeVRight,
                            double irisLeft, double irisRight,
                            double topFlapLeft, double topFlapRight,
                            double bottomFlapLeft, double bottomFlapRight,
                            double tiltLeft, double tiltRight,
                            double ventsLeft, double ventsRight,
                            double neckTilt,
                            double neckNod = 0, ServoNames? neckOwner = null, double neckTurn = 0,
                            double whip = 0, double mic = 0, double mfr = 0,
                            double noseBody = 0,
                            double noseBasket = 0,
                            double leftEyePop = 0, double rightEyePop = 0,
                            double whipRotate = 0, double mfrRotate = 0)
        {
            if (_scene == null || (!_urdfDriveEnabled && !_poseInternalUpdate)) return;
            if (_poseEditEnabled && !_poseInternalUpdate) return;

            if (!_poseInternalUpdate)
                CaptureIncomingPose(eyeHLeft, eyeHRight, eyeVLeft, eyeVRight,
                    irisLeft, irisRight, topFlapLeft, topFlapRight,
                    bottomFlapLeft, bottomFlapRight, tiltLeft, tiltRight,
                    ventsLeft, ventsRight, neckTilt, neckNod, neckOwner, neckTurn,
                    whip, mic, mfr, noseBody, noseBasket, leftEyePop, rightEyePop,
                    whipRotate, mfrRotate);

            long revisionBefore = _poseRevision;
            _suppressCollisionRefresh = true;
            try
            {
            // Inputs named Left/Right here are SCREEN sides, preserving the
            // original RobotHeadView API. Robot-left is screen-right.
            SetControl(ServoNames.EyesHorizontalRight, RobotControls.RightLensHorizontal, eyeHLeft);
            SetControl(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal, eyeHRight);
            SetControl(ServoNames.EyesVerticalUp, RobotControls.RightLensVertical, eyeVLeft);
            SetControl(ServoNames.EyesVerticalUp, RobotControls.LeftLensVertical, eyeVRight);
            SetControl(ServoNames.IrisClose, RobotControls.RightIris, irisLeft);
            SetControl(ServoNames.IrisClose, RobotControls.LeftIris, irisRight);
            SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, topFlapLeft);
            SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, topFlapRight);
            SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, bottomFlapLeft);
            SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, bottomFlapRight);
            SetControl(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, tiltLeft);
            SetControl(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, tiltRight);
            SetControl(ServoNames.VentsOpen, RobotControls.RightEyeVent, ventsLeft);
            SetControl(ServoNames.VentsOpen, RobotControls.LeftEyeVent, ventsRight);

            SetSharedNeckState(neckOwner, neckNod, neckTilt);
            ApplyNeckPose(neckTurn);

            SetServo(ServoNames.Whip_Antenna_RaiseLower, whip);
            SetServo(ServoNames.Whip_Antenna_Rotate, whipRotate);
            SetServo(ServoNames.Microphone_RaiseLower, mic);
            SetServo(ServoNames.MFR_UpDown, mfr);
            SetServo(ServoNames.MFR_Rotate, mfrRotate);
            SetServo(ServoNames.NoseBody, noseBody);
            SetServo(ServoNames.NoseBasket, noseBasket);
            SetServo(ServoNames.LeftEyePop, leftEyePop);
            SetServo(ServoNames.RightEyePop, rightEyePop);
            }
            finally
            {
                _suppressCollisionRefresh = false;
            }
            if (_poseRevision != revisionBefore)
                RefreshCollisionState(allowThrottle: true);
            if (_poseEditEnabled)
                UpdatePoseOverlayLayout();
        }

        public void SetChildServo(ServoNames parentServo, RobotControls control, double value)
        {
            if (_scene == null || (!_urdfDriveEnabled && !_poseInternalUpdate)) return;
            if (_poseEditEnabled && !_poseInternalUpdate) return;
            if (!_poseInternalUpdate) UpdatePoseStateForChild(parentServo, control, value);

            // NeckNodUp and NeckTiltRight take turns driving the SAME two
            // child actuators. Switching logical modes transfers ownership of
            // that shared pair; it does not switch to a second set of URDF
            // child controls. A child-calibration jog starts the newly selected
            // mode from neutral so an old value from the other mode cannot leak
            // into the test.
            if ((parentServo is ServoNames.NeckNodUp or ServoNames.NeckTiltRight) &&
                (control is RobotControls.NeckTiltLeft or RobotControls.NeckTiltRight))
            {
                if (_activeNeckMode != parentServo)
                {
                    _activeNeckMode = parentServo;
                    _neckLeft = UsesCalibratedMotion ? CurrentNeckMotion(parentServo, RobotControls.NeckTiltLeft) : 0;
                    _neckRight = UsesCalibratedMotion ? CurrentNeckMotion(parentServo, RobotControls.NeckTiltRight) : 0;
                }

                value = CalibratedControlValue(parentServo, control, value);
                if (control == RobotControls.NeckTiltLeft) _neckLeft = value;
                else _neckRight = value;
                ApplyNeckPose(null);
                RefreshCollisionState();
                return;
            }

            long revisionBefore = _poseRevision;
            SetControl(parentServo, control, value);
            if (_poseRevision != revisionBefore)
                RefreshCollisionState();
        }

        /// <summary>Replace the visual motion calibration used by this preview.
        /// Travel extents come from URDFconfig.json; child-servo direction is
        /// inherited live from ServoConfig.json.</summary>
        public void SetUrdfConfiguration(UrdfConfiguration configuration)
        {
            _urdfConfiguration = configuration ?? UrdfConfiguration.CreateDefault();
            InvalidatePoseCaches();
            ApplyPoseControlRangesFromUrdf();
            if (_poseEditEnabled) UpdatePoseOverlayLayout();
        }

        public void SetServoConfiguration(ServoConfiguration configuration)
        {
            _servoConfiguration = configuration ?? ServoConfiguration.CreateDefault();
            _motionLimits.Clear(); _motionChannels.Clear(); _stepperMotion.Clear();
            InvalidatePoseCaches();
            RebuildCollisionBaseline();
        }

        /// <summary>True when the current calibrated URDF pose contains at least
        /// one enabled collision warning that was not already an intentional
        /// contact in the calibrated logical-zero pose.</summary>
        public bool HasCollision => _collisionWarningsEnabled && _scene?.HasActiveCollision == true;

        /// <summary>Whether this URDF preview is actively performing collision
        /// warning checks.  The toggle is independent of URDF Drive.</summary>
        public bool CollisionWarningsEnabled => _collisionWarningsEnabled;
        public bool CollisionModelAvailable => _scene != null;

        public void SetCollisionWarningsEnabled(bool enabled)
        {
            if (_collisionWarningsEnabled == enabled) return;
            _collisionWarningsEnabled = enabled;
            UpdateCollisionToggleButton();
            if (enabled)
                RefreshCollisionState();
            else
            {
                _scene?.ClearCollisionState();
                ShowNormalStatus();
            }
            CollisionWarningEnabledChanged?.Invoke(enabled);
        }

        /// <summary>Raised whenever the on-screen Collision Warning toggle
        /// changes.  MainWindow uses this to invalidate stale command warnings
        /// when every available preview has collision checking disabled.</summary>
        public event Action<bool> CollisionWarningEnabledChanged;

        /// <summary>Raised by the lower-left UnDock/Dock button. The host owns
        /// the actual WPF window/layout change so this view stays reusable in
        /// both embedded and detached contexts.</summary>
        public event Action DockToggleRequested;

        /// <summary>Raised while the bottom-center resize handle is dragged in
        /// the embedded editor. The host applies and persists the actual pane height.</summary>
        public event Action<double> VerticalResizeDeltaRequested;

        /// <summary>Configure this preview as the embedded editor view.</summary>
        public void SetDockedHostState()
        {
            _hostIsDocked = true;
            _dockToggleButton.Content = "UnDock";
            _dockToggleButton.ToolTip = "Show the URDF model in a separate window and expand the servo grid.";
            _verticalResizeHandle.Visibility = Visibility.Visible;
        }

        /// <summary>Configure this preview as the detached window view.</summary>
        public void SetDetachedHostState()
        {
            _hostIsDocked = false;
            _dockToggleButton.Content = "Dock";
            _dockToggleButton.ToolTip = "Return the URDF model to the main editor.";
            _verticalResizeHandle.Visibility = Visibility.Collapsed;
        }

        /// <summary>Stable IDs for the collision-shape pairs active in the
        /// current calibrated URDF pose. MainWindow uses the collection when
        /// classifying a command-time pose for the red timeline warning.</summary>
        public IReadOnlyCollection<string> CollisionPairKeys =>
            !_collisionWarningsEnabled
                ? Array.Empty<string>()
                : _scene?.ActiveCollisionPairs ?? Array.Empty<string>();

        private void ShowNormalStatus()
        {
            _status.Text = "URDF 3-D head\nDrag to orbit\nMouse wheel to zoom\nDouble-click to reset";
            _status.Foreground = new SolidColorBrush(Color.FromArgb(205, 225, 232, 242));
        }

        public void RefreshCollisionNow() => RefreshCollisionState();

        private void RefreshCollisionState(bool allowThrottle = false)
        {
            if (_scene == null || _suppressCollisionRefresh) return;

            long now = Environment.TickCount64;
            if (allowThrottle && _collisionWarningsEnabled &&
                now - _lastCollisionRefreshMs < 67)
            {
                _deferredCollisionTimer.Stop();
                _deferredCollisionTimer.Interval = TimeSpan.FromMilliseconds(
                    Math.Max(1, 67 - (now - _lastCollisionRefreshMs)));
                _deferredCollisionTimer.Start();
                return;
            }

            _deferredCollisionTimer.Stop();
            _lastCollisionRefreshMs = now;

            if (!_collisionWarningsEnabled)
            {
                _scene.ClearCollisionState();
                ShowNormalStatus();
                return;
            }

            _scene.UpdateCollisionState(_leftEyePopLogical > 0.0001,
                                        _rightEyePopLogical > 0.0001);

            if (_scene.HasActiveCollision)
            {
                string links = string.Join(" ↔ ", _scene.CollidingLinks.Take(3));
                _status.Text = string.IsNullOrWhiteSpace(links)
                    ? "COLLISION"
                    : "COLLISION\n" + links;
                _status.Foreground = new SolidColorBrush(Color.FromRgb(255, 40, 40));
            }
            else
            {
                ShowNormalStatus();
            }
        }

        /// <summary>Build the collision allow-list from the robot's calibrated
        /// logical-zero pose. Existing contacts at home (hinges, pins, nested
        /// linkage pieces, etc.) are allowed per collision-shape pair, while the
        /// current visible pose is restored immediately afterward.</summary>
        private void RebuildCollisionBaseline()
        {
            if (_scene == null) return;

            var snapshot = _scene.CaptureMotionState();
            double oldNeckLeft = _neckLeft, oldNeckRight = _neckRight;
            ServoNames? oldNeckMode = _activeNeckMode;
            double oldNeckTurn = _lastNeckTurn;
            double oldLeftEyePop = _leftEyePopLogical;
            double oldRightEyePop = _rightEyePopLogical;
            bool oldDrive = _urdfDriveEnabled;
            bool oldSuppress = _suppressCollisionRefresh;
            bool oldInternalUpdate = _poseInternalUpdate;

            _urdfDriveEnabled = true;
            _suppressCollisionRefresh = true;
            _poseInternalUpdate = true;
            try
            {
                _neckLeft = _neckRight = 0;
                _activeNeckMode = null;
                _lastNeckTurn = 0;
                ApplyNeckPose(0);

                SetServo(ServoNames.EyesHorizontalRight, 0);
                SetServo(ServoNames.EyesVerticalUp, 0);
                // Use the normal calibrated mapping here as well: logical zero
                // may no longer be CAD joint zero after URDF calibration.
                SetServo(ServoNames.IrisClose, 0);
                SetServo(ServoNames.FlapsOpen, 0);
                SetServo(ServoNames.FlapTiltUp, 0);
                SetServo(ServoNames.VentsOpen, 0);
                SetServo(ServoNames.NoseBody, 0);
                SetServo(ServoNames.NoseBasket, 0);
                SetServo(ServoNames.MFR_UpDown, 0);
                SetServo(ServoNames.MFR_Rotate, 0);
                SetServo(ServoNames.Microphone_RaiseLower, 0);
                SetServo(ServoNames.Whip_Antenna_RaiseLower, 0);
                SetServo(ServoNames.Whip_Antenna_Rotate, 0);
                SetServo(ServoNames.LeftEyePop, 0);
                SetServo(ServoNames.RightEyePop, 0);

                _scene.EstablishCollisionBaseline();
            }
            finally
            {
                _scene.RestoreMotionState(snapshot);
                _neckLeft = oldNeckLeft;
                _neckRight = oldNeckRight;
                _activeNeckMode = oldNeckMode;
                _lastNeckTurn = oldNeckTurn;
                _leftEyePopLogical = oldLeftEyePop;
                _rightEyePopLogical = oldRightEyePop;
                _urdfDriveEnabled = oldDrive;
                _suppressCollisionRefresh = oldSuppress;
                _poseInternalUpdate = oldInternalUpdate;
                InvalidatePoseCaches();
            }

            RefreshCollisionState();
        }

        private double Motion(ServoNames servo, RobotControls control, double input) =>
            _urdfConfiguration.Map(servo, control, input, _servoConfiguration);

        private void InvalidatePoseCaches()
        {
            _lastControlValues.Clear();
            _lastAppliedNeckMode = null;
            _lastAppliedNeckLeft = _lastAppliedNeckRight = double.NaN;
            _lastAppliedNeckTurn = double.NaN;
            _lastLeftIrisRadius = _lastRightIrisRadius = double.NaN;
            _lastMouthStep = -1;
            _hasRgbFrameHash = false;
        }

        /// <summary>
        /// Voice-amplitude display on the 14 physical front LEDs of the CAD Lip
        /// Light Box. Inactive LED lenses remain a dull orange. As amplitude
        /// rises, pairs brighten smoothly from the two center LEDs outward.
        /// Active LEDs use an emissive orange material, a translucent local halo,
        /// and four localized dynamic point lights that cast orange spill onto
        /// the surrounding mouth/head geometry.
        /// </summary>
        public void SetMouth(double amplitude)
        {
            if (_scene == null || !_urdfDriveEnabled) return;

            // One URDF-calibration gain controls both mouth LED systems.
            // Apply it before clamping so 2.0x reaches full LED response at
            // half-scale input while 0.5x deliberately reduces sensitivity.
            double ledGain = Math.Clamp(_urdfConfiguration?.AudioLedGain ?? 1.0, 0.5, 2.0);
            double a = Math.Clamp(amplitude * ledGain, 0, 1);
            int mouthStep = (int)Math.Round(a * 128.0);
            if (mouthStep == _lastMouthStep) return;
            _lastMouthStep = mouthStep;
            a = mouthStep / 128.0;
            double scaledPairs = a * 7.0;
            Color off = Color.FromRgb(154, 82, 28);       // dull orange lens
            Color on = Color.FromRgb(255, 146, 32);       // bright orange lens
            Color emissive = Color.FromRgb(255, 104, 8);  // hot orange emission

            for (int i = 0; i < 14; i++)
            {
                // 6/7 are the center pair, then 5/8, 4/9 ... 0/13.
                int pairFromCenter = i <= 6 ? 6 - i : i - 7;

                // Smoothly fill the next pair instead of snapping it directly
                // from off to full brightness. This also makes the halo/light
                // output track the audio level continuously.
                double level = Math.Clamp(scaledPairs - pairFromCenter, 0.0, 1.0);
                double glow = Math.Pow(level, 0.72);
                _mouthLevels[i] = glow;

                _scene.SetMaterialColor($"lip_led_{i:00}_dynamic", BlendColor(off, on, level));
                _scene.SetMaterialEmissive($"lip_led_{i:00}_dynamic", ScaleColor(emissive, glow));
                _scene.SetLipHaloIntensity(i, glow);
            }

            _scene.UpdateLipPointLights(_mouthLevels);

            // The red/green side-mouth LED rails are a second audio-level
            // display. They fill from the physical front of the mouth toward
            // the rear, preserving each LED's red or green lens color.
            _scene.SetSideMouthAudioLevel(a);
        }

        private static Color BlendColor(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(from.R + (to.R - from.R) * amount),
                (byte)Math.Round(from.G + (to.G - from.G) * amount),
                (byte)Math.Round(from.B + (to.B - from.B) * amount));
        }

        private static Color ScaleColor(Color color, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(color.R * amount),
                (byte)Math.Round(color.G * amount),
                (byte)Math.Round(color.B * amount));
        }

        public void SetServo(ServoNames servo, double value)
        {
            if (_scene == null || (!_urdfDriveEnabled && !_poseInternalUpdate)) return;
            if (_poseEditEnabled && !_poseInternalUpdate) return;
            if (!_poseInternalUpdate) UpdatePoseStateForServo(servo, value);

            long revisionBefore = _poseRevision;
            switch (servo)
            {
                case ServoNames.NeckTurn:
                    ApplyNeckPose(value);
                    break;

                // These are alternate logical owners of one shared physical
                // neck pair. Whichever command arrives last takes control of
                // the same NeckTiltLeft/NeckTiltRight child values.
                case ServoNames.NeckNodUp:
                case ServoNames.NeckTiltRight:
                    _activeNeckMode = servo;
                    _neckLeft = CalibratedControlValue(servo, RobotControls.NeckTiltLeft, value);
                    _neckRight = CalibratedControlValue(servo, RobotControls.NeckTiltRight, value);
                    ApplyNeckPose(null);
                    break;

                case ServoNames.EyesHorizontalRight:
                    SetControl(ServoNames.EyesHorizontalRight, RobotControls.LeftLensHorizontal, value);
                    SetControl(ServoNames.EyesHorizontalRight, RobotControls.RightLensHorizontal, value);
                    break;
                case ServoNames.EyesVerticalUp:
                    SetControl(ServoNames.EyesVerticalUp, RobotControls.LeftLensVertical, value);
                    SetControl(ServoNames.EyesVerticalUp, RobotControls.RightLensVertical, value);
                    break;
                case ServoNames.IrisClose:
                    SetControl(ServoNames.IrisClose, RobotControls.LeftIris, value);
                    SetControl(ServoNames.IrisClose, RobotControls.RightIris, value);
                    break;
                case ServoNames.FlapsOpen:
                    SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftTopOpen, value);
                    SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightTopOpen, value);
                    SetControl(ServoNames.FlapsOpen, RobotControls.BrowLeftBottomOpen, value);
                    SetControl(ServoNames.FlapsOpen, RobotControls.BrowRightBottomOpen, value);
                    break;
                case ServoNames.FlapTiltUp:
                    SetControl(ServoNames.FlapTiltUp, RobotControls.BrowLeftTopTilt, value);
                    SetControl(ServoNames.FlapTiltUp, RobotControls.BrowRightTopTilt, value);
                    break;
                case ServoNames.VentsOpen:
                    SetControl(ServoNames.VentsOpen, RobotControls.LeftEyeVent, value);
                    SetControl(ServoNames.VentsOpen, RobotControls.RightEyeVent, value);
                    break;
                case ServoNames.NoseBody:
                case ServoNames.NoseBasket:
                case ServoNames.MFR_UpDown:
                case ServoNames.MFR_Rotate:
                case ServoNames.Microphone_RaiseLower:
                case ServoNames.Whip_Antenna_RaiseLower:
                case ServoNames.Whip_Antenna_Rotate:
                    SetControl(servo,
                        (RobotControls)Enum.Parse(typeof(RobotControls), servo.ToString()), value);
                    break;
                case ServoNames.LeftEyePop:
                    SetControl(ServoNames.LeftEyePop, RobotControls.LeftEyePop, value);
                    break;
                case ServoNames.RightEyePop:
                    SetControl(ServoNames.RightEyePop, RobotControls.RightEyePop, value);
                    break;
                case ServoNames.BothEyePop:
                    SetControl(ServoNames.LeftEyePop, RobotControls.LeftEyePop, value);
                    SetControl(ServoNames.RightEyePop, RobotControls.RightEyePop, value);
                    break;
            }

            if (_poseRevision != revisionBefore)
                RefreshCollisionState();
        }

        /// <summary>Apply a timeline/grid pose to the one shared neck actuator pair.
        /// The explicit owner matters even at value 0, because a zero-valued Nod or
        /// Tilt command still transfers ownership to that logical mode.</summary>
        private void SetSharedNeckState(ServoNames? owner, double nodValue, double tiltValue)
        {
            if (owner is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
            {
                _activeNeckMode = owner;
                double value = owner == ServoNames.NeckNodUp ? nodValue : tiltValue;
                _neckLeft = CalibratedControlValue(owner.Value, RobotControls.NeckTiltLeft, value);
                _neckRight = CalibratedControlValue(owner.Value, RobotControls.NeckTiltRight, value);
            }
            else
            {
                _activeNeckMode = null;
                _neckLeft = _neckRight = 0;
            }
        }

        private double _lastNeckTurn;

        private void ApplyNeckPose(double? neckTurn)
        {
            if (_scene == null || (!_urdfDriveEnabled && !_poseInternalUpdate)) return;
            if (neckTurn.HasValue) _lastNeckTurn = CalibratedControlValue(ServoNames.NeckTurn, RobotControls.NeckTurn, neckTurn.Value);

            if (_lastAppliedNeckMode == _activeNeckMode &&
                Math.Abs(_lastAppliedNeckLeft - _neckLeft) < 1e-6 &&
                Math.Abs(_lastAppliedNeckRight - _neckRight) < 1e-6 &&
                Math.Abs(_lastAppliedNeckTurn - _lastNeckTurn) < 1e-6)
                return;
            _lastAppliedNeckMode = _activeNeckMode;
            _lastAppliedNeckLeft = _neckLeft;
            _lastAppliedNeckRight = _neckRight;
            _lastAppliedNeckTurn = _lastNeckTurn;
            _poseRevision++;

            // Visual travel comes from the calibration embedded in the URDF,
            // optionally overridden by URDFconfig.json. Joint origins still come
            // from the CAD/URDF. NeckTurn remains centered on the
            // CAD Disc, while nod/tilt remain centered on the Solid U-Joint
            // hinge intersection.
            // The visual NeckTurn direction is intentionally reversed from the
            // raw calibration mapping so positive/negative URDF turn matches the
            // requested on-screen convention. The editable dial uses the same
            // helper, so its degree readout and model motion stay synchronized.
            _scene.SetJoint("NeckTurn",
                NeckTurnVisualDegrees(_lastNeckTurn) * Deg);

            // NeckNodUp and NeckTiltRight do not have independent child
            // actuators. They take turns interpreting the SAME left/right pair.
            // The active logical owner supplies the gang-relative directions:
            // Nod uses the differential component; Tilt uses the common component.
            double pitch = 0.0;
            double roll = 0.0;
            if (_activeNeckMode is ServoNames.NeckNodUp or ServoNames.NeckTiltRight)
            {
                ServoNames owner = _activeNeckMode.Value;
                double left = Motion(owner, RobotControls.NeckTiltLeft, _neckLeft);
                double right = Motion(owner, RobotControls.NeckTiltRight, _neckRight);

                if (owner == ServoNames.NeckNodUp)
                    pitch = -((left - right) * 0.5) * Deg;
                else
                    roll = ((left + right) * 0.5) * Deg;
            }

            _scene.SetJoint("NeckNodUp", pitch);
            _scene.SetJoint("NeckTiltRight", roll);
            UpdateFabcoKinematics(pitch, roll);
        }

        /// <summary>
        /// Visually solves the two Fabco K-5-X linkages from their CAD ball
        /// centers.  The lower balls stay fixed in neck_yaw_link.  The upper
        /// balls move with the head about the central U-joint.  Each cylinder
        /// body rotates about its lower ball to point at the new upper-ball
        /// position; the piston receives the same rotation plus an axial
        /// translation equal to the change in ball-to-ball distance.
        /// </summary>
        private void UpdateFabcoKinematics(double pitchRadians, double rollRadians)
        {
            if (_scene == null) return;

            var headMotion = new Transform3DGroup();
            headMotion.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), rollRadians / Deg),
                NeckUniversalPivot));
            headMotion.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), pitchRadians / Deg),
                NeckUniversalPivot));

            Point3D leftUpper = headMotion.Transform(LeftUpperBallNeutral);
            Point3D rightUpper = headMotion.Transform(RightUpperBallNeutral);

            ApplyFabcoLinkage("left", LeftLowerBall, LeftUpperBallNeutral, leftUpper);
            ApplyFabcoLinkage("right", RightLowerBall, RightUpperBallNeutral, rightUpper);
        }

        private void ApplyFabcoLinkage(string side, Point3D lower,
                                       Point3D neutralUpper, Point3D currentUpper)
        {
            Vector3D neutral = neutralUpper - lower;
            Vector3D current = currentUpper - lower;
            double neutralLength = neutral.Length;
            double currentLength = current.Length;
            if (neutralLength < 1e-9 || currentLength < 1e-9) return;

            Vector3D axis = Vector3D.CrossProduct(neutral, current);
            double angle = Vector3D.AngleBetween(neutral, current);
            if (axis.LengthSquared < 1e-16)
            {
                // Parallel vectors need no swivel.  The 180-degree case is not
                // reachable within the neck's authored nod/tilt limits.
                axis = new Vector3D(0, 0, 1);
                angle = 0;
            }
            else
            {
                axis.Normalize();
            }

            var swivel = new RotateTransform3D(new AxisAngleRotation3D(axis, angle), lower);
            _scene.SetLinkTransform($"{side}_fabco_body_link", swivel);

            Vector3D direction = current;
            direction.Normalize();
            double extensionDelta = currentLength - neutralLength;
            var piston = new Transform3DGroup();
            piston.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(axis, angle), lower));
            piston.Children.Add(new TranslateTransform3D(direction.X * extensionDelta,
                                                          direction.Y * extensionDelta,
                                                          direction.Z * extensionDelta));
            _scene.SetLinkTransform($"{side}_fabco_piston_link", piston);
        }

        private void SetControl(ServoNames parentServo, RobotControls control, double value)
        {
            if (_scene == null || (!_urdfDriveEnabled && !_poseInternalUpdate)) return;
            if (control == RobotControls.LeftEyePop) parentServo = ServoNames.LeftEyePop;
            if (control == RobotControls.RightEyePop) parentServo = ServoNames.RightEyePop;
            if (!_poseInternalUpdate && !_renderingCalibrated && !UsesCalibratedMotion && CollisionSafeguardActive?.Invoke() == true &&
                !ControllerMotionPathClear(new[] { new CollisionMotionTarget(parentServo, control, value) }, out string reason))
            {
                UpdatePoseStateForChild(parentServo, control, _lastControlValues.GetValueOrDefault((parentServo, control)));
                CollisionSafeguardBlocked?.Invoke(reason); return;
            }
            value = CalibratedControlValue(parentServo, control, value);

            var cacheKey = (parentServo, control);
            if (_lastControlValues.TryGetValue(cacheKey, out double previous) &&
                Math.Abs(previous - value) < 1e-6)
                return;
            _lastControlValues[cacheKey] = value;
            _poseRevision++;

            switch (control)
            {
                case RobotControls.LeftLensHorizontal:
                case RobotControls.RightLensHorizontal:
                    // Horizontal gaze rotates the outer Gimbal Ring about URDF Z.
                    // The Wollensak Raptar lens is a child of that ring, so it
                    // follows the horizontal motion as one assembly.
                    _scene.SetJoint(control.ToString(),
                        Motion(parentServo, control, value) * Deg);
                    break;
                case RobotControls.LeftLensVertical:
                case RobotControls.RightLensVertical:
                    // Vertical gaze rotates only the Wollensak Raptar lens inside
                    // the Gimbal Ring. The URDF +Y axis passes through the two CAD
                    // Gimbal Spacers, which form the lens' vertical-motion pivot.
                    _scene.SetJoint(control.ToString(),
                        Motion(parentServo, control, value) * Deg);
                    break;

                case RobotControls.LeftIris:
                    ApplyIris(parentServo, control, "left", control.ToString(), value);
                    break;
                case RobotControls.RightIris:
                    ApplyIris(parentServo, control, "right", control.ToString(), value);
                    break;

                case RobotControls.BrowLeftTopOpen:
                case RobotControls.BrowRightTopOpen:
                    // Upper and lower CAD assemblies use opposite joint signs
                    // around their servo pinions, but share one configured
                    // semantic FlapsOpen travel range.
                    _scene.SetJoint(control.ToString(), -Motion(parentServo, control, value) * Deg);
                    break;
                case RobotControls.BrowLeftBottomOpen:
                case RobotControls.BrowRightBottomOpen:
                    _scene.SetJoint(control.ToString(), Motion(parentServo, control, value) * Deg);
                    break;

                case RobotControls.BrowLeftTopTilt:
                case RobotControls.BrowRightTopTilt:
                    // The URDF mirrors the right hinge axis.  Both controls therefore
                    // use the same semantic range: -100 = 30 degrees below horizontal,
                    // +100 = vertically inward toward the nose.
                    _scene.SetJoint(control.ToString(), Motion(parentServo, control, value) * Deg);
                    break;

                case RobotControls.LeftEyeVent:
                case RobotControls.RightEyeVent:
                {
                    // SimplifiedHead2 supplies the real Hitec HS-40 output axis plus
                    // all five fin pivot pins in each eye-tube assembly.  One logical
                    // vent value drives the servo horn/pivot strut and all five CAD
                    // fins around those physical axes.
                    double angle = Motion(parentServo, control, value) * Deg;
                    string side = control == RobotControls.LeftEyeVent ? "Left" : "Right";
                    _scene.SetJoint(side + "EyeVent", angle);
                    for (int i = 1; i <= 5; i++)
                        _scene.SetJoint($"{side}EyeVentFin{i}", angle);
                    break;
                }

                case RobotControls.NoseBody:
                    _scene.SetJoint("NoseBody", Motion(parentServo, control, value) * Deg);
                    break;
                case RobotControls.NoseBasket:
                    _scene.SetJoint("NoseBasket", Motion(parentServo, control, value) * Deg);
                    break;

                case RobotControls.MFR_UpDown:
                    _scene.SetJoint("MFR_UpDown", Motion(parentServo, control, value) / 1000.0);
                    break;
                case RobotControls.MFR_Rotate:
                    _scene.SetJoint("MFR_Rotate", Motion(parentServo, control, value) * Deg);
                    break;
                case RobotControls.Whip_Antenna_RaiseLower:
                    ApplyWhipRaiseLower(parentServo, control, value);
                    break;
                case RobotControls.Whip_Antenna_Rotate:
                    _scene.SetJoint("Whip_Antenna_Rotate",
                        Motion(parentServo, control, value) * Deg);
                    break;
                case RobotControls.Microphone_RaiseLower:
                    _scene.SetJoint("Microphone_RaiseLower",
                        Motion(parentServo, control, value) / 1000.0);
                    break;
                case RobotControls.LeftEyePop:
                    _leftEyePopLogical = value;
                    _scene.SetJoint(control.ToString(), Motion(parentServo, control, value) / 1000.0);
                    break;
                case RobotControls.RightEyePop:
                    _rightEyePopLogical = value;
                    _scene.SetJoint(control.ToString(), Motion(parentServo, control, value) / 1000.0);
                    break;
            }
        }

        private void ApplyWhipRaiseLower(ServoNames parentServo, RobotControls control,
                                         double value)
        {
            double liftMm = Motion(parentServo, control, value);
            _scene.SetJoint("Whip_Antenna_RaiseLower", liftMm / 1000.0);

            // Keep the upper linkage vertical while the ASME B18.8.2 hinge is
            // below the visible head-top surface.  As soon as the hinge clears
            // that plane, pivot the complete upper assembly about the real pin.
            // The remaining 2.921 mm of lift brings the lower linkage's flat
            // shoulder to the same surface; at that exact point the upper
            // assembly is horizontal (90 degrees).  Further lift holds 90°.
            double t = (liftMm - WhipFoldStartLiftMm) /
                       (WhipFoldEndLiftMm - WhipFoldStartLiftMm);
            double foldDegrees = Math.Clamp(t, 0.0, 1.0) * 90.0;
            _scene.SetJoint("Whip_Antenna_Fold", foldDegrees * Deg);
        }

        private void ApplyIris(ServoNames parentServo, RobotControls control,
                               string side, string joint, double value)
        {
            // Keep the existing URDF Configuration aperture-percentage model
            // so configured extents and reversal still affect the preview, but
            // convert that percentage into the requested physical opening:
            //   100% => 1.80 in opening (fully open / input -100 by default)
            //    55% => 0.90 in opening (input 0 by default)
            //    10% => 0.30 in opening (minimum / input +100 by default)
            //
            // The conversion is intentionally piecewise-linear because the
            // requested 0.90 in neutral diameter is not the midpoint of the
            // 1.80 in and 0.30 in endpoints.
            double clamped = Math.Clamp(value, -100, 100);
            double aperturePercent = Math.Clamp(
                Motion(parentServo, control, clamped),
                IrisMinimumAperturePercent,
                IrisMaximumAperturePercent);

            double innerDiameterInches;
            if (aperturePercent >= IrisDefaultAperturePercent)
            {
                double t =
                    (aperturePercent - IrisDefaultAperturePercent) /
                    (IrisMaximumAperturePercent - IrisDefaultAperturePercent);
                innerDiameterInches =
                    IrisDefaultInnerDiameterInches +
                    (IrisOuterDiameterInches - IrisDefaultInnerDiameterInches) * t;
            }
            else
            {
                double t =
                    (aperturePercent - IrisMinimumAperturePercent) /
                    (IrisDefaultAperturePercent - IrisMinimumAperturePercent);
                innerDiameterInches =
                    IrisMinimumInnerDiameterInches +
                    (IrisDefaultInnerDiameterInches - IrisMinimumInnerDiameterInches) * t;
            }

            double innerRadiusMetres =
                Math.Clamp(innerDiameterInches,
                           IrisMinimumInnerDiameterInches,
                           IrisOuterDiameterInches) *
                InchToMetres / 2.0;

            // LeftIris/RightIris remain in the joint tree for compatibility.
            _scene.SetJoint(joint, clamped / 100.0);

            ref double lastRadius = ref (side == "left"
                ? ref _lastLeftIrisRadius
                : ref _lastRightIrisRadius);
            // Rebuilding a 72-segment mesh every display frame is far more
            // expensive than moving a joint. A 0.1 mm aperture threshold is
            // visually continuous while reducing mesh churn substantially.
            if (!double.IsNaN(lastRadius) &&
                Math.Abs(lastRadius - innerRadiusMetres) < 0.0001)
                return;
            lastRadius = innerRadiusMetres;

            // Replace the original solid blue cylinder with an annular cylinder
            // whose inner boundary is the visible iris opening. The fixed-size
            // The pupil backing remains behind it; RGB simulation switches
            // that backing between black and transparent as the eye LEDs glow.
            _scene.SetVisualAnnularCylinder(
                $"{side}_iris_disc",
                IrisOuterRadiusMetres,
                innerRadiusMetres,
                IrisThicknessMetres,
                72);
        }

        /// <summary>Apply all 64 Arduino NeoPixel colors to the URDF eye and
        /// vent rings. The front iris backing is opaque black when its eye
        /// ring is dark and fades transparent as that ring emits light.</summary>
        public void SetRgbRingFrame(RgbRingFrame frame)
        {
            if (_scene == null || !_urdfDriveEnabled || frame == null) return;
            double eyeIntensity = Math.Clamp(_urdfConfiguration?.EyeLightIntensity ?? 1.0, 1.0, 20.0);
            double ventIntensity = Math.Clamp(_urdfConfiguration?.VentLightIntensity ?? 1.0, 1.0, 20.0);
            int hash = HashRgbFrame(frame, eyeIntensity, ventIntensity);
            if (_hasRgbFrameHash && hash == _lastRgbFrameHash) return;
            _hasRgbFrameHash = true;
            _lastRgbFrameHash = hash;
            _scene.SetNeoPixelFrame(frame, eyeIntensity, ventIntensity);
        }

        private static int HashRgbFrame(RgbRingFrame frame,
                                        double eyeIntensity, double ventIntensity)
        {
            var hash = new HashCode();
            hash.Add(eyeIntensity);
            hash.Add(ventIntensity);
            foreach (Color color in frame.LeftEye) hash.Add(color);
            foreach (Color color in frame.LeftVent) hash.Add(color);
            foreach (Color color in frame.RightEye) hash.Add(color);
            foreach (Color color in frame.RightVent) hash.Add(color);
            return hash.ToHashCode();
        }

        /// <summary>True when servo/timeline-driven updates are applied to the
        /// WPF 3-D URDF model. Defaults to true for each preview instance.</summary>
        public bool UrdfDriveEnabled => _urdfDriveEnabled;

        /// <summary>Set Drive state programmatically when moving between docked
        /// and detached hosts so the same user setting follows the model.</summary>
        public void SetUrdfDriveEnabled(bool enabled)
        {
            if (_urdfDriveEnabled == enabled) return;
            _urdfDriveEnabled = enabled;
            UpdateDriveToggleButton();
        }

        private void UpdateDriveToggleButton()
        {
            _driveToggleButton.Content = _urdfDriveEnabled ? "Drive: On" : "Drive: Off";
            _driveToggleButton.ToolTip = _urdfDriveEnabled
                ? "URDF model driving is On. Click to freeze servo/timeline/RGB/mouth updates."
                : "URDF model driving is Off. Click to resume servo/timeline/RGB/mouth updates.";

            // Give the current state an obvious visual cue without changing
            // the application's global theme or any other controls.
            _driveToggleButton.Background = new SolidColorBrush(_urdfDriveEnabled
                ? Color.FromRgb(0xC9, 0xED, 0xC5)
                : Color.FromRgb(0xE6, 0xD0, 0xD0));
            _driveToggleButton.Foreground = Brushes.Black;
        }

        private void UpdateCollisionToggleButton()
        {
            _collisionToggleButton.Content = _collisionWarningsEnabled
                ? "Collision Warning: On"
                : "Collision Warning: Off";
            _collisionToggleButton.ToolTip = _collisionWarningsEnabled
                ? "Collision warnings are enabled. Click to stop collision checking and highlighting."
                : "Collision warnings are disabled. Click to resume collision checking and highlighting.";
            _collisionToggleButton.Background = new SolidColorBrush(_collisionWarningsEnabled
                ? Color.FromRgb(0xC9, 0xED, 0xC5)
                : Color.FromRgb(0xE6, 0xD0, 0xD0));
            _collisionToggleButton.Foreground = Brushes.Black;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Overlay controls contain ordinary WPF buttons. Do not start
            // camera orbiting when the user clicks either control stack.
            if (_bottomControls.IsMouseOver || _status.IsMouseOver || _verticalResizeHandle.IsMouseOver ||
                _poseButton.IsMouseOver || (_poseEditEnabled && _poseOverlay.IsMouseOver)) return;

            if (e.ClickCount >= 2)
            {
                ResetCamera();
                return;
            }
            _orbiting = true;
            _lastMouse = e.GetPosition(this);
            CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _orbiting = false;
            ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_orbiting) return;
            Point p = e.GetPosition(this);
            Vector d = p - _lastMouse;
            _lastMouse = p;
            _cameraYaw -= d.X * .008;
            _cameraPitch = Math.Clamp(_cameraPitch + d.Y * .006, -75 * Deg, 75 * Deg);
            UpdateCamera();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _cameraDistance *= e.Delta > 0 ? .88 : 1.14;
            _cameraDistance = Math.Clamp(_cameraDistance, .55, 2.5);
            UpdateCamera();
        }

        /// <summary>Return to a straight-on camera orientation while preserving
        /// the current zoom distance. This is the action exposed by the
        /// on-screen Recenter button.</summary>
        public void RecenterCamera()
        {
            _cameraYaw = 0;
            _cameraPitch = 0;
            UpdateCamera();
        }

        public void TurnCameraDegrees(double degrees)
        {
            _cameraYaw += degrees * Deg;
            UpdateCamera();
        }

        /// <summary>
        /// Save a clean, centered PNG of the current URDF pose for a Library Pose.
        /// Pose/editor chrome is excluded, the camera is temporarily placed in a
        /// straight-on fitted view, and the resulting bitmap is cropped around the
        /// head/flaps/neck.  The user's live camera and UI are restored before this
        /// method returns.
        /// </summary>
        public void SaveCenteredLibraryPoseImage(string destinationPath)
            => SaveLibraryPoseBitmap(CaptureCenteredLibraryPoseImage(), destinationPath);

        internal static void SaveLibraryPoseBitmap(BitmapSource image, string destinationPath)
        {
            string fullPath = Path.GetFullPath(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory);
            using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        internal BitmapSource CaptureCenteredLibraryPoseImage()
        {
            if (_scene == null)
                throw new InvalidOperationException("The URDF model is not loaded.");
            if (ActualWidth <= 2.0 || ActualHeight <= 2.0)
                throw new InvalidOperationException("The URDF display is not large enough to capture an image.");

            IReadOnlyList<Point3D> framingPoints =
                _scene.GetWorldVisualBoundsPoints(LibraryPoseThumbnailLinks);
            if (framingPoints.Count == 0)
                throw new InvalidOperationException("Could not determine the robot-head bounds for the Library Pose image.");

            Visibility poseVisibility = _poseOverlay.Visibility;
            Visibility controlsVisibility = _bottomControls.Visibility;
            Visibility statusVisibility = _status.Visibility;
            Visibility fpsVisibility = _fps.Visibility;
            Visibility resizeVisibility = _verticalResizeHandle.Visibility;

            try
            {
                // A Library Pose picture should look like the normal URDF model,
                // not like the editor. Rendering this RobotHeadView (rather than
                // only Viewport3D) retains its light-blue background.
                _poseOverlay.Visibility = Visibility.Collapsed;
                _bottomControls.Visibility = Visibility.Collapsed;
                _status.Visibility = Visibility.Collapsed;
                _fps.Visibility = Visibility.Collapsed;
                _verticalResizeHandle.Visibility = Visibility.Collapsed;

                double minX = framingPoints.Min(p => p.X);
                double maxX = framingPoints.Max(p => p.X);
                double minY = framingPoints.Min(p => p.Y);
                double maxY = framingPoints.Max(p => p.Y);
                double minZ = framingPoints.Min(p => p.Z);
                double maxZ = framingPoints.Max(p => p.Z);
                var center = new Point3D((minX + maxX) * 0.5,
                                         (minY + maxY) * 0.5,
                                         (minZ + maxZ) * 0.5);

                // Straight-on front view (+X looking toward the head), fitted by
                // binary-searching camera distance against the actual perspective
                // projection. This is independent of the user's current orbit/zoom.
                double halfDepth = Math.Max(0.001, (maxX - minX) * 0.5);
                double low = halfDepth + 0.015;
                double high = Math.Max(0.55, low * 1.5);
                const double fitFraction = 0.88;

                bool Fits(double distance)
                {
                    _camera.Position = new Point3D(center.X + distance, center.Y, center.Z);
                    _camera.LookDirection = center - _camera.Position;
                    _camera.UpDirection = new Vector3D(0, 0, 1);

                    double left = double.PositiveInfinity, top = double.PositiveInfinity;
                    double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
                    foreach (Point3D p in framingPoints)
                    {
                        if (!TryProjectWorldPoint(p, out Point sp)) return false;
                        left = Math.Min(left, sp.X); right = Math.Max(right, sp.X);
                        top = Math.Min(top, sp.Y); bottom = Math.Max(bottom, sp.Y);
                    }
                    double allowedWidth = ActualWidth * fitFraction;
                    double allowedHeight = ActualHeight * fitFraction;
                    return right - left <= allowedWidth && bottom - top <= allowedHeight;
                }

                int growthGuard = 0;
                while (!Fits(high) && growthGuard++ < 24)
                    high *= 1.35;
                if (growthGuard >= 24)
                    throw new InvalidOperationException("Could not fit the robot head into the Library Pose image.");

                for (int i = 0; i < 36; i++)
                {
                    double mid = (low + high) * 0.5;
                    if (Fits(mid)) high = mid;
                    else low = mid;
                }
                Fits(high);

                // Calculate the exact projected crop after fitting, with enough
                // breathing room to keep the outer flaps and the complete neck.
                double cropLeft = double.PositiveInfinity, cropTop = double.PositiveInfinity;
                double cropRight = double.NegativeInfinity, cropBottom = double.NegativeInfinity;
                foreach (Point3D p in framingPoints)
                {
                    if (!TryProjectWorldPoint(p, out Point sp)) continue;
                    cropLeft = Math.Min(cropLeft, sp.X); cropRight = Math.Max(cropRight, sp.X);
                    cropTop = Math.Min(cropTop, sp.Y); cropBottom = Math.Max(cropBottom, sp.Y);
                }
                if (!double.IsFinite(cropLeft) || !double.IsFinite(cropTop))
                    throw new InvalidOperationException("Could not project the robot head for the Library Pose image.");

                double objectWidth = Math.Max(1.0, cropRight - cropLeft);
                double objectHeight = Math.Max(1.0, cropBottom - cropTop);
                double padding = Math.Max(12.0, Math.Min(objectWidth, objectHeight) * 0.045);
                cropLeft = Math.Max(0, cropLeft - padding);
                cropTop = Math.Max(0, cropTop - padding);
                cropRight = Math.Min(ActualWidth, cropRight + padding);
                cropBottom = Math.Min(ActualHeight, cropBottom + padding);

                UpdateLayout();
                int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth));
                int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight));
                var rendered = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96,
                                                      PixelFormats.Pbgra32);
                rendered.Render(this);

                int x = Math.Clamp((int)Math.Floor(cropLeft), 0, pixelWidth - 1);
                int y = Math.Clamp((int)Math.Floor(cropTop), 0, pixelHeight - 1);
                int w = Math.Clamp((int)Math.Ceiling(cropRight) - x, 1, pixelWidth - x);
                int h = Math.Clamp((int)Math.Ceiling(cropBottom) - y, 1, pixelHeight - y);
                var cropped = new CroppedBitmap(rendered, new Int32Rect(x, y, w, h));
                cropped.Freeze();
                return cropped;
            }
            finally
            {
                _poseOverlay.Visibility = poseVisibility;
                _bottomControls.Visibility = controlsVisibility;
                _status.Visibility = statusVisibility;
                _fps.Visibility = fpsVisibility;
                _verticalResizeHandle.Visibility = resizeVisibility;

                // Logical camera values were never changed. Reapply them now so
                // the user returns to precisely the orbit/zoom they had before save.
                UpdateCamera();
                UpdatePoseOverlayLayout();
            }
        }

        private void CaptureOpeningCameraIfNeeded()
        {
            if (_openingCameraCaptured) return;
            _openingCameraYaw = _cameraYaw;
            _openingCameraPitch = _cameraPitch;
            _openingCameraDistance = _cameraDistance;
            _openingCameraCaptured = true;
        }

        public void CaptureCurrentCameraAsOpeningView()
        {
            _openingCameraYaw = _cameraYaw;
            _openingCameraPitch = _cameraPitch;
            _openingCameraDistance = _cameraDistance;
            _openingCameraCaptured = true;
        }

        public void RestoreOpeningCamera()
        {
            if (!_openingCameraCaptured)
                CaptureOpeningCameraIfNeeded();
            _cameraYaw = _openingCameraYaw;
            _cameraPitch = _openingCameraPitch;
            _cameraDistance = _openingCameraDistance;
            UpdateCamera();
        }

        public void CopyCameraFrom(RobotHeadView source, bool makeOpeningView = false)
        {
            if (source == null) return;
            _cameraYaw = source._cameraYaw;
            _cameraPitch = source._cameraPitch;
            _cameraDistance = source._cameraDistance;
            UpdateCamera();
            if (makeOpeningView)
                CaptureCurrentCameraAsOpeningView();
        }

        public (double Yaw, double Pitch, double Distance) GetCameraState() =>
            (_cameraYaw, _cameraPitch, _cameraDistance);

        public void ApplyCameraState(double yaw, double pitch, double distance,
                                     bool makeOpeningView = true)
        {
            if (double.IsFinite(yaw)) _cameraYaw = yaw;
            if (double.IsFinite(pitch)) _cameraPitch = Math.Clamp(pitch, -75 * Deg, 75 * Deg);
            if (double.IsFinite(distance) && distance > 0)
                _cameraDistance = Math.Clamp(distance, .55, 2.5);
            UpdateCamera();
            if (makeOpeningView)
                CaptureCurrentCameraAsOpeningView();
        }

        private void ResetCamera()
        {
            _cameraDistance = 1.15;
            RecenterCamera();
        }

        private void UpdateCamera()
        {
            Point3D cameraTarget = CalculateAnchoredCameraTarget();
            double cp = Math.Cos(_cameraPitch);
            var position = new Point3D(
                cameraTarget.X + _cameraDistance * cp * Math.Cos(_cameraYaw),
                cameraTarget.Y + _cameraDistance * cp * Math.Sin(_cameraYaw),
                cameraTarget.Z + _cameraDistance * Math.Sin(_cameraPitch));
            _camera.Position = position;
            _camera.LookDirection = cameraTarget - position;
            _camera.UpDirection = new Vector3D(0, 0, 1);
            _camera.NearPlaneDistance = .01;
            _camera.FarPlaneDistance = 20;
            UpdatePoseOverlayLayout();
        }

        /// <summary>
        /// Calculates the camera target needed to keep the physical bottom of the
        /// neck exactly 35 pixels above the bottom edge of the URDF viewport.
        /// Perspective projection normally makes that point drift when the camera
        /// distance or viewport aspect ratio changes. Solving the projection for
        /// target Z removes that drift without changing yaw/pitch semantics.
        /// </summary>
        private Point3D CalculateAnchoredCameraTarget()
        {
            double width = _viewport.ActualWidth > 1.0 ? _viewport.ActualWidth : ActualWidth;
            double height = _viewport.ActualHeight > 1.0 ? _viewport.ActualHeight : ActualHeight;
            // During construction WPF has not measured the viewport yet. Use the
            // established opening framing until real viewport dimensions exist.
            if (width <= 1.0 || height <= 1.0)
                return new Point3D(NeckBaseScreenAnchor.X, NeckBaseScreenAnchor.Y, FallbackCameraTargetZ);

            // Anchor the physical neck base to the URDF viewport itself rather
            // than to any overlay controls. This applies identically when docked
            // or undocked and is independent of button-stack height.
            double desiredScreenY = height - NeckBaseBottomAnchor;
            desiredScreenY = Math.Clamp(desiredScreenY, 1.0, height - 1.0);

            // WPF PerspectiveCamera.FieldOfView is horizontal. Convert its tangent
            // to the current vertical field of view before solving for target Z.
            double normalizedY = 1.0 - (2.0 * desiredScreenY / height);
            double tanHorizontalHalfFov = Math.Tan((_camera.FieldOfView * Deg) * 0.5);
            double tanVerticalHalfFov = tanHorizontalHalfFov * (height / width);

            double cp = Math.Cos(_cameraPitch);
            double sp = Math.Sin(_cameraPitch);
            double denominator = cp + normalizedY * tanVerticalHalfFov * sp;
            if (Math.Abs(denominator) < 0.000001)
                return new Point3D(NeckBaseScreenAnchor.X, NeckBaseScreenAnchor.Y, FallbackCameraTargetZ);

            double anchorToTargetZ =
                normalizedY * tanVerticalHalfFov * _cameraDistance / denominator;
            double targetZ = NeckBaseScreenAnchor.Z - anchorToTargetZ;

            return new Point3D(NeckBaseScreenAnchor.X, NeckBaseScreenAnchor.Y, targetZ);
        }
    }

    internal sealed partial class UrdfScene
    {
        private const double Deg = Math.PI / 180.0;

        private readonly Dictionary<string, UrdfJoint> _joints = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Model3DGroup> _links = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GeometryModel3D> _visuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<GeometryModel3D>> _linkVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<GeometryModel3D, (Material Front, Material Back)> _originalMaterials = new();
        private readonly Dictionary<string, SolidColorBrush> _materialBrushes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SolidColorBrush> _emissiveBrushes = new(StringComparer.Ordinal);
        private readonly Dictionary<int, SolidColorBrush> _lipHaloBrushes = new();
        private readonly List<Point3D> _lipLedCenters = new();
        private readonly List<PointLight> _lipPointLights = new();
        private readonly List<SideMouthLed> _sideMouthLeds = new();
        private readonly List<SideMouthLightGroup> _sideMouthLightGroups = new();
        private readonly Dictionary<NeoPixelRingId, List<NeoPixelVisual>> _neoPixelLeds = new();
        private readonly Dictionary<NeoPixelRingId, PointLight> _neoPixelPointLights = new();
        private readonly Dictionary<NeoPixelRingId, List<SpotLight>> _neoPixelCenterLights = new();
        private readonly Dictionary<NeoPixelRingId, List<SpotLight>> _neoPixelVentLights = new();
        private readonly Dictionary<NeoPixelRingId, VentTubeGlowVisual> _neoPixelVentTubeGlows = new();
        private readonly Dictionary<string, MeshGeometry3D> _geometryCache = new(StringComparer.Ordinal);
        private readonly Dictionary<MeshGeometry3D, IReadOnlyList<Rect3D>> _collisionBoundsCache = new();
        private readonly Dictionary<string, UrdfJoint> _parentJointByChild = new(StringComparer.Ordinal);
        private readonly List<CollisionProxy> _collisionProxies = new();
        private readonly HashSet<string> _baselineCollisionPairs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeCollisionPairs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeCollidingLinks = new(StringComparer.Ordinal);
        private readonly HashSet<GeometryModel3D> _highlightedVisuals = new();
        private string _baseDirectory = AppContext.BaseDirectory;
        private UrdfExteriorMeshes _exteriorMeshes = new();
        private readonly Dictionary<GeometryModel3D, GeometryModel3D> _openBackVisuals = new();
        private string _rootLinkName = "";
        private bool _collisionBaselineInitialized;

        private static readonly Material CollisionHighlightMaterial = CreateCollisionHighlightMaterial();

        public Model3DGroup RootModel { get; private set; }
        public bool HasActiveCollision => _activeCollisionPairs.Count > 0;
        internal bool SafeguardAvailable => _collisionBaselineInitialized && _collisionProxies.Count >= 2;
        internal bool HasSafeguardCollision(bool left, bool right) => DetectCollisionPairs(true, left, right).Any();
        public IReadOnlyCollection<string> ActiveCollisionPairs => _activeCollisionPairs.ToArray();
        public IReadOnlyCollection<string> CollidingLinks => _activeCollidingLinks.ToArray();

        public static UrdfScene Load(string path, bool optimizeExterior = true)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("URDF file not found", path);

            var doc = XDocument.Load(path);
            var robot = doc.Root ?? throw new InvalidDataException("URDF has no robot root element.");
            var scene = new UrdfScene
            {
                _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? AppContext.BaseDirectory
            };
            if (optimizeExterior) scene._exteriorMeshes = UrdfExteriorMeshes.Load(path);
            scene.ReadMaterials(robot);
            scene.ReadLinks(robot);
            scene.ReadJoints(robot);
            scene.BuildTree(robot);
            scene.InitializeLipLighting();
            scene.InitializeSideMouthLighting();
            scene.InitializeNeoPixelLighting();
            return scene;
        }

        public void SetJoint(string name, double position)
        {
            if (_joints.TryGetValue(name, out var joint))
                joint.SetPosition(position);
        }

        /// <summary>Transform a point expressed in one URDF link's local
        /// coordinates into the root/world coordinates used by Viewport3D.
        /// Pose-editor overlay controls use this to stay attached to moving
        /// model parts while the camera or joints move.</summary>
        public bool TryTransformLinkPoint(string linkName, Point3D localPoint, out Point3D worldPoint)
        {
            worldPoint = localPoint;
            if (!_links.ContainsKey(linkName)) return false;

            string current = linkName;
            while (true)
            {
                if (_links.TryGetValue(current, out var link) && link.Transform != null)
                    worldPoint = link.Transform.Transform(worldPoint);

                if (!_parentJointByChild.TryGetValue(current, out var parentJoint))
                    break;

                if (parentJoint.Node.Transform != null)
                    worldPoint = parentJoint.Node.Transform.Transform(worldPoint);
                current = parentJoint.ParentLink;
            }
            return true;
        }

        /// <summary>Return world-space corner points for the current visual bounds
        /// of the requested links. Used by Library Pose image framing so the crop
        /// follows the actual current head/flap/neck pose without including
        /// unrelated accessories.</summary>
        public IReadOnlyList<Point3D> GetWorldVisualBoundsPoints(IEnumerable<string> linkNames)
        {
            var result = new List<Point3D>();
            if (linkNames == null) return result;

            foreach (string linkName in linkNames)
            {
                if (string.IsNullOrWhiteSpace(linkName) ||
                    !_linkVisuals.TryGetValue(linkName, out var visuals))
                    continue;

                foreach (GeometryModel3D visual in visuals)
                {
                    MeshGeometry3D mesh = visual.Geometry as MeshGeometry3D;
                    if (mesh == null || mesh.Bounds.IsEmpty) continue;
                    Rect3D b = mesh.Bounds;
                    double x0 = b.X, x1 = b.X + b.SizeX;
                    double y0 = b.Y, y1 = b.Y + b.SizeY;
                    double z0 = b.Z, z1 = b.Z + b.SizeZ;

                    foreach (double x in new[] { x0, x1 })
                    foreach (double y in new[] { y0, y1 })
                    foreach (double z in new[] { z0, z1 })
                    {
                        Point3D local = new Point3D(x, y, z);
                        if (visual.Transform != null)
                            local = visual.Transform.Transform(local);
                        if (TryTransformLinkPoint(linkName, local, out Point3D world))
                            result.Add(world);
                    }
                }
            }
            return result;
        }

        public void SetLinkScale(string name, Vector3D scale)
        {
            if (_links.TryGetValue(name, out var link))
                link.Transform = new ScaleTransform3D(scale.X, scale.Y, scale.Z);
        }

        public void SetVisualAnnularCylinder(string name, double outerRadius,
                                             double innerRadius, double length,
                                             int segments = 72)
        {
            if (_visuals.TryGetValue(name, out var visual))
                visual.Geometry = PrimitiveMeshes.AnnularCylinder(
                    outerRadius, innerRadius, length, segments);
        }

        public void SetLinkTransform(string name, Transform3D transform)
        {
            if (_links.TryGetValue(name, out var link))
                link.Transform = transform ?? Transform3D.Identity;
        }

        public void SetMaterialColor(string name, Color color)
        {
            if (_materialBrushes.TryGetValue(name, out var brush))
                brush.Color = color;
        }

        public void SetMaterialEmissive(string name, Color color)
        {
            if (_emissiveBrushes.TryGetValue(name, out var brush))
                brush.Color = color;
        }

        public void SetLipHaloIntensity(int index, double intensity)
        {
            if (!_lipHaloBrushes.TryGetValue(index, out var brush)) return;
            intensity = Math.Clamp(intensity, 0.0, 1.0);

            // A translucent orange emissive sphere just larger than the LED
            // lens gives a soft glow without requiring a post-processing bloom
            // pass. Keep it invisible when the LED is inactive.
            byte alpha = (byte)Math.Round(74.0 * Math.Pow(intensity, 0.78));
            brush.Color = Color.FromArgb(alpha, 255, 111, 18);
        }

        public void UpdateLipPointLights(IReadOnlyList<double> ledLevels)
        {
            if (_lipPointLights.Count != 4 || ledLevels == null || ledLevels.Count < 14)
                return;

            int[][] groups =
            {
                new[] { 0, 1, 2, 3 },
                new[] { 4, 5, 6 },
                new[] { 7, 8, 9 },
                new[] { 10, 11, 12, 13 },
            };

            for (int g = 0; g < groups.Length; g++)
            {
                double sum = 0.0;
                foreach (int i in groups[g])
                    sum += Math.Clamp(ledLevels[i], 0.0, 1.0);

                double average = sum / groups[g].Length;
                double strength = Math.Pow(average, 0.72) * 0.92;
                _lipPointLights[g].Color = Color.FromRgb(
                    (byte)Math.Round(255 * strength),
                    (byte)Math.Round(118 * strength),
                    (byte)Math.Round(20 * strength));
            }
        }

        private void InitializeLipLighting()
        {
            if (!_links.TryGetValue("head_link", out var headLink)) return;

            _lipLedCenters.Clear();
            _lipHaloBrushes.Clear();
            _lipPointLights.Clear();

            for (int i = 0; i < 14; i++)
            {
                if (!_visuals.TryGetValue($"lip_voice_led_{i:00}", out var visual) ||
                    visual.Geometry == null || visual.Geometry.Bounds.IsEmpty)
                    continue;

                Rect3D b = visual.Geometry.Bounds;
                var center = new Point3D(
                    b.X + b.SizeX / 2.0,
                    b.Y + b.SizeY / 2.0,
                    b.Z + b.SizeZ / 2.0);
                center = visual.Transform?.Transform(center) ?? center;
                _lipLedCenters.Add(center);

                // Halo is centered slightly in front of the physical LED lens
                // (+X is forward for the head model).
                var haloBrush = new SolidColorBrush(Color.FromArgb(0, 255, 111, 18));
                var haloMaterial = new MaterialGroup();
                haloMaterial.Children.Add(new DiffuseMaterial(haloBrush));
                haloMaterial.Children.Add(new EmissiveMaterial(haloBrush));

                double radius = 0.0072;
                var halo = new GeometryModel3D(
                    PrimitiveMeshes.Sphere(radius, 18, 12),
                    haloMaterial)
                {
                    BackMaterial = haloMaterial,
                    Transform = new TranslateTransform3D(center.X + 0.0025, center.Y, center.Z),
                };
                headLink.Children.Add(halo);
                _lipHaloBrushes[i] = haloBrush;
            }

            if (_lipLedCenters.Count != 14)
                return;

            int[][] groups =
            {
                new[] { 0, 1, 2, 3 },
                new[] { 4, 5, 6 },
                new[] { 7, 8, 9 },
                new[] { 10, 11, 12, 13 },
            };

            foreach (int[] indices in groups)
            {
                double x = 0, y = 0, z = 0;
                foreach (int i in indices)
                {
                    x += _lipLedCenters[i].X;
                    y += _lipLedCenters[i].Y;
                    z += _lipLedCenters[i].Z;
                }
                double n = indices.Length;

                var light = new PointLight(
                    Colors.Black,
                    new Point3D(x / n + 0.006, y / n, z / n))
                {
                    Range = 0.16,
                    ConstantAttenuation = 0.72,
                    LinearAttenuation = 7.0,
                    QuadraticAttenuation = 34.0,
                };
                headLink.Children.Add(light);
                _lipPointLights.Add(light);
            }
        }

        public void SetSideMouthAudioLevel(double amplitude)
        {
            if (_sideMouthLeds.Count == 0) return;

            double a = Math.Clamp(amplitude, 0.0, 1.0);
            double scaledStations = a * 12.0;

            foreach (var led in _sideMouthLeds)
            {
                // Station 0 is physically nearest the front of the mouth;
                // station 11 is the rearmost. Smoothly fill the next station.
                double level = Math.Clamp(scaledStations - led.Station, 0.0, 1.0);
                double glow = Math.Pow(level, 0.72);
                led.Level = glow;

                Color off = led.IsRed
                    ? Color.FromRgb(118, 31, 25)
                    : Color.FromRgb(46, 92, 40);
                Color on = led.IsRed
                    ? Color.FromRgb(255, 62, 38)
                    : Color.FromRgb(82, 255, 86);
                Color emissive = led.IsRed
                    ? Color.FromRgb(255, 28, 12)
                    : Color.FromRgb(32, 255, 48);

                SetMaterialColor(led.MaterialName, BlendColorLocal(off, on, level));
                SetMaterialEmissive(led.MaterialName, ScaleColorLocal(emissive, glow));

                byte alpha = (byte)Math.Round(68.0 * Math.Pow(glow, 0.78));
                led.HaloBrush.Color = led.IsRed
                    ? Color.FromArgb(alpha, 255, 38, 16)
                    : Color.FromArgb(alpha, 42, 255, 58);
            }

            UpdateSideMouthPointLights();
        }

        private static Color BlendColorLocal(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(from.R + (to.R - from.R) * amount),
                (byte)Math.Round(from.G + (to.G - from.G) * amount),
                (byte)Math.Round(from.B + (to.B - from.B) * amount));
        }

        private static Color ScaleColorLocal(Color color, double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(color.R * amount),
                (byte)Math.Round(color.G * amount),
                (byte)Math.Round(color.B * amount));
        }

        private void InitializeSideMouthLighting()
        {
            if (!_links.TryGetValue("head_link", out var headLink)) return;

            _sideMouthLeds.Clear();
            _sideMouthLightGroups.Clear();

            AddSideMouthLeds(headLink, isRed: true, columns: 4);
            AddSideMouthLeds(headLink, isRed: false, columns: 2);

            // Four real lights: red + green on each mouth side. Their position
            // follows the weighted center of the currently glowing LEDs, so the
            // light spill itself progresses from front to rear with volume.
            foreach (bool isRed in new[] { true, false })
            {
                foreach (bool positiveSide in new[] { false, true })
                {
                    var members = _sideMouthLeds
                        .Where(l => l.IsRed == isRed && (l.Center.Y >= 0) == positiveSide)
                        .ToList();
                    if (members.Count == 0) continue;

                    var front = members.OrderByDescending(l => l.Center.X).First().Center;
                    var light = new PointLight(Colors.Black,
                        new Point3D(front.X + 0.006, front.Y, front.Z))
                    {
                        Range = 0.17,
                        ConstantAttenuation = 0.72,
                        LinearAttenuation = 7.0,
                        QuadraticAttenuation = 34.0,
                    };
                    headLink.Children.Add(light);
                    _sideMouthLightGroups.Add(new SideMouthLightGroup(isRed, members, light));
                }
            }
        }

        private void AddSideMouthLeds(Model3DGroup headLink, bool isRed, int columns)
        {
            string color = isRed ? "red" : "green";
            for (int station = 0; station < 12; station++)
            {
                for (int column = 0; column < columns; column++)
                {
                    string visualName = $"mouth_side_{color}_s{station:00}_c{column}";
                    string materialName = $"mouth_side_{color}_s{station:00}_c{column}_dynamic";
                    if (!_visuals.TryGetValue(visualName, out var visual) ||
                        visual.Geometry == null || visual.Geometry.Bounds.IsEmpty)
                        continue;

                    Rect3D b = visual.Geometry.Bounds;
                    var center = new Point3D(
                        b.X + b.SizeX / 2.0,
                        b.Y + b.SizeY / 2.0,
                        b.Z + b.SizeZ / 2.0);
                    center = visual.Transform?.Transform(center) ?? center;

                    Color haloColor = isRed
                        ? Color.FromArgb(0, 255, 38, 16)
                        : Color.FromArgb(0, 42, 255, 58);
                    var haloBrush = new SolidColorBrush(haloColor);
                    var haloMaterial = new MaterialGroup();
                    haloMaterial.Children.Add(new DiffuseMaterial(haloBrush));
                    haloMaterial.Children.Add(new EmissiveMaterial(haloBrush));

                    var halo = new GeometryModel3D(
                        PrimitiveMeshes.Sphere(0.0054, 16, 10), haloMaterial)
                    {
                        BackMaterial = haloMaterial,
                        Transform = new TranslateTransform3D(center.X + 0.0020, center.Y, center.Z),
                    };
                    headLink.Children.Add(halo);

                    _sideMouthLeds.Add(new SideMouthLed(
                        station, isRed, materialName, center, haloBrush));
                }
            }
        }

        private void UpdateSideMouthPointLights()
        {
            foreach (var group in _sideMouthLightGroups)
            {
                double total = 0.0;
                double x = 0.0, y = 0.0, z = 0.0;
                double max = 0.0;
                foreach (var led in group.Members)
                {
                    double level = Math.Clamp(led.Level, 0.0, 1.0);
                    if (level <= 0.0001) continue;
                    total += level;
                    max = Math.Max(max, level);
                    x += led.Center.X * level;
                    y += led.Center.Y * level;
                    z += led.Center.Z * level;
                }

                if (total <= 0.0001)
                {
                    group.Light.Color = Colors.Black;
                    continue;
                }

                group.Light.Position = new Point3D(
                    x / total + 0.006,
                    y / total,
                    z / total);

                double coverage = Math.Clamp(total / group.Members.Count, 0.0, 1.0);
                double strength = Math.Clamp(max * (0.50 + 0.50 * coverage), 0.0, 1.0);
                group.Light.Color = group.IsRed
                    ? Color.FromRgb(
                        (byte)Math.Round(255 * strength),
                        (byte)Math.Round(44 * strength),
                        (byte)Math.Round(18 * strength))
                    : Color.FromRgb(
                        (byte)Math.Round(38 * strength),
                        (byte)Math.Round(255 * strength),
                        (byte)Math.Round(54 * strength));
            }
        }

        private sealed class SideMouthLed
        {
            public SideMouthLed(int station, bool isRed, string materialName,
                                Point3D center, SolidColorBrush haloBrush)
            {
                Station = station;
                IsRed = isRed;
                MaterialName = materialName;
                Center = center;
                HaloBrush = haloBrush;
            }

            public int Station { get; }
            public bool IsRed { get; }
            public string MaterialName { get; }
            public Point3D Center { get; set; }
            public SolidColorBrush HaloBrush { get; }
            public double Level { get; set; }
        }

        private sealed class SideMouthLightGroup
        {
            public SideMouthLightGroup(bool isRed, List<SideMouthLed> members, PointLight light)
            {
                IsRed = isRed;
                Members = members;
                Light = light;
            }

            public bool IsRed { get; }
            public List<SideMouthLed> Members { get; }
            public PointLight Light { get; }
        }

        // -----------------------------------------------------------------
        // Arduino NeoPixel eye + vent rings
        // -----------------------------------------------------------------

        /// <summary>Apply the emulated Arduino output to all four 16-pixel
        /// rings. Front Eye output is confined to the iris/diffuser surfaces;
        /// rear Vent output drives a 360-degree inner-tube wash plus localized
        /// radial and rear-facing flood lights.</summary>
        public void SetNeoPixelFrame(RgbRingFrame frame, double eyeIntensity, double ventIntensity)
        {
            eyeIntensity = Math.Clamp(eyeIntensity, 1.0, 20.0);
            ventIntensity = Math.Clamp(ventIntensity, 1.0, 20.0);

            ApplyNeoPixelRing(NeoPixelRingId.LeftEye, frame.LeftEye, eyeIntensity);
            ApplyNeoPixelRing(NeoPixelRingId.LeftVent, frame.LeftVent, ventIntensity);
            ApplyNeoPixelRing(NeoPixelRingId.RightEye, frame.RightEye, eyeIntensity);
            ApplyNeoPixelRing(NeoPixelRingId.RightVent, frame.RightVent, ventIntensity);

            // The old front RGB disc is now a light baffle/diffuser: opaque
            // black when dark, then smoky translucent with a blended glow
            // whenever the corresponding front eye ring is visibly on.
            SetEyeBacking("left_pupil_dynamic", frame.LeftEye, eyeIntensity);
            SetEyeBacking("right_pupil_dynamic", frame.RightEye, eyeIntensity);
        }

        private void InitializeNeoPixelLighting()
        {
            _neoPixelLeds.Clear();
            _neoPixelPointLights.Clear();
            _neoPixelCenterLights.Clear();
            _neoPixelVentLights.Clear();
            _neoPixelVentTubeGlows.Clear();

            // Exact physical positions derived from EyeMechanism.step's two
            // [ELEC-BULB-NEORING16] NeoPixel Ring 16 instances. The product's
            // LED centers lie at radius 18.975 mm. Ring 1 faces forward and is
            // used for the eye; Ring 2 faces rearward and is used for the vent.
            AddNeoPixelRing("left_eye_pop_link", NeoPixelRingId.LeftEye,
                forwardFacing: true);
            AddNeoPixelRing("left_eye_pop_link", NeoPixelRingId.LeftVent,
                forwardFacing: false);
            AddNeoPixelRing("right_eye_pop_link", NeoPixelRingId.RightEye,
                forwardFacing: true);
            AddNeoPixelRing("right_eye_pop_link", NeoPixelRingId.RightVent,
                forwardFacing: false);

            // The eye tubes themselves are fixed to the head, so their 360-degree
            // interior glow is attached to head_link rather than the moving Eye Pop
            // assemblies. The dimensions are derived from the eye-tube CAD: inner
            // radius ~= 42.79 mm, center X ~= 137.625 mm in head-link coordinates.
            AddVentTubeGlow(NeoPixelRingId.LeftVent, +0.099822);
            AddVentTubeGlow(NeoPixelRingId.RightVent, -0.099822);
        }

        private void AddNeoPixelRing(string linkName, NeoPixelRingId id, bool forwardFacing)
        {
            if (!_links.TryGetValue(linkName, out var link)) return;

            const double radius = 0.018975;
            const double ledSize = 0.00455;
            const double ledThickness = 0.00072;

            // LED emitting-face centers use the physical CAD ring centers,
            // while the user's installation assumption defines pixel 0 at
            // exactly 12 o'clock on every ring.
            double x = forwardFacing ? 0.02495 : 0.00789;
            double centerZ = forwardFacing ? 0.0 : 0.00508;
            double startDeg = 0.0;
            double stepDeg = forwardFacing ? 22.5 : -22.5;
            double normal = forwardFacing ? 1.0 : -1.0;

            var leds = new List<NeoPixelVisual>(16);
            for (int i = 0; i < 16; i++)
            {
                // LED 0 is exactly 12 o'clock by configuration assumption.
                // Indices proceed clockwise viewed from the emitting side.
                double angleDeg = startDeg + stepDeg * i;
                double a = angleDeg * Deg;
                double y = radius * Math.Sin(a);
                double z = centerZ + radius * Math.Cos(a);

                var diffuse = new SolidColorBrush(Color.FromRgb(11, 11, 12));
                var emissive = new SolidColorBrush(Colors.Black);
                var material = new MaterialGroup();
                material.Children.Add(new DiffuseMaterial(diffuse));
                material.Children.Add(new EmissiveMaterial(emissive));

                GeometryModel3D lens;
                if (forwardFacing)
                {
                    // The front Eye ring is rendered as sixteen contiguous-looking
                    // annular LED segments rather than sixteen square LED packages.
                    // Each 22.5-degree segment still maps one-to-one to Arduino
                    // pixel 0..15, with a small angular gap so adjacent colors remain
                    // visually distinct through the iris diffuser.
                    const double innerRadius = radius - ledSize / 2.0;
                    const double outerRadius = radius + ledSize / 2.0;
                    const double segmentGapDeg = 1.4;
                    double halfStep = Math.Abs(stepDeg) / 2.0;
                    double segStart = angleDeg - halfStep + segmentGapDeg / 2.0;
                    double segEnd = angleDeg + halfStep - segmentGapDeg / 2.0;
                    lens = new GeometryModel3D(
                        PrimitiveMeshes.AnnularSectorX(innerRadius, outerRadius,
                            ledThickness, segStart, segEnd, 5), material)
                    {
                        BackMaterial = material,
                        Transform = new TranslateTransform3D(x, 0, centerZ),
                    };
                }
                else
                {
                    // Rear Vent rings retain the physical individual NeoPixel
                    // package representation; only the front Eye rings use segments.
                    var transform = new Transform3DGroup();
                    transform.Children.Add(new RotateTransform3D(
                        new AxisAngleRotation3D(new Vector3D(1, 0, 0), angleDeg)));
                    transform.Children.Add(new TranslateTransform3D(x, y, z));
                    lens = new GeometryModel3D(
                        PrimitiveMeshes.Box(ledThickness, ledSize, ledSize), material)
                    {
                        BackMaterial = material,
                        Transform = transform,
                    };
                }
                link.Children.Add(lens);

                var haloBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                if (!forwardFacing)
                {
                    var haloMaterial = new MaterialGroup();
                    haloMaterial.Children.Add(new DiffuseMaterial(haloBrush));
                    haloMaterial.Children.Add(new EmissiveMaterial(haloBrush));
                    var halo = new GeometryModel3D(
                        PrimitiveMeshes.Sphere(0.0105, 14, 10), haloMaterial)
                    {
                        BackMaterial = haloMaterial,
                        Transform = new TranslateTransform3D(
                            x + normal * 0.0017, y, z),
                    };
                    link.Children.Add(halo);
                }

                leds.Add(new NeoPixelVisual(diffuse, emissive, haloBrush));
            }
            _neoPixelLeds[id] = leds;

            // Front Eye rings intentionally do not create WPF lights. Their
            // illumination is confined to the iris-center diffusion disk and the
            // NeoPixel emissive surfaces themselves, so no Eye light can spill onto
            // the eye tube, head shell, gimbal, or surrounding geometry.
            if (forwardFacing)
                return;

            // Rear-facing Vent lighting has two jobs:
            //  1) light the complete 360-degree inner circumference of the tube;
            //  2) throw a broad wash rearward through the vent openings.
            // Use one rear-facing output cone plus sixteen short-range radial cones.
            // The radial cones stop at roughly the inner tube wall, preventing them
            // from intentionally projecting through the tube onto its outside skin.
            var ventLights = new List<SpotLight>(17);

            var centralPosition = new Point3D(x - 0.0045, 0, centerZ);
            var centralSpot = new SpotLight
            {
                Color = Colors.Black,
                Position = centralPosition,
                Direction = new Vector3D(-1.0, 0.0, 0.0),
                InnerConeAngle = 62.0,
                OuterConeAngle = 104.0,
                Range = 0.135,
                ConstantAttenuation = 0.18,
                LinearAttenuation = 2.0,
                QuadraticAttenuation = 5.5,
            };
            link.Children.Add(centralSpot);
            ventLights.Add(centralSpot);

            for (int q = 0; q < 16; q++)
            {
                double angle = q * 22.5 * Deg;
                double sy = radius * 0.96 * Math.Sin(angle);
                double szOffset = radius * 0.96 * Math.Cos(angle);
                var position = new Point3D(x - 0.0010, sy, centerZ + szOffset);

                // Nearly radial direction with a small rearward component. From
                // the 18.975-mm LED radius to the 42.79-mm inner wall is about
                // 23.8 mm, so a 27-mm range gives strong wall illumination without
                // deliberately extending far beyond the physical tube wall.
                var direction = new Vector3D(
                    -0.16,
                    Math.Sin(angle),
                    Math.Cos(angle));
                direction.Normalize();

                var spot = new SpotLight
                {
                    Color = Colors.Black,
                    Position = position,
                    Direction = direction,
                    InnerConeAngle = 44.0,
                    OuterConeAngle = 78.0,
                    Range = 0.027,
                    ConstantAttenuation = 0.14,
                    LinearAttenuation = 1.8,
                    QuadraticAttenuation = 4.8,
                };
                link.Children.Add(spot);
                ventLights.Add(spot);
            }

            _neoPixelVentLights[id] = ventLights;
        }

        private void AddVentTubeGlow(NeoPixelRingId id, double centerY)
        {
            if (!_links.TryGetValue("head_link", out var head)) return;

            // Slightly inset from the CAD inner radius to avoid z-fighting while
            // keeping the emissive surface visually on the inside wall.
            const double innerGlowRadius = 0.04245;
            const double glowLength = 0.0910;
            const double centerX = 0.137625;

            var diffuse = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            var emissive = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(diffuse));
            material.Children.Add(new EmissiveMaterial(emissive));

            var transform = new Transform3DGroup();
            // Primitive cylinder axis is +Z; rotate it to the eye-tube +X axis.
            transform.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90.0)));
            transform.Children.Add(new TranslateTransform3D(centerX, centerY, 0.0));

            var shell = new GeometryModel3D(
                PrimitiveMeshes.OpenCylinder(innerGlowRadius, glowLength, 96), material)
            {
                BackMaterial = material,
                Transform = transform,
            };
            head.Children.Add(shell);
            _neoPixelVentTubeGlows[id] = new VentTubeGlowVisual(diffuse, emissive);
        }

        private void ApplyNeoPixelRing(NeoPixelRingId id, IReadOnlyList<Color> colors, double intensityMultiplier)
        {
            if (!_neoPixelLeds.TryGetValue(id, out var leds) || colors == null) return;
            intensityMultiplier = Math.Clamp(intensityMultiplier, 1.0, 20.0);
            int n = Math.Min(leds.Count, colors.Count);
            double sumR = 0, sumG = 0, sumB = 0;
            double maxLevel = 0;

            for (int i = 0; i < n; i++)
            {
                Color c = colors[i];
                double level = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0;
                maxLevel = Math.Max(maxLevel, level);
                var led = leds[i];

                if (level <= 0.001)
                {
                    led.Diffuse.Color = Color.FromRgb(11, 11, 12);
                    led.Emissive.Color = Colors.Black;
                    led.Halo.Color = Color.FromArgb(0, 0, 0, 0);
                    continue;
                }

                // The Arduino frame already contains brightness scaling. Boost
                // only the rendered emission so the physical command semantics
                // remain unchanged while the eye LEDs look more luminous.
                Color hue = NormalizeHue(c);
                led.Diffuse.Color = BlendColorLocal(c, hue, 0.22 + 0.20 * level);
                double emissiveBoost = id is NeoPixelRingId.LeftVent or NeoPixelRingId.RightVent ? 3.10 : 1.75;
                // Surface emission is also boosted, but with square-root scaling
                // so the 20x light setting retains visible RGB detail instead of
                // immediately clipping every LED surface to white/full channel.
                led.Emissive.Color = BoostColor(c, emissiveBoost * Math.Sqrt(intensityMultiplier));

                bool ventRing = id is NeoPixelRingId.LeftVent or NeoPixelRingId.RightVent;
                byte alpha = ventRing
                    ? (byte)Math.Clamp(Math.Round(190.0 * Math.Pow(level, 0.52) * Math.Sqrt(intensityMultiplier)), 0, 250)
                    : (byte)0;
                led.Halo.Color = Color.FromArgb(alpha, hue.R, hue.G, hue.B);

                sumR += c.R;
                sumG += c.G;
                sumB += c.B;
            }

            Color pooledColor = Colors.Black;
            if (maxLevel > 0.001)
            {
                // Average color establishes hue; stronger front-eye gain makes
                // the ring illuminate the iris rather than reading as 16 dim dots.
                // Environmental spill is intentionally restrained. Front-eye
                // light is concentrated by the inward spotlights and diffuser
                // disk, while vent rings get a stronger but still localized
                // pooled light so they read clearly through the vents.
                double gain = id is NeoPixelRingId.LeftEye or NeoPixelRingId.RightEye ? 1.35 : 5.60;
                double boost = id is NeoPixelRingId.LeftEye or NeoPixelRingId.RightEye
                    ? (0.18 + 0.32 * maxLevel)
                    : (0.52 + 0.95 * maxLevel);
                pooledColor = Color.FromRgb(
                    (byte)Math.Clamp(Math.Round((sumR / Math.Max(1, n)) * gain * boost), 0, 255),
                    (byte)Math.Clamp(Math.Round((sumG / Math.Max(1, n)) * gain * boost), 0, 255),
                    (byte)Math.Clamp(Math.Round((sumB / Math.Max(1, n)) * gain * boost), 0, 255));
            }

            if (_neoPixelVentLights.TryGetValue(id, out var ventLights))
            {
                // Central cone drives light out through the vent openings.
                if (ventLights.Count > 0)
                {
                    ventLights[0].Color = pooledColor;
                    ventLights[0].ConstantAttenuation = 0.18 / intensityMultiplier;
                    ventLights[0].LinearAttenuation = 2.0 / intensityMultiplier;
                    ventLights[0].QuadraticAttenuation = 5.5 / intensityMultiplier;
                }

                // One radial light per NeoPixel paints the complete inner tube
                // circumference. Keep each range short and use that pixel's own
                // color so wipe/rainbow/chase animations still travel around the
                // physical ring rather than becoming one flat flood color.
                for (int q = 0; q < 16 && q + 1 < ventLights.Count; q++)
                {
                    var spot = ventLights[q + 1];
                    Color source = q < n ? colors[q] : Colors.Black;
                    spot.Color = Color.FromRgb(
                        (byte)Math.Clamp(Math.Round(source.R * 6.4), 0, 255),
                        (byte)Math.Clamp(Math.Round(source.G * 6.4), 0, 255),
                        (byte)Math.Clamp(Math.Round(source.B * 6.4), 0, 255));
                    spot.ConstantAttenuation = 0.14 / intensityMultiplier;
                    spot.LinearAttenuation = 1.8 / intensityMultiplier;
                    spot.QuadraticAttenuation = 4.8 / intensityMultiplier;
                }
            }

            if (_neoPixelVentTubeGlows.TryGetValue(id, out var tubeGlow))
            {
                if (maxLevel <= 0.001)
                {
                    tubeGlow.Diffuse.Color = Color.FromArgb(0, 0, 0, 0);
                    tubeGlow.Emissive.Color = Color.FromArgb(0, 0, 0, 0);
                }
                else
                {
                    Color hue = NormalizeHue(pooledColor);
                    double strength = Math.Min(1.0, maxLevel * Math.Sqrt(intensityMultiplier));
                    byte diffuseAlpha = (byte)Math.Clamp(Math.Round(38.0 + 58.0 * strength), 0, 110);
                    byte emissiveAlpha = (byte)Math.Clamp(Math.Round(120.0 + 115.0 * strength), 0, 245);
                    tubeGlow.Diffuse.Color = Color.FromArgb(
                        diffuseAlpha,
                        (byte)Math.Clamp(Math.Round(hue.R * 0.28 * strength), 0, 255),
                        (byte)Math.Clamp(Math.Round(hue.G * 0.28 * strength), 0, 255),
                        (byte)Math.Clamp(Math.Round(hue.B * 0.28 * strength), 0, 255));
                    tubeGlow.Emissive.Color = Color.FromArgb(
                        emissiveAlpha,
                        (byte)Math.Clamp(Math.Round(hue.R * 0.78 * strength), 0, 255),
                        (byte)Math.Clamp(Math.Round(hue.G * 0.78 * strength), 0, 255),
                        (byte)Math.Clamp(Math.Round(hue.B * 0.78 * strength), 0, 255));
                }
            }
        }

        private void SetEyeBacking(string materialName, IReadOnlyList<Color> colors, double intensityMultiplier)
        {
            intensityMultiplier = Math.Clamp(intensityMultiplier, 1.0, 20.0);
            double sumR = 0, sumG = 0, sumB = 0;
            int litCount = 0;
            double maxLevel = 0;

            if (colors != null)
            {
                foreach (var c in colors)
                {
                    double level = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0;
                    if (level < 2.0 / 255.0) continue;
                    sumR += c.R;
                    sumG += c.G;
                    sumB += c.B;
                    maxLevel = Math.Max(maxLevel, level);
                    litCount++;
                }
            }

            if (litCount == 0)
            {
                SetMaterialColor(materialName, Color.FromArgb(255, 0, 0, 0));
                SetMaterialEmissive(materialName, Colors.Black);
                return;
            }

            Color average = Color.FromRgb(
                (byte)Math.Clamp(Math.Round(sumR / litCount), 0, 255),
                (byte)Math.Clamp(Math.Round(sumG / litCount), 0, 255),
                (byte)Math.Clamp(Math.Round(sumB / litCount), 0, 255));
            Color hue = NormalizeHue(average);

            // A smoky translucent diffuser obscures the individual LED packages
            // while allowing their light through. The subtle averaged emissive
            // tint visually blends adjacent NeoPixel colors across the disk.
            byte alpha = (byte)Math.Round(150.0 - 38.0 * maxLevel); // 112..150
            byte tint = (byte)Math.Round(12.0 + 16.0 * maxLevel);
            SetMaterialColor(materialName, Color.FromArgb(alpha, tint, tint, tint));
            double diffuserBoost = Math.Sqrt(intensityMultiplier);
            SetMaterialEmissive(materialName, Color.FromRgb(
                (byte)Math.Clamp(Math.Round(hue.R * 0.34 * maxLevel * diffuserBoost), 0, 255),
                (byte)Math.Clamp(Math.Round(hue.G * 0.34 * maxLevel * diffuserBoost), 0, 255),
                (byte)Math.Clamp(Math.Round(hue.B * 0.34 * maxLevel * diffuserBoost), 0, 255)));
        }

        private static Color NormalizeHue(Color c)
        {
            // Preserve the RGB channel ratios while normalizing the strongest
            // component to full intensity. This separates hue from the Arduino
            // brightness already encoded in the ring frame.
            double maxChannel = Math.Max(c.R, Math.Max(c.G, c.B));
            if (maxChannel <= 0.0)
                return Colors.Black;

            double scale = 255.0 / maxChannel;
            return Color.FromRgb(
                (byte)Math.Clamp(Math.Round(c.R * scale), 0, 255),
                (byte)Math.Clamp(Math.Round(c.G * scale), 0, 255),
                (byte)Math.Clamp(Math.Round(c.B * scale), 0, 255));
        }

        private static Color BoostColor(Color c, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Clamp(Math.Round(c.R * factor), 0, 255),
                (byte)Math.Clamp(Math.Round(c.G * factor), 0, 255),
                (byte)Math.Clamp(Math.Round(c.B * factor), 0, 255));
        }

        private enum NeoPixelRingId
        {
            LeftEye,
            LeftVent,
            RightEye,
            RightVent,
        }

        private sealed class VentTubeGlowVisual
        {
            public VentTubeGlowVisual(SolidColorBrush diffuse, SolidColorBrush emissive)
            {
                Diffuse = diffuse;
                Emissive = emissive;
            }

            public SolidColorBrush Diffuse { get; }
            public SolidColorBrush Emissive { get; }
        }

        private sealed class NeoPixelVisual
        {
            public NeoPixelVisual(SolidColorBrush diffuse, SolidColorBrush emissive,
                                  SolidColorBrush halo)
            {
                Diffuse = diffuse;
                Emissive = emissive;
                Halo = halo;
            }
            public SolidColorBrush Diffuse { get; }
            public SolidColorBrush Emissive { get; }
            public SolidColorBrush Halo { get; }
        }

        /// <summary>
        /// Captures only the kinematic state that can change while the editor is
        /// running. This is used when rebuilding the neutral collision-contact
        /// baseline from the calibrated logical-zero pose without disturbing the
        /// pose currently visible to the user.
        /// </summary>
        public MotionSnapshot CaptureMotionState()
        {
            var joints = _joints.ToDictionary(kv => kv.Key, kv => kv.Value.Position,
                                              StringComparer.Ordinal);
            var links = new Dictionary<string, Matrix3D>(StringComparer.Ordinal);
            foreach (var kv in _links)
                links[kv.Key] = kv.Value.Transform?.Value ?? Matrix3D.Identity;
            return new MotionSnapshot(joints, links);
        }

        public void RestoreMotionState(MotionSnapshot snapshot)
        {
            if (snapshot == null) return;
            foreach (var kv in snapshot.JointPositions)
                if (_joints.TryGetValue(kv.Key, out var joint))
                    joint.SetPosition(kv.Value);
            foreach (var kv in snapshot.LinkTransforms)
                if (_links.TryGetValue(kv.Key, out var link))
                    link.Transform = new MatrixTransform3D(kv.Value);
        }

        /// <summary>
        /// Treat collision-proxy contacts present in the calibrated logical-zero
        /// pose as intentional/mechanical contacts. The allow-list is per
        /// collision shape rather than per link, so a flap can legitimately
        /// touch its hinge while still being checked against every other piece
        /// of the head.
        /// </summary>
        public void EstablishCollisionBaseline()
        {
            _baselineCollisionPairs.Clear();
            foreach (var hit in DetectCollisionPairs(ignoreBaseline: false, leftEyePoppedOut: false, rightEyePoppedOut: false))
                _baselineCollisionPairs.Add(hit.PairKey);
            _collisionBaselineInitialized = true;
            _collisionModel = null; // Rebuild cached baseline exclusions after calibration changes.
            _warningCollisionSession = null;
            ClearCollisionHighlights();
            _activeCollisionPairs.Clear();
            _activeCollidingLinks.Clear();
        }

        /// <summary>Remove all active collision state/highlighting without
        /// changing the baseline or the URDF pose.</summary>
        public void ClearCollisionState()
        {
            ClearCollisionHighlights();
            _activeCollisionPairs.Clear();
            _activeCollidingLinks.Clear();
        }

        /// <summary>Recalculate active collisions for the current joint pose and
        /// highlight the exact visual meshes represented by the colliding
        /// collision proxies.</summary>
        public void UpdateCollisionState(bool leftEyePoppedOut, bool rightEyePoppedOut)
        {
            if (!_collisionBaselineInitialized || _collisionProxies.Count < 2)
            {
                ClearCollisionState();
                return;
            }

            var hits = DetectCollisionPairs(ignoreBaseline: true,
                                            leftEyePoppedOut, rightEyePoppedOut);
            _activeCollisionPairs.Clear();
            _activeCollidingLinks.Clear();
            var visuals = new HashSet<GeometryModel3D>();

            foreach (var hit in hits)
            {
                _activeCollisionPairs.Add(hit.PairKey);
                _activeCollidingLinks.Add(hit.A.LinkName);
                _activeCollidingLinks.Add(hit.B.LinkName);

                AddHighlightVisual(hit.A, visuals);
                AddHighlightVisual(hit.B, visuals);
            }

            ApplyCollisionHighlights(visuals);
        }

        private static Material CreateCollisionHighlightMaterial()
        {
            var diffuseBrush = new SolidColorBrush(Color.FromRgb(255, 24, 24));
            var emissiveBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            diffuseBrush.Freeze();
            emissiveBrush.Freeze();

            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(diffuseBrush));
            group.Children.Add(new EmissiveMaterial(emissiveBrush));
            group.Freeze();
            return group;
        }

        private void AddHighlightVisual(CollisionProxy proxy, HashSet<GeometryModel3D> result)
        {
            // The Hitec HS-85BB servos and their upper-flap carrier hardware are
            // intentionally diagnostic-only visuals. They are not collision
            // geometry and must never be painted red by a collision warning.
            if (proxy.LinkName is "left_top_carrier_link" or "right_top_carrier_link" ||
                proxy.Name.Contains("servo_bracket_hardware", StringComparison.OrdinalIgnoreCase))
                return;

            if (proxy.SourceVisual != null)
            {
                result.Add(proxy.SourceVisual);
                return;
            }

            if (_linkVisuals.TryGetValue(proxy.LinkName, out var linkVisuals))
                foreach (var visual in linkVisuals)
                    result.Add(visual);
        }

        private void ApplyCollisionHighlights(HashSet<GeometryModel3D> desired)
        {
            foreach (var visual in desired.ToArray())
                if (_openBackVisuals.TryGetValue(visual, out var back)) desired.Add(back);
            foreach (var visual in _highlightedVisuals.ToArray())
            {
                if (_originalMaterials.TryGetValue(visual, out var original))
                {
                    visual.Material = original.Front;
                    visual.BackMaterial = original.Back;
                }
            }
            _highlightedVisuals.Clear();

            foreach (var visual in desired)
            {
                if (visual.Material != null) visual.Material = CollisionHighlightMaterial;
                if (visual.BackMaterial != null) visual.BackMaterial = CollisionHighlightMaterial;
                _highlightedVisuals.Add(visual);
            }
        }

        private void ClearCollisionHighlights() =>
            ApplyCollisionHighlights(new HashSet<GeometryModel3D>());

        private void ReadMaterials(XElement robot)
        {
            foreach (var m in robot.Elements("material"))
            {
                string name = Attr(m, "name");
                string rgba = m.Element("color")?.Attribute("rgba")?.Value ?? ".7 .7 .7 1";
                _materialBrushes[name] = new SolidColorBrush(ParseColor(rgba));
            }
        }

        private void ReadLinks(XElement robot)
        {
            foreach (var e in robot.Elements("link"))
            {
                string name = Attr(e, "name");
                var group = new Model3DGroup();
                var linkVisuals = new List<GeometryModel3D>();

                foreach (var visual in e.Elements("visual"))
                {
                    var model = CreateVisual(visual, name, out var openBack);
                    if (model != null)
                    {
                        group.Children.Add(model);
                        linkVisuals.Add(model);
                        _originalMaterials[model] = (model.Material, model.BackMaterial);
                        if (openBack != null)
                        {
                            group.Children.Add(openBack); linkVisuals.Add(openBack);
                            _originalMaterials[openBack] = (openBack.Material, openBack.BackMaterial);
                            _openBackVisuals[model] = openBack;
                        }

                        string visualName = visual.Attribute("name")?.Value ?? "";
                        if (!string.IsNullOrWhiteSpace(visualName))
                            _visuals[visualName] = model;
                    }
                }

                _links[name] = group;
                _linkVisuals[name] = linkVisuals;

                int collisionIndex = 0;
                foreach (var collision in e.Elements("collision"))
                {
                    foreach (var proxy in CreateCollisionProxies(name, collision, collisionIndex++))
                        _collisionProxies.Add(proxy);
                }
            }
        }

        private GeometryModel3D CreateVisual(XElement visual, string linkName, out GeometryModel3D openBack)
        {
            openBack = null;
            var geometry = visual.Element("geometry");
            if (geometry == null) return null;

            MeshGeometry3D mesh = CreateGeometry(geometry);
            if (mesh == null) return null;

            string materialName = visual.Element("material")?.Attribute("name")?.Value ?? "";
            if (!_materialBrushes.TryGetValue(materialName, out var brush))
                brush = new SolidColorBrush(Color.FromRgb(170, 175, 185));

            var materials = new MaterialGroup();
            materials.Children.Add(new DiffuseMaterial(brush));
            bool isPupilBacking = materialName is "left_pupil_dynamic" or "right_pupil_dynamic";
            if (!isPupilBacking)
            {
                materials.Children.Add(new SpecularMaterial(
                    new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), 32));
            }
            else
            {
                // The pupil backing doubles as the front-eye diffusion disk.
                // Its emissive tint is driven from the blended NeoPixel frame.
                var diffuserEmissive = new SolidColorBrush(Colors.Black);
                materials.Children.Add(new EmissiveMaterial(diffuserEmissive));
                _emissiveBrushes[materialName] = diffuserEmissive;
            }

            // Lip voice LEDs get a separately mutable emissive channel. The
            // diffuse color keeps inactive lenses visibly dull orange, while
            // SetMouth drives emission independently as the audio level rises.
            if ((materialName.StartsWith("lip_led_", StringComparison.Ordinal) ||
                 materialName.StartsWith("mouth_side_red_", StringComparison.Ordinal) ||
                 materialName.StartsWith("mouth_side_green_", StringComparison.Ordinal) ||
                 materialName.StartsWith("neopixel_", StringComparison.Ordinal)) &&
                materialName.EndsWith("_dynamic", StringComparison.Ordinal))
            {
                var emissiveBrush = new SolidColorBrush(Colors.Black);
                materials.Children.Add(new EmissiveMaterial(emissiveBrush));
                _emissiveBrushes[materialName] = emissiveBrush;
            }

            var model = new GeometryModel3D(mesh, materials) { BackMaterial = materials };
            model.Transform = ParseOriginTransform(visual.Element("origin"));
            string source = geometry.Element("mesh")?.Attribute("filename")?.Value;
            string key = linkName + "/" + (visual.Attribute("name")?.Value ?? "");
            if (_exteriorMeshes.TryBackFaces(key, source, mesh, out var backMesh))
            {
                model.BackMaterial = null;
                if (backMesh != null) openBack = new GeometryModel3D(backMesh, null)
                { BackMaterial = materials, Transform = model.Transform };
            }
            return model;
        }

        private IEnumerable<CollisionProxy> CreateCollisionProxies(string linkName,
                                                                       XElement collision,
                                                                       int collisionIndex)
        {
            var geometry = collision.Element("geometry");
            if (geometry == null) yield break;

            MeshGeometry3D mesh = CreateGeometry(geometry);
            if (mesh == null || mesh.Bounds.IsEmpty) yield break;

            string collisionName = collision.Attribute("name")?.Value ?? $"collision_{collisionIndex:00}";
            string sourceName = collisionName.StartsWith("auto_collision_", StringComparison.Ordinal)
                ? collisionName.Substring("auto_collision_".Length)
                : "";
            GeometryModel3D sourceVisual = null;
            if (!string.IsNullOrWhiteSpace(sourceName))
                _visuals.TryGetValue(sourceName, out sourceVisual);

            // A number of CAD export meshes group physically separate pieces
            // merely because they share a material/color. One bounding box for
            // such a mesh would fill large empty regions and create false
            // collisions. Subdivide the triangle cloud spatially into a small
            // set of conservative boxes. Simple/small geometry remains one box.
            if (!_collisionBoundsCache.TryGetValue(mesh, out var bounds))
            {
                bounds = BuildCollisionBounds(mesh);
                _collisionBoundsCache[mesh] = bounds;
            }
            int part = 0;
            foreach (Rect3D localBounds in bounds)
            {
                yield return new CollisionProxy(
                    $"{linkName}/{collisionName}/{collisionIndex}/part{part:00}",
                    $"{collisionName}/part{part:00}",
                    linkName,
                    localBounds,
                    ParseOriginTransform(collision.Element("origin")),
                    sourceVisual);
                part++;
            }
        }

        private static IReadOnlyList<Rect3D> BuildCollisionBounds(MeshGeometry3D mesh)
        {
            if (mesh == null || mesh.Bounds.IsEmpty || mesh.TriangleIndices.Count < 3)
                return mesh == null || mesh.Bounds.IsEmpty
                    ? Array.Empty<Rect3D>()
                    : new[] { mesh.Bounds };

            int triangleCount = mesh.TriangleIndices.Count / 3;
            if (triangleCount <= 300)
                return new[] { mesh.Bounds };

            // Large CAD/material meshes sometimes contain several physically
            // separate pieces. Partition triangle envelopes into the eight
            // octants around the mesh-bounds center. This is a single linear
            // pass (important for 100k+ triangle CAD parts) and yields at most
            // eight conservative OBBs instead of one large empty-volume box.
            Rect3D full = mesh.Bounds;
            double cx = full.X + full.SizeX / 2.0;
            double cy = full.Y + full.SizeY / 2.0;
            double cz = full.Z + full.SizeZ / 2.0;
            var bins = new Rect3D[8];
            var used = new bool[8];

            for (int t = 0; t < triangleCount; t++)
            {
                int ia = mesh.TriangleIndices[t * 3];
                int ib = mesh.TriangleIndices[t * 3 + 1];
                int ic = mesh.TriangleIndices[t * 3 + 2];
                if (ia < 0 || ib < 0 || ic < 0 ||
                    ia >= mesh.Positions.Count || ib >= mesh.Positions.Count || ic >= mesh.Positions.Count)
                    continue;

                Point3D a = mesh.Positions[ia];
                Point3D b = mesh.Positions[ib];
                Point3D c = mesh.Positions[ic];
                double tx = (a.X + b.X + c.X) / 3.0;
                double ty = (a.Y + b.Y + c.Y) / 3.0;
                double tz = (a.Z + b.Z + c.Z) / 3.0;
                int bin = (tx >= cx ? 1 : 0) |
                          (ty >= cy ? 2 : 0) |
                          (tz >= cz ? 4 : 0);

                double minX = Math.Min(a.X, Math.Min(b.X, c.X));
                double minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
                double minZ = Math.Min(a.Z, Math.Min(b.Z, c.Z));
                double maxX = Math.Max(a.X, Math.Max(b.X, c.X));
                double maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
                double maxZ = Math.Max(a.Z, Math.Max(b.Z, c.Z));
                var triBounds = new Rect3D(minX, minY, minZ,
                    Math.Max(0, maxX - minX),
                    Math.Max(0, maxY - minY),
                    Math.Max(0, maxZ - minZ));

                if (!used[bin])
                {
                    bins[bin] = triBounds;
                    used[bin] = true;
                }
                else
                {
                    Rect3D current = bins[bin];
                    double ux0 = Math.Min(current.X, triBounds.X);
                    double uy0 = Math.Min(current.Y, triBounds.Y);
                    double uz0 = Math.Min(current.Z, triBounds.Z);
                    double ux1 = Math.Max(current.X + current.SizeX, triBounds.X + triBounds.SizeX);
                    double uy1 = Math.Max(current.Y + current.SizeY, triBounds.Y + triBounds.SizeY);
                    double uz1 = Math.Max(current.Z + current.SizeZ, triBounds.Z + triBounds.SizeZ);
                    bins[bin] = new Rect3D(ux0, uy0, uz0,
                        Math.Max(0, ux1 - ux0),
                        Math.Max(0, uy1 - uy0),
                        Math.Max(0, uz1 - uz0));
                }
            }

            var result = new List<Rect3D>(8);
            for (int i = 0; i < bins.Length; i++)
                if (used[i] && !bins[i].IsEmpty)
                    result.Add(bins[i]);
            return result.Count > 0 ? result : new[] { mesh.Bounds };
        }

        private MeshGeometry3D CreateGeometry(XElement geometry)
        {
            string key = geometry.ToString(SaveOptions.DisableFormatting);
            if (_geometryCache.TryGetValue(key, out var cached))
                return cached;

            MeshGeometry3D mesh;
            if (geometry.Element("box") is XElement box)
            {
                Vector3D size = ParseVector(Attr(box, "size"));
                mesh = PrimitiveMeshes.Box(size.X, size.Y, size.Z);
            }
            else if (geometry.Element("cylinder") is XElement cylinder)
            {
                double radius = DoubleAttr(cylinder, "radius");
                double length = DoubleAttr(cylinder, "length");
                mesh = PrimitiveMeshes.Cylinder(radius, length, 40);
            }
            else if (geometry.Element("sphere") is XElement sphere)
            {
                mesh = PrimitiveMeshes.Sphere(DoubleAttr(sphere, "radius"), 28, 18);
            }
            else if (geometry.Element("mesh") is XElement meshElement)
            {
                string filename = Attr(meshElement, "filename");
                Vector3D scale = ParseVector(meshElement.Attribute("scale")?.Value ?? "1 1 1");
                string meshPath = ResolveMeshPath(filename);
                mesh = string.Equals(Path.GetExtension(meshPath), ".stl", StringComparison.OrdinalIgnoreCase)
                    ? PrimitiveMeshes.Stl(meshPath, scale)
                    : PrimitiveMeshes.Obj(meshPath, scale);
            }
            else
            {
                return null;
            }

            _geometryCache[key] = mesh;
            return mesh;
        }

        private string ResolveMeshPath(string filename)
        {
            if (filename.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                filename = new Uri(filename).LocalPath;
            if (Path.IsPathRooted(filename)) return filename;
            string local = filename.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_baseDirectory, local));
        }

        private void ReadJoints(XElement robot)
        {
            foreach (var e in robot.Elements("joint"))
            {
                string name = Attr(e, "name");
                string type = Attr(e, "type");
                string parent = Attr(e.Element("parent"), "link");
                string child = Attr(e.Element("child"), "link");
                Vector3D axis = ParseVector(e.Element("axis")?.Attribute("xyz")?.Value ?? "0 0 1");
                var limit = e.Element("limit");
                double lower = limit == null ? double.NegativeInfinity : DoubleAttr(limit, "lower", double.NegativeInfinity);
                double upper = limit == null ? double.PositiveInfinity : DoubleAttr(limit, "upper", double.PositiveInfinity);

                var joint = new UrdfJoint(name, type, parent, child, axis,
                    ParseOriginComponents(e.Element("origin")), lower, upper);
                _joints[name] = joint;
                _parentJointByChild[child] = joint;
            }
        }

        private void BuildTree(XElement robot)
        {
            var childLinks = _joints.Values.Select(j => j.ChildLink).ToHashSet(StringComparer.Ordinal);
            _rootLinkName = _links.Keys.FirstOrDefault(l => !childLinks.Contains(l))
                ?? throw new InvalidDataException("URDF contains no root link.");

            foreach (var joint in _joints.Values)
            {
                if (!_links.TryGetValue(joint.ParentLink, out var parent) ||
                    !_links.TryGetValue(joint.ChildLink, out var child))
                    throw new InvalidDataException($"Joint {joint.Name} references a missing link.");

                joint.Node.Children.Add(child);
                parent.Children.Add(joint.Node);
            }

            RootModel = _links[_rootLinkName];
        }

        private enum CollisionEyeSide
        {
            None,
            Left,
            Right
        }

        private enum CollisionKind
        {
            UpperFlap,
            LowerFlap,
            EyeTube,
            GimbalTop,
            GimbalBottom,
            GimbalBarTop,
            GimbalBarBottom,
            FrontLens
        }

        private sealed class CollisionCandidate
        {
            public CollisionProxy Proxy { get; }
            public OrientedBox Box { get; }
            public CollisionKind Kind { get; }
            public CollisionEyeSide EyeSide { get; }

            public CollisionCandidate(CollisionProxy proxy, OrientedBox box,
                                      CollisionKind kind,
                                      CollisionEyeSide eyeSide = CollisionEyeSide.None)
            {
                Proxy = proxy;
                Box = box;
                Kind = kind;
                EyeSide = eyeSide;
            }
        }

        private static bool IsUpperFlapCollisionProxy(CollisionProxy proxy) =>
            proxy.LinkName is "left_top_flap_link" or "right_top_flap_link";

        private static bool IsLowerFlapCollisionProxy(CollisionProxy proxy) =>
            (proxy.LinkName is "left_bottom_flap_link" or "right_bottom_flap_link") &&
            (proxy.Name.StartsWith("auto_collision_lower_left_flap_panel/", StringComparison.Ordinal) ||
             proxy.Name.StartsWith("auto_collision_lower_right_flap_panel/", StringComparison.Ordinal));

        private static CollisionEyeSide GetGimbalBarSide(CollisionProxy proxy)
        {
            if (proxy.LinkName == "left_eye_pop_link" &&
                (proxy.Name.StartsWith("auto_collision_left_gimbal_bar_top/", StringComparison.Ordinal) ||
                 proxy.Name.StartsWith("auto_collision_left_gimbal_bar_bottom/", StringComparison.Ordinal)))
                return CollisionEyeSide.Left;

            if (proxy.LinkName == "right_eye_pop_link" &&
                (proxy.Name.StartsWith("auto_collision_right_gimbal_bar_top/", StringComparison.Ordinal) ||
                 proxy.Name.StartsWith("auto_collision_right_gimbal_bar_bottom/", StringComparison.Ordinal)))
                return CollisionEyeSide.Right;

            return CollisionEyeSide.None;
        }

        private static CollisionKind? GetGimbalBarKind(CollisionProxy proxy)
        {
            if (proxy.Name.Contains("_gimbal_bar_top/", StringComparison.Ordinal))
                return CollisionKind.GimbalBarTop;
            if (proxy.Name.Contains("_gimbal_bar_bottom/", StringComparison.Ordinal))
                return CollisionKind.GimbalBarBottom;
            return null;
        }

        private static CollisionEyeSide GetEyeTubeSide(CollisionProxy proxy)
        {
            if (!string.Equals(proxy.LinkName, "head_link", StringComparison.Ordinal))
                return CollisionEyeSide.None;
            if (proxy.Name.StartsWith("auto_collision_left_eye_tube/", StringComparison.Ordinal))
                return CollisionEyeSide.Left;
            if (proxy.Name.StartsWith("auto_collision_right_eye_tube/", StringComparison.Ordinal))
                return CollisionEyeSide.Right;
            return CollisionEyeSide.None;
        }

        private static CollisionEyeSide GetGimbalSide(CollisionProxy proxy) =>
            proxy.LinkName switch
            {
                "left_eye_v_link" => CollisionEyeSide.Left,
                "right_eye_v_link" => CollisionEyeSide.Right,
                _ => CollisionEyeSide.None
            };

        private static CollisionEyeSide GetLensSide(CollisionProxy proxy) =>
            proxy.LinkName switch
            {
                "left_eye_h_link" => CollisionEyeSide.Left,
                "right_eye_h_link" => CollisionEyeSide.Right,
                _ => CollisionEyeSide.None
            };

        private static bool TryClipY(Rect3D source, double minY, double maxY,
                                     out Rect3D clipped)
        {
            double y0 = Math.Max(source.Y, minY);
            double y1 = Math.Min(source.Y + source.SizeY, maxY);
            if (y1 <= y0)
            {
                clipped = Rect3D.Empty;
                return false;
            }
            clipped = new Rect3D(source.X, y0, source.Z,
                                 source.SizeX, y1 - y0, source.SizeZ);
            return true;
        }

        private static bool TryClipZ(Rect3D source, double minZ, double maxZ,
                                     out Rect3D clipped)
        {
            double z0 = Math.Max(source.Z, minZ);
            double z1 = Math.Min(source.Z + source.SizeZ, maxZ);
            if (z1 <= z0)
            {
                clipped = Rect3D.Empty;
                return false;
            }
            clipped = new Rect3D(source.X, source.Y, z0,
                                 source.SizeX, source.SizeY, z1 - z0);
            return true;
        }

        /// <summary>
        /// Build only the collision envelopes that can generate warnings in
        /// v1.5.14. Gimbal and lens meshes are intentionally clipped to thin
        /// contact bands so empty volume inside their CAD ring/assembly does not
        /// generate broad false positives. The complete lower-flap PANEL is
        /// collision-active; its separately rendered arm and hardware deliberately
        /// have no collision proxy and are therefore excluded.
        /// </summary>
        private List<CollisionShape> BuildCollisionShapes()
        {
            const double GimbalSelectionBand = 0.006; // 6 mm near top/bottom
            const double GimbalContactBand = 0.004;   // outermost 4 mm surface
            const double LensSelectionBand = 0.006;   // 6 mm from front-most CAD
            const double LensContactBand = 0.004;     // front-most 4 mm face

            var result = new List<CollisionShape>();

            foreach (var proxy in _collisionProxies)
            {
                if (IsUpperFlapCollisionProxy(proxy))
                    result.Add(new CollisionShape(proxy, proxy.LocalBounds,
                                                      CollisionKind.UpperFlap));
                else if (IsLowerFlapCollisionProxy(proxy))
                    result.Add(new CollisionShape(proxy, proxy.LocalBounds,
                                                      CollisionKind.LowerFlap));
                else
                {
                    CollisionEyeSide tubeSide = GetEyeTubeSide(proxy);
                    if (tubeSide != CollisionEyeSide.None)
                    {
                        result.Add(new CollisionShape(proxy, proxy.LocalBounds,
                                                          CollisionKind.EyeTube, tubeSide));
                        continue;
                    }

                    CollisionEyeSide barSide = GetGimbalBarSide(proxy);
                    CollisionKind? barKind = GetGimbalBarKind(proxy);
                    if (barSide != CollisionEyeSide.None && barKind.HasValue)
                    {
                        result.Add(new CollisionShape(proxy, proxy.LocalBounds,
                                                          barKind.Value, barSide));
                    }
                }
            }

            foreach (CollisionEyeSide side in new[] { CollisionEyeSide.Left, CollisionEyeSide.Right })
            {
                var gimbal = _collisionProxies.Where(p => GetGimbalSide(p) == side).ToList();
                if (gimbal.Count > 0)
                {
                    double top = gimbal.Max(p => p.LocalBounds.Y + p.LocalBounds.SizeY);
                    double bottom = gimbal.Min(p => p.LocalBounds.Y);

                    foreach (var proxy in gimbal)
                    {
                        double proxyTop = proxy.LocalBounds.Y + proxy.LocalBounds.SizeY;
                        if (proxyTop >= top - GimbalSelectionBand &&
                            TryClipY(proxy.LocalBounds, top - GimbalContactBand, top, out Rect3D topBounds))
                        {
                            result.Add(new CollisionShape(proxy,
                                topBounds, CollisionKind.GimbalTop, side));
                        }

                        if (proxy.LocalBounds.Y <= bottom + GimbalSelectionBand &&
                            TryClipY(proxy.LocalBounds, bottom, bottom + GimbalContactBand, out Rect3D bottomBounds))
                        {
                            result.Add(new CollisionShape(proxy,
                                bottomBounds, CollisionKind.GimbalBottom, side));
                        }
                    }
                }

                var lens = _collisionProxies.Where(p => GetLensSide(p) == side).ToList();
                if (lens.Count > 0)
                {
                    // The STL-to-URDF origin rotates mesh-local Z onto the eye's
                    // forward axis.  Only retain the front-most face region.
                    double front = lens.Max(p => p.LocalBounds.Z + p.LocalBounds.SizeZ);
                    foreach (var proxy in lens)
                    {
                        double proxyFront = proxy.LocalBounds.Z + proxy.LocalBounds.SizeZ;
                        if (proxyFront < front - LensSelectionBand) continue;
                        if (!TryClipZ(proxy.LocalBounds, front - LensContactBand, front,
                                      out Rect3D frontBounds)) continue;

                        result.Add(new CollisionShape(proxy,
                            frontBounds, CollisionKind.FrontLens, side));
                    }
                }
            }

            return result;
        }

        private static bool EyeSidePoppedOut(CollisionEyeSide side,
                                             bool leftEyePoppedOut,
                                             bool rightEyePoppedOut) =>
            side switch
            {
                CollisionEyeSide.Left => leftEyePoppedOut,
                CollisionEyeSide.Right => rightEyePoppedOut,
                _ => false
            };

        private static bool ShouldCheckCollisionPair(CollisionCandidate a,
                                                     CollisionCandidate b,
                                                     bool leftEyePoppedOut,
                                                     bool rightEyePoppedOut)
        {
            CollisionCandidate flap;
            CollisionCandidate target;

            if (a.Kind is CollisionKind.UpperFlap or CollisionKind.LowerFlap)
            {
                flap = a;
                target = b;
            }
            else if (b.Kind is CollisionKind.UpperFlap or CollisionKind.LowerFlap)
            {
                flap = b;
                target = a;
            }
            else
            {
                return false;
            }

            // Flap-to-flap warnings are intentionally disabled.
            if (target.Kind is CollisionKind.UpperFlap or CollisionKind.LowerFlap)
                return false;

            if (flap.Kind == CollisionKind.UpperFlap)
            {
                // Upper flaps: outside of either fixed eye tube at all times,
                // plus the TOP moving gimbal band and fixed top Gimbal Bar while
                // that eye is popped out.
                if (target.Kind == CollisionKind.EyeTube) return true;
                if (target.Kind is CollisionKind.GimbalTop or CollisionKind.GimbalBarTop)
                    return EyeSidePoppedOut(target.EyeSide,
                                            leftEyePoppedOut, rightEyePoppedOut);
                return false;
            }

            // Lower flaps use the complete flap-panel collision proxy. The
            // separately rendered arm/hardware has no collision proxy and therefore
            // can neither trigger nor receive red collision highlighting. The full
            // flap panel checks the front lens at all times, plus the BOTTOM
            // moving gimbal band and fixed bottom Gimbal Bar while the eye is popped.
            if (target.Kind == CollisionKind.FrontLens) return true;
            if (target.Kind is CollisionKind.GimbalBottom or CollisionKind.GimbalBarBottom)
                return EyeSidePoppedOut(target.EyeSide,
                                        leftEyePoppedOut, rightEyePoppedOut);
            return false;
        }

        private List<CollisionHit> DetectCollisionPairsReference(bool ignoreBaseline,
                                                        bool leftEyePoppedOut,
                                                        bool rightEyePoppedOut)
        {
            // Collision warnings are intentionally limited to:
            //   upper flap ↔ fixed eye-tube exterior
            //   upper flap ↔ top moving-gimbal band (only while popped)
            //   upper flap ↔ fixed top Gimbal Bar (only while popped)
            //   full lower flap panel ↔ bottom moving-gimbal band (only while popped)
            //   full lower flap panel ↔ fixed bottom Gimbal Bar (only while popped)
            //   full lower flap panel ↔ front-most lens face
            // Lower flap arms/hardware, upper-flap Hitec HS-85BB servo carriers,
            // and all other robot geometry are excluded.
            var boxes = BuildCollisionShapes().Select(s => new CollisionCandidate(s.Proxy, BuildOrientedBox(s.Proxy, s.Bounds), s.Kind, s.EyeSide)).ToList();

            // Broad phase: sweep along world X. Only boxes whose X spans
            // overlap reach the Y/Z AABB and oriented-box tests.
            boxes.Sort((a, b) => a.Box.Aabb.X.CompareTo(b.Box.Aabb.X));

            var hits = new List<CollisionHit>();
            for (int i = 0; i < boxes.Count; i++)
            {
                CollisionCandidate a = boxes[i];
                double aMaxX = a.Box.Aabb.X + a.Box.Aabb.SizeX;
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    CollisionCandidate b = boxes[j];
                    if (b.Box.Aabb.X > aMaxX) break;
                    if (string.Equals(a.Proxy.LinkName, b.Proxy.LinkName, StringComparison.Ordinal))
                        continue;
                    if (!ShouldCheckCollisionPair(a, b, leftEyePoppedOut, rightEyePoppedOut))
                        continue;

                    string pairKey = PairKey($"{a.Proxy.Id}#{a.Kind}",
                                             $"{b.Proxy.Id}#{b.Kind}");
                    if (ignoreBaseline && _baselineCollisionPairs.Contains(pairKey))
                        continue;
                    if (!AabbIntersects(a.Box.Aabb, b.Box.Aabb))
                        continue;
                    if (!OrientedBoxesIntersect(a.Box, b.Box))
                        continue;

                    hits.Add(new CollisionHit(a.Proxy, b.Proxy, pairKey));
                }
            }
            return hits;
        }

        private OrientedBox BuildOrientedBox(CollisionProxy proxy) =>
            BuildOrientedBox(proxy, proxy.LocalBounds);

        private OrientedBox BuildOrientedBox(CollisionProxy proxy, Rect3D b)
        {
            var centerLocal = new Point3D(
                b.X + b.SizeX / 2.0,
                b.Y + b.SizeY / 2.0,
                b.Z + b.SizeZ / 2.0);
            Point3D center = TransformCollisionPoint(proxy, centerLocal);

            var axes = new Vector3D[3];
            var half = new double[3];
            double[] localHalf = { b.SizeX / 2.0, b.SizeY / 2.0, b.SizeZ / 2.0 };
            Point3D[] unitPoints =
            {
                new(centerLocal.X + 1, centerLocal.Y, centerLocal.Z),
                new(centerLocal.X, centerLocal.Y + 1, centerLocal.Z),
                new(centerLocal.X, centerLocal.Y, centerLocal.Z + 1),
            };

            for (int i = 0; i < 3; i++)
            {
                Point3D wp = TransformCollisionPoint(proxy, unitPoints[i]);
                Vector3D axis = wp - center;
                double scale = axis.Length;
                if (scale < 1e-12)
                {
                    axis = i switch
                    {
                        0 => new Vector3D(1, 0, 0),
                        1 => new Vector3D(0, 1, 0),
                        _ => new Vector3D(0, 0, 1),
                    };
                    scale = 1.0;
                }
                else axis.Normalize();
                axes[i] = axis;
                half[i] = localHalf[i] * scale;
            }

            var corners = new List<Point3D>(8);
            foreach (double x in new[] { b.X, b.X + b.SizeX })
                foreach (double y in new[] { b.Y, b.Y + b.SizeY })
                    foreach (double z in new[] { b.Z, b.Z + b.SizeZ })
                        corners.Add(TransformCollisionPoint(proxy, new Point3D(x, y, z)));

            double minX = corners.Min(p => p.X), maxX = corners.Max(p => p.X);
            double minY = corners.Min(p => p.Y), maxY = corners.Max(p => p.Y);
            double minZ = corners.Min(p => p.Z), maxZ = corners.Max(p => p.Z);
            var aabb = new Rect3D(minX, minY, minZ,
                                  Math.Max(0, maxX - minX),
                                  Math.Max(0, maxY - minY),
                                  Math.Max(0, maxZ - minZ));

            return new OrientedBox(center, axes, half, aabb);
        }

        private Point3D TransformCollisionPoint(CollisionProxy proxy, Point3D point)
        {
            if (proxy.LocalTransform != null)
                point = proxy.LocalTransform.Transform(point);

            string linkName = proxy.LinkName;
            while (true)
            {
                if (_links.TryGetValue(linkName, out var link) && link.Transform != null)
                    point = link.Transform.Transform(point);

                if (!_parentJointByChild.TryGetValue(linkName, out var parentJoint))
                    break;

                if (parentJoint.Node.Transform != null)
                    point = parentJoint.Node.Transform.Transform(point);
                linkName = parentJoint.ParentLink;
            }
            return point;
        }

        private static bool AabbIntersects(Rect3D a, Rect3D b) =>
            a.X <= b.X + b.SizeX && a.X + a.SizeX >= b.X &&
            a.Y <= b.Y + b.SizeY && a.Y + a.SizeY >= b.Y &&
            a.Z <= b.Z + b.SizeZ && a.Z + a.SizeZ >= b.Z;

        /// <summary>Separating-axis test for two oriented boxes. The collision
        /// geometry itself may be a detailed STL; its local mesh bounds are the
        /// deliberately conservative collision envelope used by the real-time
        /// editor checker.</summary>
        private static bool OrientedBoxesIntersect(OrientedBox a, OrientedBox b)
        {
            const double eps = 1e-9;
            Span<double> r = stackalloc double[9];
            Span<double> ar = stackalloc double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    r[i * 3 + j] = Vector3D.DotProduct(a.Axis[i], b.Axis[j]);
                    ar[i * 3 + j] = Math.Abs(r[i * 3 + j]) + eps;
                }

            Vector3D between = b.Center - a.Center;
            Span<double> t = stackalloc double[]
            {
                Vector3D.DotProduct(between, a.Axis[0]),
                Vector3D.DotProduct(between, a.Axis[1]),
                Vector3D.DotProduct(between, a.Axis[2]),
            };

            double ra, rb;
            for (int i = 0; i < 3; i++)
            {
                ra = a.Half[i];
                rb = b.Half[0] * ar[i * 3 + 0] + b.Half[1] * ar[i * 3 + 1] + b.Half[2] * ar[i * 3 + 2];
                if (Math.Abs(t[i]) > ra + rb) return false;
            }

            for (int j = 0; j < 3; j++)
            {
                ra = a.Half[0] * ar[0 * 3 + j] + a.Half[1] * ar[1 * 3 + j] + a.Half[2] * ar[2 * 3 + j];
                rb = b.Half[j];
                double projected = Math.Abs(t[0] * r[0 * 3 + j] + t[1] * r[1 * 3 + j] + t[2] * r[2 * 3 + j]);
                if (projected > ra + rb) return false;
            }

            // Cross-product axes A0 x B0 ... A2 x B2.
            ra = a.Half[1] * ar[2 * 3 + 0] + a.Half[2] * ar[1 * 3 + 0];
            rb = b.Half[1] * ar[0 * 3 + 2] + b.Half[2] * ar[0 * 3 + 1];
            if (Math.Abs(t[2] * r[1 * 3 + 0] - t[1] * r[2 * 3 + 0]) > ra + rb) return false;

            ra = a.Half[1] * ar[2 * 3 + 1] + a.Half[2] * ar[1 * 3 + 1];
            rb = b.Half[0] * ar[0 * 3 + 2] + b.Half[2] * ar[0 * 3 + 0];
            if (Math.Abs(t[2] * r[1 * 3 + 1] - t[1] * r[2 * 3 + 1]) > ra + rb) return false;

            ra = a.Half[1] * ar[2 * 3 + 2] + a.Half[2] * ar[1 * 3 + 2];
            rb = b.Half[0] * ar[0 * 3 + 1] + b.Half[1] * ar[0 * 3 + 0];
            if (Math.Abs(t[2] * r[1 * 3 + 2] - t[1] * r[2 * 3 + 2]) > ra + rb) return false;

            ra = a.Half[0] * ar[2 * 3 + 0] + a.Half[2] * ar[0 * 3 + 0];
            rb = b.Half[1] * ar[1 * 3 + 2] + b.Half[2] * ar[1 * 3 + 1];
            if (Math.Abs(t[0] * r[2 * 3 + 0] - t[2] * r[0 * 3 + 0]) > ra + rb) return false;

            ra = a.Half[0] * ar[2 * 3 + 1] + a.Half[2] * ar[0 * 3 + 1];
            rb = b.Half[0] * ar[1 * 3 + 2] + b.Half[2] * ar[1 * 3 + 0];
            if (Math.Abs(t[0] * r[2 * 3 + 1] - t[2] * r[0 * 3 + 1]) > ra + rb) return false;

            ra = a.Half[0] * ar[2 * 3 + 2] + a.Half[2] * ar[0 * 3 + 2];
            rb = b.Half[0] * ar[1 * 3 + 1] + b.Half[1] * ar[1 * 3 + 0];
            if (Math.Abs(t[0] * r[2 * 3 + 2] - t[2] * r[0 * 3 + 2]) > ra + rb) return false;

            ra = a.Half[0] * ar[1 * 3 + 0] + a.Half[1] * ar[0 * 3 + 0];
            rb = b.Half[1] * ar[2 * 3 + 2] + b.Half[2] * ar[2 * 3 + 1];
            if (Math.Abs(t[1] * r[0 * 3 + 0] - t[0] * r[1 * 3 + 0]) > ra + rb) return false;

            ra = a.Half[0] * ar[1 * 3 + 1] + a.Half[1] * ar[0 * 3 + 1];
            rb = b.Half[0] * ar[2 * 3 + 2] + b.Half[2] * ar[2 * 3 + 0];
            if (Math.Abs(t[1] * r[0 * 3 + 1] - t[0] * r[1 * 3 + 1]) > ra + rb) return false;

            ra = a.Half[0] * ar[1 * 3 + 2] + a.Half[1] * ar[0 * 3 + 2];
            rb = b.Half[0] * ar[2 * 3 + 1] + b.Half[1] * ar[2 * 3 + 0];
            if (Math.Abs(t[1] * r[0 * 3 + 2] - t[0] * r[1 * 3 + 2]) > ra + rb) return false;

            return true;
        }

        private static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        private static Transform3D ParseOriginTransform(XElement origin)
        {
            var (xyz, rpy) = ParseOriginComponents(origin);
            return BuildTransform(xyz, rpy);
        }

        internal static Transform3D BuildTransform(Vector3D xyz, Vector3D rpy)
        {
            var g = new Transform3DGroup();
            if (Math.Abs(rpy.X) > 1e-12)
                g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), rpy.X / Deg)));
            if (Math.Abs(rpy.Y) > 1e-12)
                g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), rpy.Y / Deg)));
            if (Math.Abs(rpy.Z) > 1e-12)
                g.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), rpy.Z / Deg)));
            if (xyz.LengthSquared > 1e-20)
                g.Children.Add(new TranslateTransform3D(xyz.X, xyz.Y, xyz.Z));
            return g;
        }

        private static (Vector3D xyz, Vector3D rpy) ParseOriginComponents(XElement origin)
        {
            if (origin == null) return (new Vector3D(), new Vector3D());
            return (ParseVector(origin.Attribute("xyz")?.Value ?? "0 0 0"),
                    ParseVector(origin.Attribute("rpy")?.Value ?? "0 0 0"));
        }

        private static Vector3D ParseVector(string text)
        {
            string[] p = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 3) throw new FormatException($"Expected a three-value vector, got '{text}'.");
            return new Vector3D(ParseDouble(p[0]), ParseDouble(p[1]), ParseDouble(p[2]));
        }

        private static Color ParseColor(string text)
        {
            string[] p = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            double r = p.Length > 0 ? ParseDouble(p[0]) : .7;
            double g = p.Length > 1 ? ParseDouble(p[1]) : .7;
            double b = p.Length > 2 ? ParseDouble(p[2]) : .7;
            double a = p.Length > 3 ? ParseDouble(p[3]) : 1;
            return Color.FromArgb((byte)(Math.Clamp(a, 0, 1) * 255),
                                  (byte)(Math.Clamp(r, 0, 1) * 255),
                                  (byte)(Math.Clamp(g, 0, 1) * 255),
                                  (byte)(Math.Clamp(b, 0, 1) * 255));
        }

        private static string Attr(XElement e, string name) =>
            e?.Attribute(name)?.Value ?? throw new InvalidDataException($"Missing '{name}' attribute.");

        private static double DoubleAttr(XElement e, string name, double fallback = double.NaN)
        {
            string text = e?.Attribute(name)?.Value;
            if (text == null)
            {
                if (!double.IsNaN(fallback)) return fallback;
                throw new InvalidDataException($"Missing '{name}' attribute.");
            }
            return ParseDouble(text);
        }

        private static double ParseDouble(string text) =>
            double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        public sealed class MotionSnapshot
        {
            internal Dictionary<string, double> JointPositions { get; }
            internal Dictionary<string, Matrix3D> LinkTransforms { get; }

            internal MotionSnapshot(Dictionary<string, double> joints,
                                    Dictionary<string, Matrix3D> links)
            {
                JointPositions = joints;
                LinkTransforms = links;
            }
        }

        private sealed class CollisionProxy
        {
            public string Id { get; }
            public string Name { get; }
            public string LinkName { get; }
            public Rect3D LocalBounds { get; }
            public Transform3D LocalTransform { get; }
            public GeometryModel3D SourceVisual { get; }

            public CollisionProxy(string id, string name, string linkName,
                                  Rect3D bounds, Transform3D transform,
                                  GeometryModel3D sourceVisual)
            {
                Id = id;
                Name = name;
                LinkName = linkName;
                LocalBounds = bounds;
                LocalTransform = transform;
                SourceVisual = sourceVisual;
            }
        }

        private sealed class CollisionHit
        {
            public CollisionProxy A { get; }
            public CollisionProxy B { get; }
            public string PairKey { get; }

            public CollisionHit(CollisionProxy a, CollisionProxy b, string pairKey)
            {
                A = a;
                B = b;
                PairKey = pairKey;
            }
        }

        private sealed class OrientedBox
        {
            public Point3D Center { get; set; }
            public Vector3D[] Axis { get; }
            public double[] Half { get; }
            public Rect3D Aabb { get; set; }

            public OrientedBox(Point3D center, Vector3D[] axis, double[] half, Rect3D aabb)
            {
                Center = center;
                Axis = axis;
                Half = half;
                Aabb = aabb;
            }
        }

        private sealed class UrdfJoint
        {
            private readonly string _type;
            private readonly Vector3D _axis;
            private readonly double _lower;
            private readonly double _upper;
            private readonly AxisAngleRotation3D _rotation;
            private readonly TranslateTransform3D _translation;

            public string Name { get; }
            public string ParentLink { get; }
            public string ChildLink { get; }
            public Model3DGroup Node { get; } = new();
            public double Position { get; private set; }
            public Matrix3D OriginMatrix { get; private set; }
            public string JointType => _type;
            public Vector3D Axis => _axis;

            public UrdfJoint(string name, string type, string parent, string child,
                             Vector3D axis, (Vector3D xyz, Vector3D rpy) origin,
                             double lower, double upper)
            {
                Name = name;
                _type = type;
                ParentLink = parent;
                ChildLink = child;
                _axis = axis.LengthSquared < 1e-20 ? new Vector3D(0, 0, 1) : axis;
                _axis.Normalize();
                _lower = lower;
                _upper = upper;

                var transforms = new Transform3DGroup();
                if (type is "revolute" or "continuous")
                {
                    _rotation = new AxisAngleRotation3D(_axis, 0);
                    transforms.Children.Add(new RotateTransform3D(_rotation));
                }
                else if (type == "prismatic")
                {
                    _translation = new TranslateTransform3D();
                    transforms.Children.Add(_translation);
                }

                // Child points first move in the joint frame, then the URDF
                // joint origin places that frame in the parent link.
                var originTransform = BuildTransform(origin.xyz, origin.rpy) as Transform3DGroup;
                OriginMatrix = originTransform?.Value ?? Matrix3D.Identity;
                if (originTransform != null)
                    foreach (Transform3D t in originTransform.Children)
                        transforms.Children.Add(t);
                Node.Transform = transforms;
            }

            public void SetPosition(double position)
            {
                // ServoAnimator calibration embedded in the URDF, with an optional
                // URDFconfig.json override, is the authoritative visual motion limiter.
                // URDF <limit> values remain descriptive metadata and are not re-clamped here.
                if (double.IsNaN(position) || double.IsInfinity(position)) return;

                Position = position;
                if (_rotation != null)
                    _rotation.Angle = position / Deg;
                else if (_translation != null)
                {
                    _translation.OffsetX = _axis.X * position;
                    _translation.OffsetY = _axis.Y * position;
                    _translation.OffsetZ = _axis.Z * position;
                }
            }
        }
    }

    internal static class PrimitiveMeshes
    {
        public static MeshGeometry3D Box(double x, double y, double z)
        {
            double hx = x / 2, hy = y / 2, hz = z / 2;
            var mesh = new MeshGeometry3D();
            AddFace(mesh, new Point3D(hx,-hy,-hz), new Point3D(hx,hy,-hz), new Point3D(hx,hy,hz), new Point3D(hx,-hy,hz), new Vector3D(1,0,0));
            AddFace(mesh, new Point3D(-hx,hy,-hz), new Point3D(-hx,-hy,-hz), new Point3D(-hx,-hy,hz), new Point3D(-hx,hy,hz), new Vector3D(-1,0,0));
            AddFace(mesh, new Point3D(-hx,hy,-hz), new Point3D(hx,hy,-hz), new Point3D(hx,hy,hz), new Point3D(-hx,hy,hz), new Vector3D(0,1,0));
            AddFace(mesh, new Point3D(hx,-hy,-hz), new Point3D(-hx,-hy,-hz), new Point3D(-hx,-hy,hz), new Point3D(hx,-hy,hz), new Vector3D(0,-1,0));
            AddFace(mesh, new Point3D(-hx,-hy,hz), new Point3D(hx,-hy,hz), new Point3D(hx,hy,hz), new Point3D(-hx,hy,hz), new Vector3D(0,0,1));
            AddFace(mesh, new Point3D(-hx,hy,-hz), new Point3D(hx,hy,-hz), new Point3D(hx,-hy,-hz), new Point3D(-hx,-hy,-hz), new Vector3D(0,0,-1));
            mesh.Freeze();
            return mesh;
        }

        private static void AddFace(MeshGeometry3D m, Point3D a, Point3D b, Point3D c, Point3D d, Vector3D n)
        {
            int i = m.Positions.Count;
            m.Positions.Add(a); m.Positions.Add(b); m.Positions.Add(c); m.Positions.Add(d);
            m.Normals.Add(n); m.Normals.Add(n); m.Normals.Add(n); m.Normals.Add(n);
            m.TriangleIndices.Add(i); m.TriangleIndices.Add(i+1); m.TriangleIndices.Add(i+2);
            m.TriangleIndices.Add(i); m.TriangleIndices.Add(i+2); m.TriangleIndices.Add(i+3);
        }

        public static MeshGeometry3D Cylinder(double radius, double length, int segments)
        {
            var m = new MeshGeometry3D();
            double z0 = -length / 2, z1 = length / 2;
            for (int i = 0; i < segments; i++)
            {
                double a0 = 2 * Math.PI * i / segments;
                double a1 = 2 * Math.PI * (i + 1) / segments;
                var n0 = new Vector3D(Math.Cos(a0), Math.Sin(a0), 0);
                var n1 = new Vector3D(Math.Cos(a1), Math.Sin(a1), 0);
                int k = m.Positions.Count;
                m.Positions.Add(new Point3D(radius*n0.X, radius*n0.Y, z0));
                m.Positions.Add(new Point3D(radius*n1.X, radius*n1.Y, z0));
                m.Positions.Add(new Point3D(radius*n1.X, radius*n1.Y, z1));
                m.Positions.Add(new Point3D(radius*n0.X, radius*n0.Y, z1));
                m.Normals.Add(n0); m.Normals.Add(n1); m.Normals.Add(n1); m.Normals.Add(n0);
                m.TriangleIndices.Add(k); m.TriangleIndices.Add(k+1); m.TriangleIndices.Add(k+2);
                m.TriangleIndices.Add(k); m.TriangleIndices.Add(k+2); m.TriangleIndices.Add(k+3);
            }

            AddCap(m, radius, z1, segments, true);
            AddCap(m, radius, z0, segments, false);
            m.Freeze();
            return m;
        }

        /// <summary>Create one annular arc segment in the Y/Z plane with its
        /// thickness along X. Angles use the eye convention: 0 degrees is
        /// 12 o'clock (+Z), increasing toward +Y. Used by the front Eye RGB
        /// ring so the 16 Arduino pixels appear as 16 illuminated arc segments.</summary>
        public static MeshGeometry3D AnnularSectorX(double innerRadius,
                                                     double outerRadius,
                                                     double thickness,
                                                     double startDegrees,
                                                     double endDegrees,
                                                     int subdivisions)
        {
            innerRadius = Math.Max(0, innerRadius);
            outerRadius = Math.Max(innerRadius, outerRadius);
            thickness = Math.Max(0.00001, thickness);
            subdivisions = Math.Max(1, subdivisions);

            var m = new MeshGeometry3D();
            double x0 = -thickness / 2.0;
            double x1 = thickness / 2.0;
            double start = startDegrees * Math.PI / 180.0;
            double end = endDegrees * Math.PI / 180.0;

            Point3D P(double x, double r, double a) =>
                new Point3D(x, r * Math.Sin(a), r * Math.Cos(a));

            for (int s = 0; s < subdivisions; s++)
            {
                double a0 = start + (end - start) * s / subdivisions;
                double a1 = start + (end - start) * (s + 1) / subdivisions;
                double am = (a0 + a1) / 2.0;

                var fi0 = P(x1, innerRadius, a0);
                var fo0 = P(x1, outerRadius, a0);
                var fo1 = P(x1, outerRadius, a1);
                var fi1 = P(x1, innerRadius, a1);
                AddFace(m, fi0, fo0, fo1, fi1, new Vector3D(1, 0, 0));

                var bi0 = P(x0, innerRadius, a0);
                var bo0 = P(x0, outerRadius, a0);
                var bo1 = P(x0, outerRadius, a1);
                var bi1 = P(x0, innerRadius, a1);
                AddFace(m, bi1, bo1, bo0, bi0, new Vector3D(-1, 0, 0));

                var outward = new Vector3D(0, Math.Sin(am), Math.Cos(am));
                AddFace(m, bo0, P(x1, outerRadius, a0), P(x1, outerRadius, a1), bo1, outward);

                var inward = new Vector3D(0, -Math.Sin(am), -Math.Cos(am));
                AddFace(m, bi1, P(x1, innerRadius, a1), P(x1, innerRadius, a0), bi0, inward);
            }

            var startOut = new Vector3D(0, -Math.Cos(start), Math.Sin(start));
            AddFace(m,
                P(x0, innerRadius, start), P(x0, outerRadius, start),
                P(x1, outerRadius, start), P(x1, innerRadius, start), startOut);

            var endOut = new Vector3D(0, Math.Cos(end), -Math.Sin(end));
            AddFace(m,
                P(x0, outerRadius, end), P(x0, innerRadius, end),
                P(x1, innerRadius, end), P(x1, outerRadius, end), endOut);

            m.Freeze();
            return m;
        }

        /// <summary>Create only the curved side wall of a cylinder, without
        /// end caps. The axis is Z. BackMaterial can be assigned by the caller
        /// when the inside surface also needs to be visible.</summary>
        public static MeshGeometry3D OpenCylinder(double radius, double length, int segments)
        {
            segments = Math.Max(12, segments);
            var m = new MeshGeometry3D();
            double z0 = -length / 2.0, z1 = length / 2.0;
            for (int i = 0; i < segments; i++)
            {
                double a0 = 2.0 * Math.PI * i / segments;
                double a1 = 2.0 * Math.PI * (i + 1) / segments;
                var n0 = new Vector3D(Math.Cos(a0), Math.Sin(a0), 0);
                var n1 = new Vector3D(Math.Cos(a1), Math.Sin(a1), 0);
                int k = m.Positions.Count;
                m.Positions.Add(new Point3D(radius * n0.X, radius * n0.Y, z0));
                m.Positions.Add(new Point3D(radius * n1.X, radius * n1.Y, z0));
                m.Positions.Add(new Point3D(radius * n1.X, radius * n1.Y, z1));
                m.Positions.Add(new Point3D(radius * n0.X, radius * n0.Y, z1));
                m.Normals.Add(n0); m.Normals.Add(n1); m.Normals.Add(n1); m.Normals.Add(n0);
                m.TriangleIndices.Add(k); m.TriangleIndices.Add(k + 1); m.TriangleIndices.Add(k + 2);
                m.TriangleIndices.Add(k); m.TriangleIndices.Add(k + 2); m.TriangleIndices.Add(k + 3);
            }
            m.Freeze();
            return m;
        }

        /// <summary>Create a closed annular cylinder (a short tube/ring). The
        /// cylinder axis is Z, matching Cylinder(); URDF visual transforms can
        /// rotate it onto the eye's X axis. When innerRadius reaches
        /// outerRadius the blue iris has zero width and an empty mesh is
        /// returned, allowing the RGB backing disc to be fully visible.</summary>
        public static MeshGeometry3D AnnularCylinder(double outerRadius,
                                                     double innerRadius,
                                                     double length,
                                                     int segments)
        {
            outerRadius = Math.Max(0, outerRadius);
            innerRadius = Math.Clamp(innerRadius, 0, outerRadius);
            segments = Math.Max(12, segments);

            var m = new MeshGeometry3D();
            if (outerRadius <= 1e-9 || innerRadius >= outerRadius - 1e-9)
            {
                m.Freeze();
                return m;
            }

            double z0 = -length / 2.0;
            double z1 = length / 2.0;

            for (int i = 0; i < segments; i++)
            {
                double a0 = 2.0 * Math.PI * i / segments;
                double a1 = 2.0 * Math.PI * (i + 1) / segments;
                double c0 = Math.Cos(a0), s0 = Math.Sin(a0);
                double c1 = Math.Cos(a1), s1 = Math.Sin(a1);

                // Outer curved wall.
                AddQuad(
                    m,
                    new Point3D(outerRadius*c0, outerRadius*s0, z0),
                    new Point3D(outerRadius*c1, outerRadius*s1, z0),
                    new Point3D(outerRadius*c1, outerRadius*s1, z1),
                    new Point3D(outerRadius*c0, outerRadius*s0, z1),
                    new Vector3D(c0, s0, 0),
                    new Vector3D(c1, s1, 0),
                    new Vector3D(c1, s1, 0),
                    new Vector3D(c0, s0, 0));

                // Inner curved wall, normals facing into the aperture.
                AddQuad(
                    m,
                    new Point3D(innerRadius*c1, innerRadius*s1, z0),
                    new Point3D(innerRadius*c0, innerRadius*s0, z0),
                    new Point3D(innerRadius*c0, innerRadius*s0, z1),
                    new Point3D(innerRadius*c1, innerRadius*s1, z1),
                    new Vector3D(-c1, -s1, 0),
                    new Vector3D(-c0, -s0, 0),
                    new Vector3D(-c0, -s0, 0),
                    new Vector3D(-c1, -s1, 0));

                // Front annular face (+Z).
                AddFace(
                    m,
                    new Point3D(outerRadius*c0, outerRadius*s0, z1),
                    new Point3D(outerRadius*c1, outerRadius*s1, z1),
                    new Point3D(innerRadius*c1, innerRadius*s1, z1),
                    new Point3D(innerRadius*c0, innerRadius*s0, z1),
                    new Vector3D(0, 0, 1));

                // Rear annular face (-Z).
                AddFace(
                    m,
                    new Point3D(outerRadius*c0, outerRadius*s0, z0),
                    new Point3D(innerRadius*c0, innerRadius*s0, z0),
                    new Point3D(innerRadius*c1, innerRadius*s1, z0),
                    new Point3D(outerRadius*c1, outerRadius*s1, z0),
                    new Vector3D(0, 0, -1));
            }

            m.Freeze();
            return m;
        }

        private static void AddQuad(MeshGeometry3D m,
                                    Point3D a, Point3D b, Point3D c, Point3D d,
                                    Vector3D na, Vector3D nb, Vector3D nc, Vector3D nd)
        {
            int i = m.Positions.Count;
            m.Positions.Add(a); m.Positions.Add(b); m.Positions.Add(c); m.Positions.Add(d);
            m.Normals.Add(na); m.Normals.Add(nb); m.Normals.Add(nc); m.Normals.Add(nd);
            m.TriangleIndices.Add(i); m.TriangleIndices.Add(i+1); m.TriangleIndices.Add(i+2);
            m.TriangleIndices.Add(i); m.TriangleIndices.Add(i+2); m.TriangleIndices.Add(i+3);
        }

        private static void AddCap(MeshGeometry3D m, double radius, double z, int segments, bool top)
        {
            int center = m.Positions.Count;
            var normal = new Vector3D(0, 0, top ? 1 : -1);
            m.Positions.Add(new Point3D(0,0,z)); m.Normals.Add(normal);
            for (int i = 0; i < segments; i++)
            {
                double a = 2 * Math.PI * i / segments;
                m.Positions.Add(new Point3D(radius*Math.Cos(a), radius*Math.Sin(a), z));
                m.Normals.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = center + 1 + i;
                int b = center + 1 + (i + 1) % segments;
                m.TriangleIndices.Add(center);
                if (top) { m.TriangleIndices.Add(a); m.TriangleIndices.Add(b); }
                else { m.TriangleIndices.Add(b); m.TriangleIndices.Add(a); }
            }
        }

        /// <summary>Load a lightweight Wavefront OBJ mesh. The STEP eye-flap
        /// solids are converted to triangulated OBJ files in Models/Meshes so the
        /// built-in WPF renderer does not require a native CAD/Assimp dependency.</summary>
        public static MeshGeometry3D Obj(string path, Vector3D scale)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("URDF mesh file not found", path);

            var source = new List<Point3D>();
            var triangles = new List<(int A, int B, int C)>();
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("v ", StringComparison.Ordinal))
                {
                    string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4)
                        source.Add(new Point3D(ParseObjDouble(p[1]) * scale.X,
                                               ParseObjDouble(p[2]) * scale.Y,
                                               ParseObjDouble(p[3]) * scale.Z));
                }
                else if (line.StartsWith("f ", StringComparison.Ordinal))
                {
                    string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 4) continue;
                    var idx = new List<int>();
                    for (int i = 1; i < p.Length; i++)
                    {
                        string head = p[i].Split('/')[0];
                        if (!int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                            continue;
                        int z = n > 0 ? n - 1 : source.Count + n;
                        if (z >= 0 && z < source.Count) idx.Add(z);
                    }
                    for (int i = 1; i + 1 < idx.Count; i++)
                        triangles.Add((idx[0], idx[i], idx[i + 1]));
                }
            }

            var mesh = new MeshGeometry3D();
            foreach (var tri in triangles)
            {
                Point3D a = source[tri.A], b = source[tri.B], c = source[tri.C];
                Vector3D n = Vector3D.CrossProduct(b - a, c - a);
                if (n.LengthSquared > 1e-20) n.Normalize();
                else n = new Vector3D(0, 0, 1);
                int k = mesh.Positions.Count;
                mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c);
                mesh.Normals.Add(n); mesh.Normals.Add(n); mesh.Normals.Add(n);
                mesh.TriangleIndices.Add(k); mesh.TriangleIndices.Add(k + 1); mesh.TriangleIndices.Add(k + 2);
            }
            mesh.Freeze();
            return mesh;
        }

        private static double ParseObjDouble(string text) =>
            double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>Load either binary or ASCII STL. Runtime CAD meshes are
        /// packaged as binary STL for compactness and speed, but accepting ASCII
        /// here makes the URDF preview resilient to CAD exporters that emit text
        /// STL without requiring a separate conversion step.</summary>
        public static MeshGeometry3D Stl(string path, Vector3D scale)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("URDF mesh file not found", path);

            using var stream = File.OpenRead(path);
            if (stream.Length < 15)
                throw new InvalidDataException("STL file is too short: " + path);

            // A binary STL is self-describing by file length: 80-byte header,
            // uint32 triangle count, then exactly 50 bytes per triangle.  Do not
            // rely on an initial "solid" token because a legal binary STL header
            // is allowed to begin with that word.
            if (stream.Length >= 84)
            {
                using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
                stream.Position = 80;
                uint triangleCount = reader.ReadUInt32();
                long expected = 84L + 50L * triangleCount;
                if (expected == stream.Length)
                {
                    stream.Position = 84;
                    return ReadBinaryStl(reader, triangleCount, scale);
                }
            }

            stream.Position = 0;
            using var text = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: true,
                                              bufferSize: 64 * 1024, leaveOpen: false);
            return ReadAsciiStl(text, scale, path);
        }

        private static MeshGeometry3D ReadBinaryStl(BinaryReader reader, uint triangleCount,
                                                     Vector3D scale)
        {
            var mesh = new MeshGeometry3D();
            for (uint i = 0; i < triangleCount; i++)
            {
                var supplied = new Vector3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Point3D a = ReadStlPoint(reader, scale);
                Point3D b = ReadStlPoint(reader, scale);
                Point3D c = ReadStlPoint(reader, scale);
                reader.ReadUInt16(); // attribute byte count
                AddStlTriangle(mesh, a, b, c, supplied);
            }
            mesh.Freeze();
            return mesh;
        }

        private static MeshGeometry3D ReadAsciiStl(StreamReader reader, Vector3D scale, string path)
        {
            var mesh = new MeshGeometry3D();
            var vertices = new List<Point3D>(3);
            Vector3D supplied = new(0, 0, 0);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("facet normal ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 5 &&
                        double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double nx) &&
                        double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double ny) &&
                        double.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double nz))
                        supplied = new Vector3D(nx, ny, nz);
                    else
                        supplied = new Vector3D(0, 0, 0);
                }
                else if (trimmed.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4 &&
                        double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                        double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                        double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                    {
                        vertices.Add(new Point3D(x * scale.X, y * scale.Y, z * scale.Z));
                        if (vertices.Count == 3)
                        {
                            AddStlTriangle(mesh, vertices[0], vertices[1], vertices[2], supplied);
                            vertices.Clear();
                        }
                    }
                }
            }

            if (mesh.TriangleIndices.Count == 0)
                throw new InvalidDataException("STL contains no readable triangles: " + path);
            mesh.Freeze();
            return mesh;
        }

        private static void AddStlTriangle(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c,
                                           Vector3D supplied)
        {
            Vector3D normal = supplied;
            if (normal.LengthSquared < 1e-20)
                normal = Vector3D.CrossProduct(b - a, c - a);
            if (normal.LengthSquared > 1e-20) normal.Normalize();
            else normal = new Vector3D(0, 0, 1);

            int k = mesh.Positions.Count;
            mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c);
            mesh.Normals.Add(normal); mesh.Normals.Add(normal); mesh.Normals.Add(normal);
            mesh.TriangleIndices.Add(k); mesh.TriangleIndices.Add(k + 1); mesh.TriangleIndices.Add(k + 2);
        }

        private static Point3D ReadStlPoint(BinaryReader reader, Vector3D scale) =>
            new(reader.ReadSingle() * scale.X,
                reader.ReadSingle() * scale.Y,
                reader.ReadSingle() * scale.Z);

        public static MeshGeometry3D Sphere(double radius, int slices, int stacks)
        {
            var m = new MeshGeometry3D();
            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI * stack / stacks;
                double z = radius * Math.Cos(phi);
                double ring = radius * Math.Sin(phi);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = 2 * Math.PI * slice / slices;
                    var p = new Point3D(ring*Math.Cos(theta), ring*Math.Sin(theta), z);
                    var n = new Vector3D(p.X, p.Y, p.Z); n.Normalize();
                    m.Positions.Add(p); m.Normals.Add(n);
                }
            }
            int row = slices + 1;
            for (int stack = 0; stack < stacks; stack++)
            for (int slice = 0; slice < slices; slice++)
            {
                int a = stack * row + slice;
                int b = a + row;
                m.TriangleIndices.Add(a); m.TriangleIndices.Add(b); m.TriangleIndices.Add(a+1);
                m.TriangleIndices.Add(a+1); m.TriangleIndices.Add(b); m.TriangleIndices.Add(b+1);
            }
            m.Freeze();
            return m;
        }
    }
}
