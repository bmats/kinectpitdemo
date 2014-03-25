using System;
using System.Linq;
using System.Threading;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Warlords.Kinect {
    public class Kinect {
        private KinectSensor sensor;
        private InteractionStream interactStream;
        private byte[] colorPixels;
        private ColorImagePoint[] colorPoints;
        private DepthImagePixel[] depthPixels;
        private Skeleton[] foundSkeletons;
        private Skeleton primarySkel;
        private bool skelIsNew;
        private UserInfo[] userInfo;
        private Func<Skeleton, bool> primarySkelPredicate;
        private GlobalInteractionClient interactionClient;

        private AutoResetEvent colorProcBlock, depthProcBlock, skeletonProcBlock, uiProcBlock;
        private Thread colorProcThread, depthProcThread, skeletonProcThread, uiProcThread;

        public event EventHandler<ColorFrameReadyEventArgs> ColorFrameReady;
        public event EventHandler<DepthFrameReadyEventArgs> DepthFrameReady;
        public event EventHandler<SkeletonReadyEventArgs> SkeletonReady;
        public event EventHandler<UserInfoReadyEventArgs> UserInfoReady;

        public Kinect(Func<Skeleton, bool> primarySkelPredicate, string deviceConnectionId = null) {
            this.primarySkelPredicate = primarySkelPredicate;

            if (deviceConnectionId != null) {
                sensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected && s.DeviceConnectionId == deviceConnectionId);
                if (sensor == null) throw new KinectException("Could not find device with the specified connection id.");
            }
            else {
                sensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
                if (sensor == null) throw new KinectException("Could not find any connect Kinects.");
            }

            // Color stream can be enabled using the ColorEnabled property
            sensor.ColorFrameReady += sensor_ColorFrameReady;
            colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];
            colorPoints = new ColorImagePoint[sensor.DepthStream.FramePixelDataLength];

            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            sensor.DepthFrameReady += sensor_DepthFrameReady;
            depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];

            sensor.SkeletonStream.Enable();
            sensor.SkeletonStream.EnableTrackingInNearRange = true;
            sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;

            // Setup the interaction stream to process skeleton and depth data
            interactionClient = new GlobalInteractionClient(false, false);
            interactStream = new InteractionStream(sensor, interactionClient);
            interactStream.InteractionFrameReady += interactStream_InteractionFrameReady;

            colorProcBlock    = new AutoResetEvent(false);
            depthProcBlock    = new AutoResetEvent(false);
            skeletonProcBlock = new AutoResetEvent(false);
            uiProcBlock       = new AutoResetEvent(false);
        }

        public void Start() {
            sensor.Start();

            colorProcBlock.Reset();
            colorProcThread = new Thread(() => {
                while (true) {
                    colorProcBlock.WaitOne();
                    ColorFrameReady(this, new ColorFrameReadyEventArgs(colorPixels,
                        sensor.ColorStream.FrameWidth,
                        sensor.ColorStream.FrameHeight));
                    colorProcBlock.Reset();
                }
            });
            colorProcThread.Start();

            depthProcBlock.Reset();
            depthProcThread = new Thread(() => {
                while (true) {
                    depthProcBlock.WaitOne();
                    if (DepthFrameReady != null) {
                        DepthFrameReady(this, new DepthFrameReadyEventArgs(depthPixels, colorPoints));
                    }
                    depthProcBlock.Reset();
                }
            });
            depthProcThread.Start();

            skeletonProcBlock.Reset();
            skeletonProcThread = new Thread(() => {
                while (true) {
                    skeletonProcBlock.WaitOne();
                    if (SkeletonReady != null) {
                        SkeletonReady(this, new SkeletonReadyEventArgs(foundSkeletons, primarySkel, skelIsNew));
                    }
                    skeletonProcBlock.Reset();
                }
            });
            skeletonProcThread.Start();

            uiProcBlock.Reset();
            uiProcThread = new Thread(() => {
                while (true) {
                    uiProcBlock.WaitOne();
                    if (UserInfoReady != null && userInfo != null) {
                        UserInfoReady(this, new UserInfoReadyEventArgs(userInfo));
                    }
                    uiProcBlock.Reset();
                }
            });
            uiProcThread.Start();
        }

        public void Stop() {
            if (colorProcThread != null && colorProcThread.ThreadState < ThreadState.AbortRequested)
                colorProcThread.Abort();
            if (depthProcThread != null && depthProcThread.ThreadState < ThreadState.AbortRequested)
                depthProcThread.Abort();
            if (skeletonProcThread != null && skeletonProcThread.ThreadState < ThreadState.AbortRequested)
                skeletonProcThread.Abort();
            if (uiProcThread != null && uiProcThread.ThreadState < ThreadState.AbortRequested)
                uiProcThread.Abort();

            if (sensor != null) sensor.Stop();
        }

        ~Kinect() {
            Stop();
        }

        #region Properties

        public KinectSensor Sensor {
            get { return sensor; }
        }

        public bool ColorEnabled {
            get { return sensor.ColorStream.IsEnabled; }
            set {
                if (value)
                    sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                else
                    sensor.ColorStream.Disable();
            }
        }

        public bool Near {
            get { return sensor.DepthStream.Range == DepthRange.Near; }
            set { sensor.DepthStream.Range = value ? DepthRange.Near : DepthRange.Default; }
        }

        public bool SkeletonTrackSeated {
            get { return sensor.SkeletonStream.TrackingMode == SkeletonTrackingMode.Seated; }
            set { sensor.SkeletonStream.TrackingMode = value ? SkeletonTrackingMode.Seated : SkeletonTrackingMode.Default; }
        }

        public bool GripEnabled {
            get { return interactionClient.Grip; }
            set { interactionClient.Grip = value; }
        }

        public bool PressEnabled {
            get { return interactionClient.Press; }
            set { interactionClient.Press = value; }
        }

        public byte[] ColorPixels {
            get { return colorPixels; }
        }

        public DepthImagePixel[] DepthPixels {
            get { return depthPixels; }
        }

        public ColorImagePoint[] DepthColorMappedPoints {
            get { return colorPoints; }
        }

        public Skeleton[] Skeletons {
            get { return foundSkeletons; }
        }

        public Skeleton PrimarySkeleton {
            get { return primarySkel; }
        }

        public UserInfo[] UserInfo {
            get { return userInfo; }
        }

        #endregion

        #region Sensor Event Handers

        private void sensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e) {
            using (ColorImageFrame frame = e.OpenColorImageFrame()) {
                if (frame == null) return;

                // Save image data
                frame.CopyPixelDataTo(colorPixels);

                colorProcBlock.Set();
            }
        }

        private void sensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e) {
            using (DepthImageFrame frame = e.OpenDepthImageFrame()) {
                if (frame == null) return;

                frame.CopyDepthImagePixelDataTo(depthPixels);

                sensor.CoordinateMapper.MapDepthFrameToColorFrame(sensor.DepthStream.Format,
                   depthPixels, sensor.ColorStream.Format, colorPoints);

                depthProcBlock.Set();

                // Pass to interaction stream
                if (interactStream != null) {
                    interactStream.ProcessDepth(depthPixels, frame.Timestamp);
                }
            }
        }

        private void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e) {
            using (SkeletonFrame frame = e.OpenSkeletonFrame()) {
                if (frame == null || frame.SkeletonArrayLength == 0) return;

                // Save skeleton data, making a new array so there aren't any sync issues
                foundSkeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(foundSkeletons);

                Skeleton prevSkel = primarySkel;
                primarySkel = foundSkeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked && primarySkelPredicate(s));

                skelIsNew =
                    (prevSkel == null && primarySkel != null) ||
                    (prevSkel != null && primarySkel != null && prevSkel.TrackingId != primarySkel.TrackingId);

                skeletonProcBlock.Set();

                // Pass to interaction stream
                if (interactStream != null) {
                    interactStream.ProcessSkeleton(foundSkeletons, sensor.AccelerometerGetCurrentReading(), frame.Timestamp);
                }
            }
        }

        private void interactStream_InteractionFrameReady(object sender, InteractionFrameReadyEventArgs e) {
            using (InteractionFrame frame = e.OpenInteractionFrame()) {
                if (frame == null) return;

                // Save interaction info, making a new array so there aren't any sync issues
                userInfo = new UserInfo[InteractionFrame.UserInfoArrayLength];
                frame.CopyInteractionDataTo(userInfo);

                if (userInfo.Length > 0) {
                    uiProcBlock.Set();
                }
            }
        }

        #endregion

        private class GlobalInteractionClient : IInteractionClient {
            internal GlobalInteractionClient(bool grip, bool press) {
                this.Grip  = grip;
                this.Press = press;
            }

            public InteractionInfo GetInteractionInfoAtLocation(int skeletonTrackingId, InteractionHandType handType, double x, double y) {
                return new InteractionInfo {
                    IsGripTarget  = Grip,
                    IsPressTarget = Press
                };
            }

            public bool Grip { get; set; }

            public bool Press { get; set; }
        }
    }
}
