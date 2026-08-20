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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace ServoAnimator
{
    public sealed class RobotHeadView : Grid
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
        private readonly StackPanel _bottomControls = new();
        private readonly Button _recenterButton = new();
        private readonly Button _cameraMinus90Button = new();
        private readonly Button _cameraPlus90Button = new();
        private readonly Button _driveToggleButton = new();
        private readonly Button _collisionToggleButton = new();
        private readonly Button _dockToggleButton = new();
        private readonly StackPanel _cameraRow = new();
        private readonly Thumb _verticalResizeHandle = new();
        private bool _hostIsDocked = true;
        private bool _urdfDriveEnabled = true;
        private bool _collisionWarningsEnabled = true;
        private bool _suppressCollisionRefresh;
        private UrdfScene _scene;
        private UrdfConfiguration _urdfConfiguration = UrdfConfiguration.CreateDefault();
        private ServoConfiguration _servoConfiguration = ServoConfiguration.CreateDefault();

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

        private double _neckNodLeft;
        private double _neckNodRight;
        private double _neckTiltLeft;
        private double _neckTiltRight;
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
            // Light-blue neutral background requested for the URDF preview.
            Background = new SolidColorBrush(Color.FromRgb(0xC0, 0xED, 0xFC));

            _camera.FieldOfView = 38;
            _viewport.Camera = _camera;
            Children.Add(_viewport);

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
            _cameraRow.Orientation = Orientation.Horizontal;
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
            _cameraRow.Children.Add(_recenterButton);

            _cameraPlus90Button.Content = "→";
            _cameraPlus90Button.Padding = new Thickness(5, 2, 5, 2);
            _cameraPlus90Button.ToolTip = "Turn the camera 90° to the right";
            _cameraPlus90Button.Click += (_, _) => TurnCameraDegrees(90.0);
            _cameraRow.Children.Add(_cameraPlus90Button);
            _bottomControls.Children.Add(_cameraRow);
            Children.Add(_bottomControls);

            // Keep the URDF legend at the lower-right, independent of the controls.
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.VerticalAlignment = VerticalAlignment.Bottom;
            _status.Margin = new Thickness(10);
            Children.Add(_status);

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
            SizeChanged += (_, _) => UpdateCamera();

            ResetCamera();
            LoadUrdf();
            Loaded += (_, _) =>
            {
                UpdateCamera();
                CaptureOpeningCameraIfNeeded();
            };
        }

        private void LoadUrdf()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Models", "johnny5_head.urdf");
                if (!File.Exists(path))
                    path = Path.Combine(AppContext.BaseDirectory, "johnny5_head.urdf");

                _scene = UrdfScene.Load(path);
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
                            double neckNod = 0, double neckTurn = 0,
                            double whip = 0, double mic = 0, double mfr = 0,
                            double noseBody = 0,
                            double noseBasket = 0,
                            double leftEyePop = 0, double rightEyePop = 0,
                            double whipRotate = 0, double mfrRotate = 0)
        {
            if (_scene == null || !_urdfDriveEnabled) return;

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

            _neckNodLeft = _neckNodRight = neckNod;
            _neckTiltLeft = _neckTiltRight = neckTilt;
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
            RefreshCollisionState();
        }

        public void SetChildServo(ServoNames parentServo, RobotControls control, double value)
        {
            if (_scene == null || !_urdfDriveEnabled) return;

            // The two neck inputs share the same physical servo pair but create
            // different head axes. Preserve independent child values so URDF
            // Configuration can calibrate/test each physical servo separately.
            if (parentServo == ServoNames.NeckNodUp &&
                control is RobotControls.NeckTiltLeft or RobotControls.NeckTiltRight)
            {
                _neckTiltLeft = _neckTiltRight = 0;
                if (control == RobotControls.NeckTiltLeft) _neckNodLeft = value;
                else _neckNodRight = value;
                ApplyNeckPose(null);
                RefreshCollisionState();
                return;
            }
            if (parentServo == ServoNames.NeckTiltRight &&
                control is RobotControls.NeckTiltLeft or RobotControls.NeckTiltRight)
            {
                _neckNodLeft = _neckNodRight = 0;
                if (control == RobotControls.NeckTiltLeft) _neckTiltLeft = value;
                else _neckTiltRight = value;
                ApplyNeckPose(null);
                RefreshCollisionState();
                return;
            }

            SetControl(parentServo, control, value);
            RefreshCollisionState();
        }

        /// <summary>Replace the visual motion calibration used by this preview.
        /// Travel extents come from URDFconfig.json; child-servo direction is
        /// inherited live from ServoConfig.json.</summary>
        public void SetUrdfConfiguration(UrdfConfiguration configuration)
        {
            _urdfConfiguration = configuration ?? UrdfConfiguration.CreateDefault();
        }

        public void SetServoConfiguration(ServoConfiguration configuration)
        {
            _servoConfiguration = configuration ?? ServoConfiguration.CreateDefault();
            RebuildCollisionBaseline();
        }

        /// <summary>True when the current calibrated URDF pose contains at least
        /// one enabled collision warning that was not already an intentional
        /// contact in the calibrated logical-zero pose.</summary>
        public bool HasCollision => _collisionWarningsEnabled && _scene?.HasActiveCollision == true;

        /// <summary>Whether this URDF preview is actively performing collision
        /// warning checks.  The toggle is independent of URDF Drive.</summary>
        public bool CollisionWarningsEnabled => _collisionWarningsEnabled;

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

        private void RefreshCollisionState()
        {
            if (_scene == null || _suppressCollisionRefresh) return;

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
            double oldNodLeft = _neckNodLeft, oldNodRight = _neckNodRight;
            double oldTiltLeft = _neckTiltLeft, oldTiltRight = _neckTiltRight;
            double oldNeckTurn = _lastNeckTurn;
            double oldLeftEyePop = _leftEyePopLogical;
            double oldRightEyePop = _rightEyePopLogical;
            bool oldDrive = _urdfDriveEnabled;
            bool oldSuppress = _suppressCollisionRefresh;

            _urdfDriveEnabled = true;
            _suppressCollisionRefresh = true;
            try
            {
                _neckNodLeft = _neckNodRight = 0;
                _neckTiltLeft = _neckTiltRight = 0;
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
                _neckNodLeft = oldNodLeft;
                _neckNodRight = oldNodRight;
                _neckTiltLeft = oldTiltLeft;
                _neckTiltRight = oldTiltRight;
                _lastNeckTurn = oldNeckTurn;
                _leftEyePopLogical = oldLeftEyePop;
                _rightEyePopLogical = oldRightEyePop;
                _urdfDriveEnabled = oldDrive;
                _suppressCollisionRefresh = oldSuppress;
            }

            RefreshCollisionState();
        }

        private double Motion(ServoNames servo, RobotControls control, double input) =>
            _urdfConfiguration.Map(servo, control, input, _servoConfiguration);

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
            double scaledPairs = a * 7.0;
            Color off = Color.FromRgb(154, 82, 28);       // dull orange lens
            Color on = Color.FromRgb(255, 146, 32);       // bright orange lens
            Color emissive = Color.FromRgb(255, 104, 8);  // hot orange emission
            var levels = new double[14];

            for (int i = 0; i < 14; i++)
            {
                // 6/7 are the center pair, then 5/8, 4/9 ... 0/13.
                int pairFromCenter = i <= 6 ? 6 - i : i - 7;

                // Smoothly fill the next pair instead of snapping it directly
                // from off to full brightness. This also makes the halo/light
                // output track the audio level continuously.
                double level = Math.Clamp(scaledPairs - pairFromCenter, 0.0, 1.0);
                double glow = Math.Pow(level, 0.72);
                levels[i] = glow;

                _scene.SetMaterialColor($"lip_led_{i:00}_dynamic", BlendColor(off, on, level));
                _scene.SetMaterialEmissive($"lip_led_{i:00}_dynamic", ScaleColor(emissive, glow));
                _scene.SetLipHaloIntensity(i, glow);
            }

            _scene.UpdateLipPointLights(levels);

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
            if (_scene == null || !_urdfDriveEnabled) return;

            switch (servo)
            {
                case ServoNames.NeckTurn:
                    ApplyNeckPose(value);
                    break;

                // These logical controls share the physical neck pair. As in
                // the old view, a live move of one clears the other.
                case ServoNames.NeckNodUp:
                    _neckNodLeft = _neckNodRight = value;
                    _neckTiltLeft = _neckTiltRight = 0;
                    ApplyNeckPose(null);
                    break;
                case ServoNames.NeckTiltRight:
                    _neckTiltLeft = _neckTiltRight = value;
                    _neckNodLeft = _neckNodRight = 0;
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

            RefreshCollisionState();
        }

        private double _lastNeckTurn;

        private void ApplyNeckPose(double? neckTurn)
        {
            if (_scene == null || !_urdfDriveEnabled) return;
            if (neckTurn.HasValue) _lastNeckTurn = neckTurn.Value;

            // Visual travel comes from the calibration embedded in the URDF,
            // optionally overridden by URDFconfig.json. Joint origins still come
            // from the CAD/URDF. NeckTurn remains centered on the
            // CAD Disc, while nod/tilt remain centered on the Solid U-Joint
            // hinge intersection.
            _scene.SetJoint("NeckTurn",
                Motion(ServoNames.NeckTurn, RobotControls.NeckTurn, _lastNeckTurn) * Deg);

            // The two Fabco/neck servos encode the two head axes differently:
            // Nod is the differential component; Tilt is the common component.
            // Per-child extents and Servo Configuration directions therefore
            // combine into one mechanically meaningful head angle.
            double nodLeft = Motion(ServoNames.NeckNodUp, RobotControls.NeckTiltLeft, _neckNodLeft);
            double nodRight = Motion(ServoNames.NeckNodUp, RobotControls.NeckTiltRight, _neckNodRight);
            double tiltLeft = Motion(ServoNames.NeckTiltRight, RobotControls.NeckTiltLeft, _neckTiltLeft);
            double tiltRight = Motion(ServoNames.NeckTiltRight, RobotControls.NeckTiltRight, _neckTiltRight);

            // URDF Y pitch is opposite the application's NeckNodUp semantic.
            double pitch = -((nodLeft - nodRight) * 0.5) * Deg;
            double roll = ((tiltLeft + tiltRight) * 0.5) * Deg;
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
            if (_scene == null || !_urdfDriveEnabled) return;

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
            _scene.SetNeoPixelFrame(frame, eyeIntensity, ventIntensity);
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
            if (_bottomControls.IsMouseOver || _status.IsMouseOver || _verticalResizeHandle.IsMouseOver) return;

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

    internal sealed class UrdfScene
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
        private string _rootLinkName = "";
        private bool _collisionBaselineInitialized;

        private static readonly Material CollisionHighlightMaterial = CreateCollisionHighlightMaterial();

        public Model3DGroup RootModel { get; private set; }
        public bool HasActiveCollision => _activeCollisionPairs.Count > 0;
        public IReadOnlyCollection<string> ActiveCollisionPairs => _activeCollisionPairs.ToArray();
        public IReadOnlyCollection<string> CollidingLinks => _activeCollidingLinks.ToArray();

        public static UrdfScene Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("URDF file not found", path);

            var doc = XDocument.Load(path);
            var robot = doc.Root ?? throw new InvalidDataException("URDF has no robot root element.");
            var scene = new UrdfScene
            {
                _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? AppContext.BaseDirectory
            };
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
            public Point3D Center { get; }
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
                visual.Material = CollisionHighlightMaterial;
                visual.BackMaterial = CollisionHighlightMaterial;
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
                    var model = CreateVisual(visual);
                    if (model != null)
                    {
                        group.Children.Add(model);
                        linkVisuals.Add(model);
                        _originalMaterials[model] = (model.Material, model.BackMaterial);

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

        private GeometryModel3D CreateVisual(XElement visual)
        {
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
        private List<CollisionCandidate> BuildRelevantCollisionCandidates()
        {
            const double GimbalSelectionBand = 0.006; // 6 mm near top/bottom
            const double GimbalContactBand = 0.004;   // outermost 4 mm surface
            const double LensSelectionBand = 0.006;   // 6 mm from front-most CAD
            const double LensContactBand = 0.004;     // front-most 4 mm face

            var result = new List<CollisionCandidate>();

            foreach (var proxy in _collisionProxies)
            {
                if (IsUpperFlapCollisionProxy(proxy))
                    result.Add(new CollisionCandidate(proxy, BuildOrientedBox(proxy),
                                                      CollisionKind.UpperFlap));
                else if (IsLowerFlapCollisionProxy(proxy))
                    result.Add(new CollisionCandidate(proxy, BuildOrientedBox(proxy),
                                                      CollisionKind.LowerFlap));
                else
                {
                    CollisionEyeSide tubeSide = GetEyeTubeSide(proxy);
                    if (tubeSide != CollisionEyeSide.None)
                    {
                        result.Add(new CollisionCandidate(proxy, BuildOrientedBox(proxy),
                                                          CollisionKind.EyeTube, tubeSide));
                        continue;
                    }

                    CollisionEyeSide barSide = GetGimbalBarSide(proxy);
                    CollisionKind? barKind = GetGimbalBarKind(proxy);
                    if (barSide != CollisionEyeSide.None && barKind.HasValue)
                    {
                        result.Add(new CollisionCandidate(proxy, BuildOrientedBox(proxy),
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
                            result.Add(new CollisionCandidate(proxy,
                                BuildOrientedBox(proxy, topBounds), CollisionKind.GimbalTop, side));
                        }

                        if (proxy.LocalBounds.Y <= bottom + GimbalSelectionBand &&
                            TryClipY(proxy.LocalBounds, bottom, bottom + GimbalContactBand, out Rect3D bottomBounds))
                        {
                            result.Add(new CollisionCandidate(proxy,
                                BuildOrientedBox(proxy, bottomBounds), CollisionKind.GimbalBottom, side));
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

                        result.Add(new CollisionCandidate(proxy,
                            BuildOrientedBox(proxy, frontBounds), CollisionKind.FrontLens, side));
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

        private List<CollisionHit> DetectCollisionPairs(bool ignoreBaseline,
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
            var boxes = BuildRelevantCollisionCandidates();

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
            double[,] r = new double[3, 3];
            double[,] ar = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    r[i, j] = Vector3D.DotProduct(a.Axis[i], b.Axis[j]);
                    ar[i, j] = Math.Abs(r[i, j]) + eps;
                }

            Vector3D between = b.Center - a.Center;
            double[] t =
            {
                Vector3D.DotProduct(between, a.Axis[0]),
                Vector3D.DotProduct(between, a.Axis[1]),
                Vector3D.DotProduct(between, a.Axis[2]),
            };

            double ra, rb;
            for (int i = 0; i < 3; i++)
            {
                ra = a.Half[i];
                rb = b.Half[0] * ar[i, 0] + b.Half[1] * ar[i, 1] + b.Half[2] * ar[i, 2];
                if (Math.Abs(t[i]) > ra + rb) return false;
            }

            for (int j = 0; j < 3; j++)
            {
                ra = a.Half[0] * ar[0, j] + a.Half[1] * ar[1, j] + a.Half[2] * ar[2, j];
                rb = b.Half[j];
                double projected = Math.Abs(t[0] * r[0, j] + t[1] * r[1, j] + t[2] * r[2, j]);
                if (projected > ra + rb) return false;
            }

            // Cross-product axes A0 x B0 ... A2 x B2.
            ra = a.Half[1] * ar[2, 0] + a.Half[2] * ar[1, 0];
            rb = b.Half[1] * ar[0, 2] + b.Half[2] * ar[0, 1];
            if (Math.Abs(t[2] * r[1, 0] - t[1] * r[2, 0]) > ra + rb) return false;

            ra = a.Half[1] * ar[2, 1] + a.Half[2] * ar[1, 1];
            rb = b.Half[0] * ar[0, 2] + b.Half[2] * ar[0, 0];
            if (Math.Abs(t[2] * r[1, 1] - t[1] * r[2, 1]) > ra + rb) return false;

            ra = a.Half[1] * ar[2, 2] + a.Half[2] * ar[1, 2];
            rb = b.Half[0] * ar[0, 1] + b.Half[1] * ar[0, 0];
            if (Math.Abs(t[2] * r[1, 2] - t[1] * r[2, 2]) > ra + rb) return false;

            ra = a.Half[0] * ar[2, 0] + a.Half[2] * ar[0, 0];
            rb = b.Half[1] * ar[1, 2] + b.Half[2] * ar[1, 1];
            if (Math.Abs(t[0] * r[2, 0] - t[2] * r[0, 0]) > ra + rb) return false;

            ra = a.Half[0] * ar[2, 1] + a.Half[2] * ar[0, 1];
            rb = b.Half[0] * ar[1, 2] + b.Half[2] * ar[1, 0];
            if (Math.Abs(t[0] * r[2, 1] - t[2] * r[0, 1]) > ra + rb) return false;

            ra = a.Half[0] * ar[2, 2] + a.Half[2] * ar[0, 2];
            rb = b.Half[0] * ar[1, 1] + b.Half[1] * ar[1, 0];
            if (Math.Abs(t[0] * r[2, 2] - t[2] * r[0, 2]) > ra + rb) return false;

            ra = a.Half[0] * ar[1, 0] + a.Half[1] * ar[0, 0];
            rb = b.Half[1] * ar[2, 2] + b.Half[2] * ar[2, 1];
            if (Math.Abs(t[1] * r[0, 0] - t[0] * r[1, 0]) > ra + rb) return false;

            ra = a.Half[0] * ar[1, 1] + a.Half[1] * ar[0, 1];
            rb = b.Half[0] * ar[2, 2] + b.Half[2] * ar[2, 0];
            if (Math.Abs(t[1] * r[0, 1] - t[0] * r[1, 1]) > ra + rb) return false;

            ra = a.Half[0] * ar[1, 2] + a.Half[1] * ar[0, 2];
            rb = b.Half[0] * ar[2, 1] + b.Half[1] * ar[2, 0];
            if (Math.Abs(t[1] * r[0, 2] - t[0] * r[1, 2]) > ra + rb) return false;

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
            public Point3D Center { get; }
            public Vector3D[] Axis { get; }
            public double[] Half { get; }
            public Rect3D Aabb { get; }

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
