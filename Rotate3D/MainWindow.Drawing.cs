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
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Rotate3D {
    public partial class MainWindow {
        private const double ImageScaleFactor = 400.0/640.0;
        private const double MaxHandCircleRadius = 45, MinHandCircleRadius = 10;

        // Drawing constructs
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;

        // Color and depth image data storage
        private WriteableBitmap colorBitmap;
        private ColorImagePoint[] colorCoordinates;
        private byte[] colorPixels;
        private DepthImagePixel[] depthPixels;
        private int[] greenScreenPixelData;
        private WriteableBitmap playerMaskImage;

        private Rect bitmapDrawingRect;
        private double handCircleRadius = 0;

        // Colors for drawing
        private Pen activeHandPen = new Pen(Brushes.White, 4),
            gripInitPen   = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 3),
            gripThreshPen = new Pen(new SolidColorBrush(Color.FromArgb( 50, 255, 255, 255)), 3);
        private Brush shadowBrush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));

        private void UpdateImage() {
            using (DrawingContext dc = this.drawingGroup.Open()) {
                // Draw color image
                dc.DrawImage(this.colorBitmap, this.bitmapDrawingRect);

                // If we have player/skeleton data
                if (this.activeSkeleton != null) {
                    // Darken areas around the player using the opacity mask and the shadow brush
                    dc.PushOpacityMask(new ImageBrush(this.playerMaskImage));
                    dc.DrawRectangle(this.shadowBrush, null, this.bitmapDrawingRect);
                    dc.Pop();

                    // Draw the skeleton
                    this.DrawBonesAndJoints(this.activeSkeleton, dc);

                    // If gripping
                    if (this.activeGrippingHand != InteractionHandType.None) {
                        // Alter opacity with radius
                        Color c = Color.FromArgb((byte)(255 - (this.handCircleRadius / MaxHandCircleRadius) * 255), 255, 255, 255);
                        this.activeHandPen.Brush = new SolidColorBrush(c);

                        // Draw the gripping hand circle around the primary hand
                        dc.DrawEllipse(null, activeHandPen, this.SkeletonPointToScreen(this.activeSkeleton.Joints[
                            this.activeGrippingHand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position),
                            handCircleRadius, handCircleRadius);

                        // If we're in explode mode
                        if (this.secondaryGripping) {
                            Point leftPoint = this.SkeletonPointToScreen(this.activeSkeleton.Joints[JointType.HandLeft].Position),
                                 rightPoint = this.SkeletonPointToScreen(this.activeSkeleton.Joints[JointType.HandRight].Position);

                            // Draw the gripping hand circle around the secondary hand
                            dc.DrawEllipse(null, activeHandPen, this.activeGrippingHand == InteractionHandType.Left ? rightPoint : leftPoint,
                                handCircleRadius, handCircleRadius);

                            // Draw the concentric grip indication circles (initial grip radius, explode threshold, collapse threshold)
                            Point center = new Point((leftPoint.X + rightPoint.X) * 0.5, (leftPoint.Y + rightPoint.Y) * 0.5);

                            // Get the pixel radii using the scalefactor * 400 (& 0.5 for converting diameter -> radius)
                            double initGripRad =  this.exploder.InitialGripDistance         * ImageScaleFactor * 400 * 0.5;
                            double explodeRad  = (this.exploder.InitialGripDistance + 0.32) * ImageScaleFactor * 400 * 0.5; //  ~0.3 (+)
                            double collapseRad = (this.exploder.InitialGripDistance - 0.29) * ImageScaleFactor * 400 * 0.5; // ~-0.3 (-)

                            dc.DrawEllipse(null, gripInitPen,   center, initGripRad, initGripRad);
                            dc.DrawEllipse(null, gripThreshPen, center, explodeRad,  explodeRad);
                            dc.DrawEllipse(null, gripThreshPen, center, collapseRad, collapseRad);
                        }

                        // Loop the radius animation
                        this.handCircleRadius += 1.5;
                        if (this.handCircleRadius >= MaxHandCircleRadius) this.handCircleRadius = MinHandCircleRadius;
                    }
                }
            }
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
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Render tbe selected joints
            foreach (JointType joint in DrawJoints)
                drawingContext.DrawEllipse(this.jointBrush, null, this.SkeletonPointToScreen(skeleton.Joints[joint].Position), JointThickness, JointThickness);
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point.
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint) {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
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
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked) {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred) {
                return;
            }

            drawingContext.DrawLine(this.bonePen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        #endregion
    }
}
