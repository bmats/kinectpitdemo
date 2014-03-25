using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;
using Warlords.Kinect;

namespace Rotate3D {
    /// <summary>
    /// The main window, which displays the Kinect color stream and overlays.
    /// </summary>
    public partial class MainWindow : Window {
        private Kinect kinect;
        private WaveDetector waver;
        //private KinectSensor sensor;
        //private InteractionStream interactStream;
        //private UserInfo[] processInfo;

        //private Thread skeletonProcessingThread, depthProcessingThread, processThread;
        //private AutoResetEvent skeletonProcessingBlocker, depthProcessingBlocker, processBlocker;
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

            // Find a Kinect... or not
            //foreach (KinectSensor potentialSensor in KinectSensor.KinectSensors) {
            //    if (potentialSensor.Status == KinectStatus.Connected) {
            //        this.sensor = potentialSensor;
            //        break;
            //    }
            //}
            //if (this.sensor == null) {
            //    new ThemedOverlayWindow(new ThemedTextControl("No active Kinects found!"), new Size(600, 100), OverlayPosition.Center, 3);
            //    this.Close();
            //    return;
            //}

            // Initialize the Kinect streams
            //this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            //this.sensor.ColorFrameReady += this.ColorFrameReady;

            //this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            //this.sensor.DepthStream.Range = DepthRange.Near;
            //this.sensor.DepthFrameReady += this.DepthFrameReady;

            //this.sensor.SkeletonStream.Enable();
            //this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated; // track upper body only
            //this.sensor.SkeletonStream.EnableTrackingInNearRange = true;
            //this.sensor.SkeletonFrameReady += SkeletonFrameReady;

