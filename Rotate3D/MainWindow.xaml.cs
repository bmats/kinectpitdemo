using System;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Warlords.Kinect;

namespace Rotate3D {
    /// <summary>
    /// The main window, which displays the Kinect color stream and overlays.
    /// </summary>
    public partial class MainWindow : Window {
        private Kinect kinect;
        private WaveDetector waver;
        private Timer timer;

        private float nextAngleX, nextAngleY, nextFactorZ;
        private bool swNext = false;

        // Gesture-triggered overlays
        private ThemedOverlayWindow helpTipWindow, helpWindow;
        private bool helpWindowVisible = false;
        private Banner banner;
        private Hand hand;

        private const int TimerInterval = 40; // ~24 fps

        public MainWindow() {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e) {
            // Move Window to the bottom left, above the taskbar (generally 40px tall)
            this.Left = SystemParameters.PrimaryScreenWidth  - this.Width;
            this.Top  = SystemParameters.PrimaryScreenHeight - this.Height;

            try {
                kinect = new Kinect(s => s.Position.Z < 2 && Math.Abs(s.Position.X) < 0.35) {
                    ColorEnabled = true,
                    SkeletonTrackSeated = true
                };
            }
            catch (KinectException ke) {
                new ThemedOverlayWindow(new ThemedTextControl(ke.Message), new Size(800, 100), OverlayPosition.Center, 3);
                this.Close();
                return;
            }

            // Setup drawing and connections
            drawingGroup = new DrawingGroup();
            drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, this.Image.Width, this.Image.Height));
            imageSource = new DrawingImage(drawingGroup);
            this.Image.Source = imageSource;

            timer = new Timer(TimerInterval);
            timer.Elapsed += UpdateImage;
            timer.Elapsed += UpdateSolidWorks;
            timer.Start();

            // Setup buffers and images
            colorBitmap = new WriteableBitmap(kinect.Sensor.ColorStream.FrameWidth, kinect.Sensor.ColorStream.FrameHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);

            greenScreenPixelData = new int[kinect.Sensor.DepthStream.FramePixelDataLength];
            playerMaskImage      = new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgra32, null);

            bitmapDrawingRect = new Rect(0, 0, this.Image.Width, this.Image.Height);

            try {
                sw = new SolidWorks();
                SolidWorks.MoveToForeground();

                // Non-fatal
                if (!sw.ExplodeAnimationFound)
                    new ThemedOverlayWindow(new ThemedTextControl("No explode animation found!"), new Size(600, 100), OverlayPosition.Center, 4);

                exploder = new Exploder(sw);
            }
            catch (Exception ex) {
                // Fatal
                if (ex is SolidWorksException) {
                    if (ex.Message == "No open SolidWorks document found!")
                        new ThemedOverlayWindow(new ThemedTextControl("No active SolidWorks document found!"), new Size(800, 100), OverlayPosition.Center, 3);
                    else
                        MessageBox.Show("Error connecting to SolidWorks:\r\n" + ex.ToString(), "Kinect Rotate 3D", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (ex is TypeInitializationException) {
                    new ThemedOverlayWindow(new ThemedTextControl("Could not find SolidWorks installation!"), new Size(800, 100), OverlayPosition.Center, 3);
                }
                else {
                    MessageBox.Show("SolidWorks exception:\r\n" + ex.ToString(), "Kinect Rotate 3D", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                this.Close();
                return;
            }

            // Create the help tip window
            helpTipWindow = new ThemedOverlayWindow(new KinectHelpTipControl(), new Size(354, 204), OverlayPosition.TopCenter, double.PositiveInfinity, false);
            helpTipWindow.Topmost = true;

            // Create the help window
            helpWindow = new ThemedOverlayWindow(new KinectHelpControl(), new Size(604, 654), OverlayPosition.Center, double.PositiveInfinity, false);
            helpWindow.Topmost = true;

            banner = new Banner(true);
            hand   = new Hand();

            this.Visibility = Visibility.Hidden; // until someone starts waving

            LoadConfig();

            kinect.ColorFrameReady += kinect_ColorFrameReady;
            kinect.DepthFrameReady += kinect_DepthFrameReady;
            kinect.SkeletonReady   += kinect_SkeletonReady;
            kinect.UserInfoReady   += kinect_UserInfoReady;

            waver = new WaveDetector(kinect);
            waver.WaveStarted   += waver_WaveStarted;
            waver.WaveLost      += waver_WaveLost;
            waver.WaveCompleted += waver_WaveCompleted;
            waver.StateChanged  += waver_StateChanged;

            kinect.Start();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (helpTipWindow != null) helpTipWindow.Close();
            if (helpWindow    != null) helpWindow.Close();
            if (banner        != null) banner.Close();
            if (hand          != null) hand.Close();

            // This can sometimes take a while
            Mouse.OverrideCursor = Cursors.Wait;
            if (kinect != null) kinect.Stop();
        }

        private void WindowMouseDown(object sender, MouseButtonEventArgs e) {
            // Allow dragging anywhere
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
