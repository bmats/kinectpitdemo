using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Threading;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Rotate3D {
    /// <summary>
    /// The main window, which displays the Kinect color stream and overlays.
    /// </summary>
    public partial class MainWindow : Window {
        private KinectSensor sensor;
        private InteractionStream interactStream;

        private Thread skeletonProcessingThread;

        // Gesture-triggered overlays
        private ThemedOverlayWindow helpTipWindow, helpWindow;
        private bool helpWindowVisible = false;

        public MainWindow() {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e) {
            // Move Window to the bottom left, above the taskbar (generally 40px tall)
            this.Left = SystemParameters.PrimaryScreenWidth  - this.Width;
            this.Top  = SystemParameters.PrimaryScreenHeight - this.Height - 40;

            // Find a Kinect... or not
            foreach (KinectSensor potentialSensor in KinectSensor.KinectSensors) {
                if (potentialSensor.Status == KinectStatus.Connected) {
                    this.sensor = potentialSensor;
                    break;
                }
            }
            if (this.sensor == null) {
                new ThemedOverlayWindow(new ThemedTextControl("No active Kinects found!"), new Size(600, 100), OverlayPosition.Center, 3);
                this.Close();
            }

            // Initialize the Kinect streams
            this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            this.sensor.ColorFrameReady += this.ColorFrameReady;

            this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            //this.sensor.DepthStream.Range = DepthRange.Near;
            this.sensor.DepthFrameReady += this.DepthFrameReady;

            this.sensor.SkeletonStream.Enable();
            this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated; // track upper body only
            this.sensor.SkeletonStream.EnableTrackingInNearRange = true;
            this.sensor.SkeletonFrameReady += SkeletonFrameReady;

            // Setup drawing and connections
            this.drawingGroup = new DrawingGroup();
            this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, this.Image.Width, this.Image.Height));
            this.imageSource = new DrawingImage(this.drawingGroup);
            this.Image.Source = this.imageSource;

            // Setup buffers and images
            this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth,
                this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
            this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];
            this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

            this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
            this.greenScreenPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];
            this.playerMaskImage = new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgra32, null);

            this.bitmapDrawingRect = new Rect(0, 0, this.Image.Width, this.Image.Height);

            // Start Kinecting
            this.sensor.Start();

            // Setup the interaction stream to process skeleton and depth data
            this.interactStream = new InteractionStream(this.sensor, new GlobalGripInteractionClient());
            this.interactStream.InteractionFrameReady += this.InteractionFrameReady;

            // Create the help tip window
            this.helpTipWindow = new ThemedOverlayWindow(new KinectHelpTipControl(), new Size(354, 204), OverlayPosition.TopRight, double.PositiveInfinity, false);
            this.helpTipWindow.Topmost = true;

            // Create the help window
            this.helpWindow = new ThemedOverlayWindow(new KinectHelpControl(), new Size(604, 604), OverlayPosition.Center, double.PositiveInfinity, false);
            this.helpWindow.Topmost = true;

            // Process skeletons in the background
            this.skeletonProcessingThread = new Thread(new ThreadStart(delegate() {
                while (true) this.ProcessSkeletons();
            }));
            this.skeletonProcessingThread.Start();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (this.skeletonProcessingThread != null)
                this.skeletonProcessingThread.Abort();

            if (this.helpTipWindow != null)
                this.helpTipWindow.Close();
            if (this.helpWindow != null)
                this.helpWindow.Close();

            // This can sometimes take a while
            Mouse.OverrideCursor = Cursors.Wait;
            if (this.sensor != null)
                this.sensor.Stop();
        }

        private void ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e) {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) {
                if (colorFrame == null) return;

                // Save image data
                colorFrame.CopyPixelDataTo(this.colorPixels);
                this.colorBitmap.WritePixels(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);

                // Draw new data
                UpdateImage();
            }
        }

        private void DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e) {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) {
                if (depthFrame == null) return;

                depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(this.sensor.DepthStream.Format,
                   this.depthPixels,
                   this.sensor.ColorStream.Format,
                   this.colorCoordinates);

                // Fill mask with opaque
                for (int i = 0; i < this.greenScreenPixelData.Length; i++)
                    this.greenScreenPixelData[i] = -1;

                // Loop through each pixel
                int index;
                for (int y = 0; y < 480; y++) {
                    for (int x = 0; x < 640; x++) {
                        index = x + y * 640;
                        DepthImagePixel pixel = this.depthPixels[index];

                        // Check if there is a player here
                        if (pixel.PlayerIndex > 0) {
                            ColorImagePoint colorImagePoint = this.colorCoordinates[index];

                            int colorInDepthX = colorImagePoint.X;
                            int colorInDepthY = colorImagePoint.Y;

                            // Unmask the player
                            if (colorInDepthX > 0 && colorInDepthX < 640 && colorInDepthY >= 0 && colorInDepthY < 480) {
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * 640);
                                this.greenScreenPixelData[greenScreenIndex] = 0; // non-opaque value
                            }
                        }
                    }
                }

                // Save the processed mask to the image
                this.playerMaskImage.WritePixels(new Int32Rect(0, 0, 640, 480), this.greenScreenPixelData,
                    640 * ((this.playerMaskImage.Format.BitsPerPixel + 7) / 8), 0);

                // Pass to interaction stream
                if (this.interactStream != null)
                    this.interactStream.ProcessDepth(this.depthPixels, depthFrame.Timestamp);
            }
        }

        private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e) {
            using (SkeletonFrame skelFrame = e.OpenSkeletonFrame()) {
                if (skelFrame == null) return;

                // Save skeleton data
                this.foundSkeletons = new Skeleton[skelFrame.SkeletonArrayLength];
                skelFrame.CopySkeletonDataTo(this.foundSkeletons);

                // Pass to interaction stream
                if (this.interactStream != null)
                    this.interactStream.ProcessSkeleton(this.foundSkeletons, this.sensor.AccelerometerGetCurrentReading(), skelFrame.Timestamp);
            }
        }

        private void InteractionFrameReady(object sender, InteractionFrameReadyEventArgs e) {
            using (InteractionFrame iFrame = e.OpenInteractionFrame()) {
                if (iFrame == null) return;

                // Save interaction info
                UserInfo[] info = new UserInfo[InteractionFrame.UserInfoArrayLength];
                iFrame.CopyInteractionDataTo(info);

                // Process for gripping
                ProcessUserInfo(info);
            }
        }

        private void WindowMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            // Toggle window style
            bool undecoratedMode = this.WindowStyle == WindowStyle.ToolWindow;

            this.WindowStyle     = undecoratedMode ? WindowStyle.None : WindowStyle.ToolWindow;
            this.BorderThickness = new Thickness(undecoratedMode ?  2 : 0);
            this.Image.Margin    = new Thickness(undecoratedMode ? 10 : 0);
        }
    }

    class GlobalGripInteractionClient : IInteractionClient {
        public InteractionInfo GetInteractionInfoAtLocation(int skeletonTrackingId, InteractionHandType handType, double x, double y) {
            // Allow gripping anywhere
            return new InteractionInfo {
                IsGripTarget  = true,
                IsPressTarget = false
            };
        }
    }
}