            // Setup drawing and connections
            drawingGroup = new DrawingGroup();
            drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, this.Image.Width, this.Image.Height));
            imageSource = new DrawingImage(drawingGroup);
            this.Image.Source = imageSource;

            timer = new Timer(TimerInterval);
            timer.Elapsed += UpdateImage;
            timer.Elapsed += UpdateSolidWorks;
            timer.Start();
            //drawTimer = new Timer(UpdateImage, null, MinProcessTime, MinProcessTime);

            // Setup buffers and images
            colorBitmap = new WriteableBitmap(kinect.Sensor.ColorStream.FrameWidth, kinect.Sensor.ColorStream.FrameHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);
            //colorCoordinates = new ColorImagePoint[kinect.Sensor.DepthStream.FramePixelDataLength];
            //colorPixels = new byte[kinect.Sensor.ColorStream.FramePixelDataLength];

            //depthPixels = new DepthImagePixel[kinect.Sensor.DepthStream.FramePixelDataLength];
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

                //this.sensor.Stop();
                this.Close();
                return;
            }

            // Start Kinecting
            //this.sensor.Start();

            // Setup the interaction stream to process skeleton and depth data
            //this.interactStream = new InteractionStream(this.sensor, new GlobalGripInteractionClient());
            //this.interactStream.InteractionFrameReady += this.InteractionFrameReady;

            // Create the help tip window
            helpTipWindow = new ThemedOverlayWindow(new KinectHelpTipControl(), new Size(354, 204), OverlayPosition.TopCenter, double.PositiveInfinity, false);
            helpTipWindow.Topmost = true;

            // Create the help window
            helpWindow = new ThemedOverlayWindow(new KinectHelpControl(), new Size(604, 654), OverlayPosition.Center, double.PositiveInfinity, false);
            helpWindow.Topmost = true;

            banner = new Banner(true);
            hand   = new Hand();

            // Process skeletons in the background
            //skeletonProcessingBlocker = new AutoResetEvent(false);
            //skeletonProcessingThread = new Thread(() => {
            //    while (true) {
            //        skeletonProcessingBlocker.WaitOne();
            //        ProcessSkeletons();
            //        skeletonProcessingBlocker.Reset();
            //    }
            //});
            //this.skeletonProcessingThread.Start();

            //this.depthProcessingBlocker = new AutoResetEvent(false);
            //this.depthProcessingThread = new Thread(DepthFrameProcessThread);
            //this.depthProcessingThread.Start();

            //this.processBlocker = new AutoResetEvent(false);
            //this.processThread = new Thread(() => {
            //    while (true) {
            //        this.processBlocker.WaitOne();
            //        if (processInfo != null)
            //            ProcessUserInfo(processInfo);
            //        this.processBlocker.Reset();
            //    }
            //});
            //this.processThread.Start();

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
            //if (skeletonProcessingThread != null)
            //    skeletonProcessingThread.Abort();
            //if (depthProcessingThread != null)
            //    depthProcessingThread.Abort();
            //if (processThread != null)
            //    processThread.Abort();

            if (helpTipWindow != null) helpTipWindow.Close();
            if (helpWindow    != null) helpWindow.Close();
            if (banner        != null) banner.Close();
            if (hand          != null) hand.Close();

            // This can sometimes take a while
            Mouse.OverrideCursor = Cursors.Wait;
            if (kinect != null) kinect.Stop();
        }

        #region Kinect Handlers

        private void kinect_ColorFrameReady(object sender, ColorFrameReadyEventArgs e) {
            Dispatcher.Invoke(() => {
                colorBitmap.WritePixels(new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
                    e.Pixels,
                    colorBitmap.PixelWidth * sizeof(int), 0);
            });
        }

        private void kinect_DepthFrameReady(object sender, DepthFrameReadyEventArgs e) {
            // Fill mask with opaque
            for (int i = 0; i < greenScreenPixelData.Length; i++) greenScreenPixelData[i] = -1;

            // Loop through each pixel
            int index;
            for (int y = 0; y < 480; y++) {
                for (int x = 0; x < 640; x++) {
                    index = x + y * 640;
                    DepthImagePixel pixel = e.DepthPixels[index];

                    // Check if there is a player here
                    if (pixel.PlayerIndex > 0) {
                        ColorImagePoint colorImagePoint = e.ColorPoints[index];

                        int colorInDepthX = colorImagePoint.X,
                            colorInDepthY = colorImagePoint.Y;

                        // Unmask the player
                        if (colorInDepthX > 0 && colorInDepthX < 640 && colorInDepthY >= 0 && colorInDepthY < 480) {
                            int greenScreenIndex = colorInDepthX + (colorInDepthY * 640);
                            greenScreenPixelData[greenScreenIndex] = 0; // non-opaque value
                        }
                    }
                }
            }

            // Save the processed mask to the image
            Dispatcher.Invoke(() => {
                playerMaskImage.WritePixels(new Int32Rect(0, 0, 640, 480), greenScreenPixelData,
                    640 * ((playerMaskImage.Format.BitsPerPixel + 7) / 8), 0);
            });
        }

        private void waver_WaveStarted(object sender, WaveEventArgs e) {
            Dispatcher.Invoke(() => {
                FadeIn();
                hand.Direction = Hand.Side.Right;
                hand.FadeIn();
            });
        }

        private void waver_WaveLost(object sender, WaveEventArgs e) {
            Dispatcher.Invoke(() => {
                FadeOut();
                hand.FadeOut();
            });
        }

        private void waver_WaveCompleted(object sender, WaveEventArgs e) {
            Dispatcher.Invoke(() => {
                helpTipWindow.FadeIn();
                helpTipWindow.FadeOutAfter(6, false, true);

                banner.SlideUp();
                hand.Direction = Hand.Side.Right;
                hand.FadeOut();
            });
        }

        private void waver_StateChanged(object sender, WaveEventArgs e) {
            switch (e.State) {
                case WaveState.None:
                case WaveState.FirstRight:
                case WaveState.SecondRight:
                case WaveState.Active:
                    this.hand.Dispatcher.Invoke(() => {
                        this.hand.Direction = Hand.Side.Right;
                    });
                    break;
                case WaveState.FirstLeft:
                case WaveState.SecondLeft:
                    this.hand.Dispatcher.Invoke(() => {
                        this.hand.Direction = Hand.Side.Left;
                    });
                    break;
            }
        }

        private void kinect_SkeletonReady(object sender, SkeletonReadyEventArgs e) {
            Skeleton prevSkel = activeSkel;
            activeSkel = e.PrimarySkeleton;

            if ((prevSkel == null && activeSkel != null) ||
                (prevSkel != null && activeSkel != null && prevSkel.TrackingId != activeSkel.TrackingId)) {
                // New skeleton found
                    
                // Reset
                viewRotationX = 0;
                viewRotationY = 0;
            }
            else if (prevSkel != null && activeSkel == null) {
                // Skeleton lost

                activeGrippingHand = InteractionHandType.None;
                helpWindowVisible  = false;
                isCoasting         = true;

                Dispatcher.Invoke(() => {
                    helpTipWindow.FadeOut();
                    helpWindow.FadeOut();

                    FadeOut();
                    banner.SlideDown();
                    hand.FadeOut();
                });
            }

            if (activeSkel != null && waver.State == WaveState.Active) {
                #region Show or hide help window

                bool inGuidePosition = activeSkel.IsInGuidePosition();
                if (activeGrippingHand == InteractionHandType.None && inGuidePosition && !helpWindowVisible) {
                    // If first time in guide position
                    if (guidePositionStartTime == 0) {
                        guidePositionStartTime = DateTime.Now.Ticks;
                    }
                    else if (DateTime.Now.Ticks - guidePositionStartTime > 10000000L) { // 1 second has passed
                        // Display help after being held in the help position
                        helpWindow.Dispatcher.Invoke(() => {
                            helpWindow.FadeIn();
                        });

                        helpWindowVisible = true;
                    }
                }
                else if ((activeGrippingHand != InteractionHandType.None || !inGuidePosition) && helpWindowVisible) {
                    // Hide help when leaving guide position
                    helpWindow.Dispatcher.Invoke(() => {
                        helpWindow.FadeOut();
                    });

                    helpWindowVisible      = false;
                    guidePositionStartTime = 0;
                }

                #endregion

                // Process skeleton for exploding
                exploder.ProcessSkeleton(activeSkel, activeGrippingHand != InteractionHandType.None && secondaryGripping);

                // Primary gripping and moving
                if (!secondaryGripping && activeGrippingHand != InteractionHandType.None) {
                    // Calculate the new arm position
                    float newX = activeSkel.GetArmRelativeX((HandType)activeGrippingHand),
                          newY = activeSkel.GetArmRelativeY((HandType)activeGrippingHand),
                          newZ = activeSkel.GetArmRelativeZ((HandType)activeGrippingHand);

                    // Update the history with the current position
                    positionHistory[positionHistoryIdx].X = newX;
                    positionHistory[positionHistoryIdx].Y = newY;
                    positionHistory[positionHistoryIdx].Z = newZ;

                    // Average the history to remove jitter and provide easing
                    float avgX = positionHistory.Select(p => p.X).Sum() / positionHistory.Length,
                          avgY = positionHistory.Select(p => p.Y).Sum() / positionHistory.Length,
                          avgZ = positionHistory.Select(p => p.Z).Sum() / positionHistory.Length;

                    float xDiff = avgX - prevPosition.X,
                          yDiff = avgY - prevPosition.Y,
                          zDiff = avgZ - prevPosition.Z;

                    // Convert the position to actual pixels
                    float angleX = xDiff * RotationConversion,
                          angleY = yDiff * RotationConversion,
                         factorZ = zDiff * ZoomConversion;

                    // Update the current position and previous position
                    viewRotationX += angleX;
                    viewRotationY += angleY;
                    zoom          += factorZ;

                    viewRotationXVel = xDiff;
                    viewRotationYVel = yDiff;
                    zoomVel          = zDiff;

                    prevPosition.X = avgX;
                    prevPosition.Y = avgY;
                    prevPosition.Z = avgZ;

                    // Update the circular history buffer index
                    positionHistoryIdx = (positionHistoryIdx + 1) % positionHistory.Length;

                    // Move the model accordingly in timer thread
                    nextAngleX  = angleX;
                    nextAngleY  = angleY;
                    nextFactorZ = factorZ;
                    swNext = true;
                }
            }
        }

        private void kinect_UserInfoReady(object sender, UserInfoReadyEventArgs e) {
            if (activeSkel == null) return;

            // Find the hand pointers which are from the main skeleton
            UserInfo ui = e.UserInfo.FirstOrDefault(u => u.SkeletonTrackingId == activeSkel.TrackingId);
            if (ui == null) return;

            foreach (InteractionHandPointer hp in ui.HandPointers.Where(hp => hp.IsTracked)) {
                switch (hp.HandEventType) {
                    case InteractionHandEventType.Grip:
                        // If not gripping, start gripping
                        if (activeGrippingHand == InteractionHandType.None) {
                            activeGrippingHand = hp.HandType;

                            // Save our new position
                            RefreshGripHandPosition();

                            isCoasting = false;
                            sw.PreRender();
                        }
                        // If already gripping, secondary grip
                        else secondaryGripping = true;
                        break;
                    case InteractionHandEventType.GripRelease:
                        // Only release the active grip
                        if (hp.HandType == this.activeGrippingHand) {
                            // Transfer grip control to secondary hand if possible
                            if (secondaryGripping) {
                                activeGrippingHand = activeGrippingHand == InteractionHandType.Left ? InteractionHandType.Right : InteractionHandType.Left;
                                secondaryGripping = false;

                                // Prevent jumping
                                RefreshGripHandPosition();
                            }
                            else {
                                // No secondary gripping, so stop control and activate coasting
                                activeGrippingHand = InteractionHandType.None;
                                isCoasting = true;

                                Dispatcher.Invoke(() => {
                                    helpTipWindow.FadeOut(false, true);
                                    helpWindow.FadeOut(false, true);
                                    helpWindowVisible = false;
                                });
                            }
                        }
                        else {
                            secondaryGripping = false;

                            // Prevent jumping
                            RefreshGripHandPosition();
                        }
                        break;
                }
            }
        }

        #endregion

        private void UpdateSolidWorks(object sender, ElapsedEventArgs e) {
            // Run explode animation if necessary (regardless of whether we have a skeleton)
            if (sw.Animating) sw.AnimateStep();

            if (swNext) {
                sw.AdjustViewAngle(nextAngleX, nextAngleY);
                sw.AdjustZoom(nextFactorZ);
                swNext = false;
            }

            if (isCoasting) {
                viewRotationXVel *= Inertia;
                viewRotationYVel *= Inertia;
                zoomVel          *= Inertia;

                float xDiff = viewRotationXVel,
                      yDiff = viewRotationYVel,
                      zDiff = zoomVel;

                // If stopped moving (passed min threshold), stop coasting
                if (Math.Abs(viewRotationXVel) < 0.001f && Math.Abs(viewRotationYVel) < 0.001f && Math.Abs(zoomVel) < 0.001f) {
                    isCoasting = false;
                    sw.PostRender();
                }

                // Convert the position to actual pixels
                float angleX = xDiff * RotationConversion,
                      angleY = yDiff * RotationConversion,
                     factorZ = zDiff * ZoomConversion;

                // Update the current position and previous position
                viewRotationX += angleX;
                viewRotationY += angleY;
                zoom          += factorZ;

                prevPosition.X += xDiff;
                prevPosition.Y += yDiff;
                prevPosition.Z += zDiff;

                // Move the model accordingly
                sw.AdjustViewAngle(angleX, angleY);
                sw.AdjustZoom(factorZ);
            }
        }

        //private void ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e) {
        //    using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) {
        //        if (colorFrame == null) return;

        //        // Save image data
        //        colorFrame.CopyPixelDataTo(this.colorPixels);
        //        this.colorBitmap.WritePixels(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
        //            this.colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);
        //    }
        //}

        //private void DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e) {
        //    using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) {
        //        if (depthFrame == null) return;

        //        depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

        //        this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(this.sensor.DepthStream.Format,
        //           this.depthPixels,
        //           this.sensor.ColorStream.Format,
        //           this.colorCoordinates);

        //        this.depthProcessingBlocker.Set();

        //        // Pass to interaction stream
        //        if (this.interactStream != null)
        //            this.interactStream.ProcessDepth(this.depthPixels, depthFrame.Timestamp);
        //    }
        //}

        //private void DepthFrameProcessThread() {
        //    while (true) {
        //        this.depthProcessingBlocker.WaitOne();

        //        // Fill mask with opaque
        //        for (int i = 0; i < this.greenScreenPixelData.Length; i++)
        //            this.greenScreenPixelData[i] = -1;

        //        // Loop through each pixel
        //        int index;
        //        for (int y = 0; y < 480; y++) {
        //            for (int x = 0; x < 640; x++) {
        //                index = x + y * 640;
        //                DepthImagePixel pixel = this.depthPixels[index];

        //                // Check if there is a player here
        //                if (pixel.PlayerIndex > 0) {
        //                    ColorImagePoint colorImagePoint = this.colorCoordinates[index];

        //                    int colorInDepthX = colorImagePoint.X;
        //                    int colorInDepthY = colorImagePoint.Y;

        //                    // Unmask the player
        //                    if (colorInDepthX > 0 && colorInDepthX < 640 && colorInDepthY >= 0 && colorInDepthY < 480) {
        //                        int greenScreenIndex = colorInDepthX + (colorInDepthY * 640);
        //                        this.greenScreenPixelData[greenScreenIndex] = 0; // non-opaque value
        //                    }
        //                }
        //            }
        //        }

        //        this.Dispatcher.Invoke(() => {
        //            // Save the processed mask to the image
        //            this.playerMaskImage.WritePixels(new Int32Rect(0, 0, 640, 480), this.greenScreenPixelData,
        //                640 * ((this.playerMaskImage.Format.BitsPerPixel + 7) / 8), 0);
        //        });

        //        this.depthProcessingBlocker.Reset();
        //    }
        //}

        //private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e) {
        //    using (SkeletonFrame skelFrame = e.OpenSkeletonFrame()) {
        //        if (skelFrame == null) return;

        //        // Save skeleton data
        //        this.foundSkeletons = new Skeleton[skelFrame.SkeletonArrayLength];
        //        skelFrame.CopySkeletonDataTo(this.foundSkeletons);
        //        this.skeletonProcessingBlocker.Set(); // new data available, proceed

        //        // Pass to interaction stream
        //        if (this.interactStream != null)
        //            this.interactStream.ProcessSkeleton(this.foundSkeletons, this.sensor.AccelerometerGetCurrentReading(), skelFrame.Timestamp);
        //    }
        //}
        
        //private void InteractionFrameReady(object sender, InteractionFrameReadyEventArgs e) {
        //    using (InteractionFrame iFrame = e.OpenInteractionFrame()) {
        //        if (iFrame == null) return;

        //        // Save interaction info
        //        UserInfo[] info = new UserInfo[InteractionFrame.UserInfoArrayLength];
        //        iFrame.CopyInteractionDataTo(info);

        //        // Process for gripping
        //        this.processInfo = info;
        //        this.processBlocker.Set();
        //    }
        //}

        private void WindowMouseDown(object sender, MouseButtonEventArgs e) {
            // Allow dragging anywhere
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }

    //class GlobalGripInteractionClient : IInteractionClient {
    //    public InteractionInfo GetInteractionInfoAtLocation(int skeletonTrackingId, InteractionHandType handType, double x, double y) {
    //        // Allow gripping anywhere
    //        return new InteractionInfo {
    //            IsGripTarget  = true,
    //            IsPressTarget = false
    //        };
    //    }
    //}
}
