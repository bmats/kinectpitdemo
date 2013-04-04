using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Rotate3D {
    public partial class MainWindow {
        private const double RotationConversion = 3, ZoomConversion = 2;

        // Connection
        private SolidWorks sw = new SolidWorks();

        // Player data
        private Skeleton[] foundSkeletons;
        private Skeleton activeSkeleton;
        private InteractionHandType activeGrippingHand;

        // View states
        private double viewRotationX, viewRotationY, zoom;

        // Position caching for jitter reduction
        private SkelHandPosition prevPosition = new SkelHandPosition();
        private SkelHandPosition[] positionHistory = new SkelHandPosition[15];
        private int positionHistoryIdx = 0;

        private long guidePositionStartTime = 0;

        // Cached actions
        private Action helpTipWindowDisplayAction, helpWindowShowAction, helpWindowHideAction;

        private void ProcessSkeletons() {
            {
                if (this.foundSkeletons == null) return;

                // Find the first skeleton which is tracked, the primary skeleton, as selected by the SDK
                Skeleton bestSkel = null;
                foreach (Skeleton skel in this.foundSkeletons) {
                    if (skel == null) continue;

                    // Tracked within focus area
                    if (skel.TrackingState == SkeletonTrackingState.Tracked &&
                        skel.Position.Z < 2 && Math.Abs(skel.Position.X) < 0.35) {
                        bestSkel = skel;
                        break;
                    }
                }
                Skeleton prevSkeleton = this.activeSkeleton;
                this.activeSkeleton   = bestSkel;

                // If new skeleton found
                if ((prevSkeleton == null && this.activeSkeleton != null) ||
                    (prevSkeleton != null && this.activeSkeleton != null && prevSkeleton.TrackingId != this.activeSkeleton.TrackingId)) {
                    
                    // Reset
                    this.viewRotationX = 0;
                    this.viewRotationY = 0;

                    // Display new skeleton help tip
                    if (this.helpTipWindowDisplayAction == null)
                        this.helpTipWindowDisplayAction = new Action(delegate() {
                            this.helpTipWindow.FadeIn();
                            this.helpTipWindow.FadeOutAfter(6, false, true);
                        });
                    this.helpTipWindow.Dispatcher.Invoke(this.helpTipWindowDisplayAction);
                }
            }

            if (this.activeSkeleton != null) {
                // Show or hide help window
                bool inGuidePosition = this.activeSkeleton.IsInGuidePosition();
                if (this.activeGrippingHand == InteractionHandType.None && inGuidePosition && !this.helpWindowVisible) {
                    if (this.guidePositionStartTime == 0) // first time in guide position
                        this.guidePositionStartTime = DateTime.Now.Ticks;
                    else if (DateTime.Now.Ticks - this.guidePositionStartTime > 10000000L) { // 1 second has passed
                        // Display help after being held in the help position
                        if (this.helpWindowShowAction == null)
                            this.helpWindowShowAction = new Action(delegate() {
                                this.helpWindow.FadeIn();
                            });
                        this.helpWindow.Dispatcher.Invoke(this.helpWindowShowAction);

                        this.helpWindowVisible = true;
                    }
                }
                else if ((this.activeGrippingHand != InteractionHandType.None || !inGuidePosition) && this.helpWindowVisible) {
                    // Hide help when leaving guide position
                    if (this.helpWindowHideAction == null)
                        this.helpWindowHideAction = new Action(delegate() {
                            this.helpWindow.FadeOut();
                        });
                    this.helpWindow.Dispatcher.Invoke(this.helpWindowHideAction);

                    this.helpWindowVisible      = false;
                    this.guidePositionStartTime = 0;
                }

                // Gripping and moving
                if (this.activeGrippingHand != InteractionHandType.None) {
                    // Calculate the new arm position
                    double newX = GetArmHorizontalPosition(this.activeGrippingHand, this.activeSkeleton),
                           newY = GetArmVerticalPosition  (this.activeGrippingHand, this.activeSkeleton),
                           newZ = GetArmZoomAmount        (this.activeGrippingHand, this.activeSkeleton);

                    // Update the history with the curent position
                    this.positionHistory[this.positionHistoryIdx].X = newX;
                    this.positionHistory[this.positionHistoryIdx].Y = newY;
                    this.positionHistory[this.positionHistoryIdx].Z = newZ;

                    // Average the history to remove jitter and provide easing
                    double avgXDiff = this.positionHistory.Select(p => p.X).Sum() / this.positionHistory.Length,
                           avgYDiff = this.positionHistory.Select(p => p.Y).Sum() / this.positionHistory.Length,
                           avgZDiff = this.positionHistory.Select(p => p.Z).Sum() / this.positionHistory.Length;

                    double xDiff = avgXDiff - this.prevPosition.X,
                           yDiff = avgYDiff - this.prevPosition.Y,
                           zDiff = avgZDiff - this.prevPosition.Z;

                    // Convert the position to actual pixels
                    double angleX = xDiff * RotationConversion,
                           angleY = yDiff * RotationConversion,
                          factorZ = zDiff * ZoomConversion;

                    // Update the current position and previous position
                    this.viewRotationX += angleX;
                    this.viewRotationY += angleY;
                    this.zoom          += factorZ;

                    this.prevPosition.X += xDiff;
                    this.prevPosition.Y += yDiff;
                    this.prevPosition.Z += zDiff;

                    // Update the circular history buffer index
                    this.positionHistoryIdx++;
                    if (this.positionHistoryIdx >= this.positionHistory.Length) this.positionHistoryIdx = 0;

                    // Move the model accordingly
                    this.sw.AdjustViewAngle(angleX, angleY);
                    this.sw.AdjustZoom(factorZ);
                }
            }
        }

        private void ProcessUserInfo(UserInfo[] info) {
            if (this.activeSkeleton == null) return;

            foreach (UserInfo ui in info) {
                // Find the hand pointers which are from the main skeleton
                if (ui.SkeletonTrackingId == this.activeSkeleton.TrackingId) {
                    foreach (InteractionHandPointer hp in ui.HandPointers.Where(hp => hp.IsTracked)) {
                        switch (hp.HandEventType) {
                            case InteractionHandEventType.Grip:
                                // If not gripping, start gripping
                                if (this.activeGrippingHand == InteractionHandType.None) {
                                    this.activeGrippingHand = hp.HandType;

                                    // Save current position
                                    this.prevPosition.X = GetArmHorizontalPosition(hp.HandType, this.activeSkeleton);
                                    this.prevPosition.Y = GetArmVerticalPosition  (hp.HandType, this.activeSkeleton);
                                    this.prevPosition.Z = GetArmZoomAmount        (hp.HandType, this.activeSkeleton);

                                    // Fill position history
                                    for (int i = 0; i < this.positionHistory.Length; i++)
                                        this.positionHistory[i] = this.prevPosition;
                                    this.positionHistoryIdx = 0;

                                    this.sw.PreRender();
                                }
                                break;
                            case InteractionHandEventType.GripRelease:
                                // Only release the active grip
                                if (hp.HandType == this.activeGrippingHand) {
                                    this.activeGrippingHand = InteractionHandType.None;
                                    this.sw.PostRender();
                                }
                                break;
                        }
                    }
                    break;
                }
            }
        }

        #region Hand Position/Angle Calculation

        /// <summary>
        /// Gets the hand's position along the x-axis relative to the shoulder center.
        /// </summary>
        /// <param name="hand">The hand whose position to report.</param>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <returns>The position, from -1.0 to 1.0 (frame width?).</returns>
        private static double GetArmHorizontalPosition(InteractionHandType hand, Skeleton skel) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint   = skel.Joints[hand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distance
            return handPoint.X - centerPoint.X;
        }

        /// <summary>
        /// Gets the hand's position along the y-axis relative to the shoulder center.
        /// </summary>
        /// <param name="hand">The hand whose position to report.</param>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <returns>The position, from -1.0 to 1.0 (frame width?).</returns>
        private static double GetArmVerticalPosition(InteractionHandType hand, Skeleton skel) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distance
            return handPoint.Y - centerPoint.Y;
        }

        /// <summary>
        /// Gets the hand's position along the z-axis relative to the shoulder center.
        /// </summary>
        /// <param name="hand">The hand whose position to report.</param>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <returns>The zoom amount.</returns>
        private static double GetArmZoomAmount(InteractionHandType hand, Skeleton skel) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distance
            return centerPoint.Z - handPoint.Z;
        }

        /// <summary>
        /// Not used.
        /// </summary>
        private static double GetArmHorizontalAngle(InteractionHandType hand, Skeleton skel) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distances
            float z = handPoint.Z - centerPoint.Z,
                  x = handPoint.X - centerPoint.X;

            // Calculate the angle, in radians
            double angle = Math.Abs(Math.Atan(x / z));

            // atan only reports reference angles; fix them to go the whole 2PI
            if (z > 0) angle = Math.PI - angle;     // +z (behind), from PI/2 to 3PI/2
            if (x < 0) angle = Math.PI * 2 - angle; // -x (left), from PI to 2PI

            // Clamp
            angle = Math.Max(0, angle);
            angle = Math.Min(angle, Math.PI * 2);

            return angle;
        }

        /// <summary>
        /// Not used.
        /// </summary>
        private static double GetArmVerticalAngle(InteractionHandType hand, Skeleton skel) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == InteractionHandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distances
            float y = handPoint.Y - centerPoint.Y,
                  x = handPoint.X - centerPoint.X;

            // Calculate the angle, in radians
            double angle = Math.Abs(Math.Atan(x / y));

            // atan only reports reference angles; fix them to go the whole 2PI
            if (y < 0) angle = Math.PI - angle;     // -y (below), from PI/2 to 3PI/2
            if (x < 0) angle = Math.PI * 2 - angle; // -x (left), from PI to 2PI

            // Clamp
            angle = Math.Max(0, angle);
            angle = Math.Min(angle, Math.PI * 2);

            return angle;
        }

        #endregion
    }

    struct SkelHandPosition {
        public double X, Y, Z;
    }
}
