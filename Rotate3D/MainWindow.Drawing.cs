using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;
using Warlords.Kinect;

namespace Rotate3D {
    public partial class MainWindow {
        private const double ImageScaleFactor = 400.0/640.0;
        private const double MaxHandCircleRadius = 45, MinHandCircleRadius = 10;

        // Drawing constructs
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;

        // Color and depth image data storage
        private WriteableBitmap colorBitmap;
        //private ColorImagePoint[] colorCoordinates;
        //private byte[] colorPixels;
        //private DepthImagePixel[] depthPixels;
        private int[] greenScreenPixelData;
        private WriteableBitmap playerMaskImage;

        private Rect bitmapDrawingRect;
        private double handCircleRadius = 0;

        // Colors for drawing
        private Pen activeHandPen = new Pen(Brushes.White, 4),
            gripInitPen   = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 3),
            gripThreshPen = new Pen(new SolidColorBrush(Color.FromArgb( 50, 255, 255, 255)), 3);
        private Brush shadowBrush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));

        //private Storyboard activeStoryboard, fadeInStoryboard = null, fadeOutStoryboard = null;

        private void UpdateImage(object sender, ElapsedEventArgs e) {
            if (colorBitmap == null) return;

            Dispatcher.Invoke(() => {
                using (DrawingContext dc = drawingGroup.Open()) {
                    // Draw color image
                    dc.DrawImage(colorBitmap, bitmapDrawingRect);

                    // If we have player/skeleton data
                    if (activeSkel != null) {
                        // Darken areas around the player using the opacity mask and the shadow brush
                        dc.PushOpacityMask(new ImageBrush(playerMaskImage));
                        dc.DrawRectangle(shadowBrush, null, bitmapDrawingRect);
                        dc.Pop();

                        // Draw the skeleton
                        DrawBonesAndJoints(activeSkel, dc);

                        // If active and gripping
                        if (waver.State == WaveState.Active && activeGrippingHand != InteractionHandType.None) {
                            // Alter opacity with radius
                            Color c = Color.FromArgb((byte)(255 - (handCircleRadius / MaxHandCircleRadius) * 255), 255, 255, 255);
                            activeHandPen.Brush = new SolidColorBrush(c);

                            // Draw the gripping hand circle around the primary hand
                            dc.DrawEllipse(null, activeHandPen, SkeletonPointToScreen(activeSkel.Joints[
                                activeGrippingHand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position),
                                handCircleRadius, handCircleRadius);

                            // If we're in explode mode
                            if (secondaryGripping) {
                                Point leftPoint = SkeletonPointToScreen(activeSkel.Joints[JointType.HandLeft].Position),
                                     rightPoint = SkeletonPointToScreen(activeSkel.Joints[JointType.HandRight].Position);

                                // Draw the gripping hand circle around the secondary hand
                                dc.DrawEllipse(null, activeHandPen, activeGrippingHand == InteractionHandType.Left ? rightPoint : leftPoint,
                                    handCircleRadius, handCircleRadius);

                                // Draw the concentric grip indication circles (initial grip radius, explode threshold, collapse threshold)
                                Point center = new Point((leftPoint.X + rightPoint.X) * 0.5, (leftPoint.Y + rightPoint.Y) * 0.5);

                                // Get the pixel radii using the scalefactor * 400 (& 0.5 for converting diameter -> radius)
                                double initGripRad =  exploder.InitialGripDistance         * ImageScaleFactor * 400 * 0.5;
                                double explodeRad  = (exploder.InitialGripDistance + 0.32) * ImageScaleFactor * 400 * 0.5; //  ~0.3 (+)
                                double collapseRad = (exploder.InitialGripDistance - 0.29) * ImageScaleFactor * 400 * 0.5; // ~-0.3 (-)

                                dc.DrawEllipse(null, gripInitPen,   center, initGripRad, initGripRad);
                                dc.DrawEllipse(null, gripThreshPen, center, explodeRad,  explodeRad);
                                dc.DrawEllipse(null, gripThreshPen, center, collapseRad, collapseRad);
                            }

                            // Loop the radius animation
                            handCircleRadius += 1.5;
                            if (handCircleRadius >= MaxHandCircleRadius) handCircleRadius = MinHandCircleRadius;
                        }
                    }
                }
            });
        }

        public void FadeIn() {
            this.Visibility = Visibility.Visible;

            this.BeginAnimation(System.Windows.Window.OpacityProperty, new DoubleAnimation {
                From = this.Opacity,
                To   = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            }, HandoffBehavior.SnapshotAndReplace);
        }

        public void FadeOut() {
            DoubleAnimation anim = new DoubleAnimation {
                From = this.Opacity,
                To   = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            };
            anim.Completed += (object sender, EventArgs e) => {
                this.Visibility = Visibility.Hidden;
            };

            this.BeginAnimation(System.Windows.Window.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
        }

        #region Skeleton Drawing (modified from SkeletonBasics-WPF sample)

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush jointBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen bonePen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 255, 0)), 5);

        // Joints to draw using the jointBrush
        private JointType[] DrawJoints = new JointType[] {
                JointType.ShoulderCenter,
                JointType.ShoulderLeft, JointType.ShoulderRight,
                JointType.ElbowLeft, JointType.ElbowRight,
                JointType.WristLeft, JointType.WristRight,
                JointType.HandLeft, JointType.HandRight };

        /// <summary>
        /// Draws a skeleton's bones and joints.
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext) {
            // Render Torso
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);

            // Left Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Render tbe selected joints
            foreach (JointType joint in DrawJoints)
                drawingContext.DrawEllipse(jointBrush, null, SkeletonPointToScreen(skeleton.Joints[joint].Position), JointThickness, JointThickness);
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point.
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint) {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = kinect.Sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X * ImageScaleFactor, depthPoint.Y * ImageScaleFactor);
        }

        /// <summary>
        /// Draws a bone line between two joints.
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1) {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked || joint1.TrackingState == JointTrackingState.NotTracked) {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred && joint1.TrackingState == JointTrackingState.Inferred) {
                return;
            }

            drawingContext.DrawLine(bonePen, SkeletonPointToScreen(joint0.Position), SkeletonPointToScreen(joint1.Position));
        }

        #endregion
    }
}
