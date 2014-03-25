using System;
using System.Configuration;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;
using Warlords.Kinect;

namespace Rotate3D {
    public partial class MainWindow {
        // Loaded from App.config
        private static float RotationConversion = 3.0f, ZoomConversion = 1.3f, Inertia = 0.8f;

        // Connection
        private SolidWorks sw;

        // Player data
        //private Skeleton[] foundSkeletons;
        private Skeleton activeSkel;
        private InteractionHandType activeGrippingHand;//, activeWavingHand;
        //private WaveState activeWaveState = WaveState.None;

        // Additional modes
        private Exploder exploder;
        private bool secondaryGripping = false;
        private bool isCoasting = false;

        // View states
        private float viewRotationX, viewRotationY, zoom;
        private float viewRotationXVel, viewRotationYVel, zoomVel;

        // Position caching for jitter reduction
        private SkeletonPoint prevPosition = new SkeletonPoint();
        private SkeletonPoint[] positionHistory = new SkeletonPoint[15];
        private int positionHistoryIdx = 0;

        private long guidePositionStartTime = 0;

        private void LoadConfig() {
            float temp;
            if (float.TryParse(ConfigurationManager.AppSettings["RotationConversion"], out temp)) RotationConversion = temp;
            if (float.TryParse(ConfigurationManager.AppSettings["ZoomConversion"],     out temp)) ZoomConversion     = temp;
            if (float.TryParse(ConfigurationManager.AppSettings["Inertia"],            out temp)) Inertia            = temp;
        }

        //private void ProcessSkeletons() {
        //    {
        //        if (this.foundSkeletons == null) return;

        //        // Find the first skeleton which is tracked, the primary skeleton, as selected by the SDK
        //        Skeleton bestSkel = null;
        //        foreach (Skeleton skel in this.foundSkeletons) {
        //            if (skel == null) continue;

        //            // Tracked within focus area
        //            if (skel.TrackingState == SkeletonTrackingState.Tracked &&
        //                skel.Position.Z < 2 && Math.Abs(skel.Position.X) < 0.35) {
        //                bestSkel = skel;
        //                break;
        //            }
        //        }
        //        Skeleton prevSkeleton = this.activeSkel;
        //        this.activeSkel   = bestSkel;

        //        if ((prevSkeleton == null && this.activeSkel != null) ||
        //            (prevSkeleton != null && this.activeSkel != null && prevSkeleton.TrackingId != this.activeSkel.TrackingId)) {
        //            // New skeleton found
                    
        //            // Reset
        //            this.viewRotationX = 0;
        //            this.viewRotationY = 0;
        //            this.activeWaveState = WaveState.None;
        //        }
        //        else if (prevSkeleton != null && this.activeSkel == null) {
        //            // Skeleton lost

        //            this.activeGrippingHand = InteractionHandType.None;
        //            this.helpWindowVisible  = false;
        //            this.isCoasting         = true;

        //            this.Dispatcher.Invoke(() => {
        //                this.helpTipWindow.FadeOut();
        //                this.helpWindow.FadeOut();

        //                this.FadeOut();
        //                this.banner.SlideDown();
        //                this.hand.FadeOut();
        //            });
        //        }
        //    }

        //    // Run explode animation if necessary (regardless of whether we have a skeleton)
        //    if (this.sw.Animating) this.sw.AnimateStep();

        //    if (activeSkel != null) {
        //        WaveState prevWaveState = activeWaveState;

        //        // Look for hand waving
        //        if (activeWaveState != WaveState.Active) {
        //            if (activeSkel.Joints[JointType.WristRight].Position.Y -
        //                activeSkel.Joints[JointType.ElbowRight].Position.Y > 0.05) {
        //                activeWavingHand = InteractionHandType.Right;

        //                switch (activeWaveState) {
        //                    case WaveState.None:
        //                    case WaveState.R1:
        //                    case WaveState.R2:
        //                        if (activeSkel.Joints[JointType.WristRight].Position.X -
        //                            activeSkel.Joints[JointType.ElbowRight].Position.X > 0.04) activeWaveState++;
        //                        this.hand.Dispatcher.Invoke(() => {
        //                            this.hand.Direction = Hand.Side.Right;
        //                        });
        //                        break;
        //                    case WaveState.L1:
        //                    case WaveState.L2:
        //                        if (activeSkel.Joints[JointType.WristRight].Position.X -
        //                            activeSkel.Joints[JointType.ElbowRight].Position.X < -0.03) activeWaveState++;
        //                        this.hand.Dispatcher.Invoke(() => {
        //                            this.hand.Direction = Hand.Side.Left;
        //                        });
        //                        break;
        //                }
        //            }
        //            else if (activeSkel.Joints[JointType.WristLeft].Position.Y -
        //                activeSkel.Joints[JointType.ElbowLeft].Position.Y > 0.05) {
        //                activeWavingHand = InteractionHandType.Left;

        //                switch (activeWaveState) {
        //                    case WaveState.None:
        //                    case WaveState.R1:
        //                    case WaveState.R2:
        //                        if (activeSkel.Joints[JointType.WristLeft].Position.X -
        //                            activeSkel.Joints[JointType.ElbowLeft].Position.X > 0.04) activeWaveState++;
        //                        this.hand.Dispatcher.Invoke(() => {
        //                            this.hand.Direction = Hand.Side.Right;
        //                        });
        //                        break;
        //                    case WaveState.L1:
        //                    case WaveState.L2:
        //                        if (activeSkel.Joints[JointType.WristLeft].Position.X -
        //                            activeSkel.Joints[JointType.ElbowLeft].Position.X < -0.03) activeWaveState++;
        //                        this.hand.Dispatcher.Invoke(() => {
        //                            this.hand.Direction = Hand.Side.Left;
        //                        });
        //                        break;
        //                }
        //            }
        //            else {
        //                activeWavingHand = InteractionHandType.None;
        //                activeWaveState  = WaveState.None;
        //                this.Dispatcher.Invoke(() => {
        //                    this.FadeOut();
        //                    this.hand.FadeOut();
        //                });
        //            }

        //            if (prevWaveState == WaveState.None && activeWaveState != WaveState.None) {
        //                this.Dispatcher.Invoke(() => {
        //                    this.FadeIn();
        //                    this.hand.Direction = Hand.Side.Right;
        //                    this.hand.FadeIn();
        //                });
        //            }
        //        }

        //        if (this.activeWaveState == WaveState.Active) {
        //            if (prevWaveState != WaveState.Active) {
        //                // Newly active

        //                this.Dispatcher.Invoke(() => {
        //                    this.helpTipWindow.FadeIn();
        //                    this.helpTipWindow.FadeOutAfter(6, false, true);

        //                    this.banner.SlideUp();
        //                    this.hand.Direction = Hand.Side.Right;
        //                    this.hand.FadeOut();
        //                });
        //            }

        //            // Show or hide help window
        //            bool inGuidePosition = this.activeSkel.IsInGuidePosition();
        //            if (this.activeGrippingHand == InteractionHandType.None && inGuidePosition && !this.helpWindowVisible) {
        //                if (this.guidePositionStartTime == 0) // first time in guide position
        //                    this.guidePositionStartTime = DateTime.Now.Ticks;
        //                else if (DateTime.Now.Ticks - this.guidePositionStartTime > 10000000L) { // 1 second has passed
        //                    // Display help after being held in the help position
        //                    this.helpWindow.Dispatcher.Invoke(() => {
        //                        this.helpWindow.FadeIn();
        //                    });

        //                    this.helpWindowVisible = true;
        //                }
        //            }
        //            else if ((this.activeGrippingHand != InteractionHandType.None || !inGuidePosition) && this.helpWindowVisible) {
        //                // Hide help when leaving guide position
        //                this.helpWindow.Dispatcher.Invoke(() => {
        //                    this.helpWindow.FadeOut();
        //                });

        //                this.helpWindowVisible      = false;
        //                this.guidePositionStartTime = 0;
        //            }

        //            // Process skeleton for exploding
        //            this.exploder.ProcessSkeleton(this.activeSkel, this.activeGrippingHand != InteractionHandType.None && this.secondaryGripping);

        //            // Primary gripping and moving
        //            if (!this.secondaryGripping && this.activeGrippingHand != InteractionHandType.None) {
        //                // Calculate the new arm position
        //                double newX = GetArmHorizontalPosition(this.activeGrippingHand, this.activeSkel),
        //                       newY = GetArmVerticalPosition  (this.activeGrippingHand, this.activeSkel),
        //                       newZ = GetArmZoomAmount        (this.activeGrippingHand, this.activeSkel);

        //                // Update the history with the current position
        //                this.positionHistory[this.positionHistoryIdx].X = newX;
        //                this.positionHistory[this.positionHistoryIdx].Y = newY;
        //                this.positionHistory[this.positionHistoryIdx].Z = newZ;

        //                // Average the history to remove jitter and provide easing
        //                double avgX = this.positionHistory.Select(p => p.X).Sum() / this.positionHistory.Length,
        //                       avgY = this.positionHistory.Select(p => p.Y).Sum() / this.positionHistory.Length,
        //                       avgZ = this.positionHistory.Select(p => p.Z).Sum() / this.positionHistory.Length;

        //                double xDiff = avgX - this.prevPosition.X,
        //                       yDiff = avgY - this.prevPosition.Y,
        //                       zDiff = avgZ - this.prevPosition.Z;

        //                // Convert the position to actual pixels
        //                double angleX = xDiff * RotationConversion,
        //                       angleY = yDiff * RotationConversion,
        //                      factorZ = zDiff * ZoomConversion;

        //                // Update the current position and previous position
        //                this.viewRotationX += angleX;
        //                this.viewRotationY += angleY;
        //                this.zoom          += factorZ;

        //                this.viewRotationXVel = xDiff;
        //                this.viewRotationYVel = yDiff;
        //                this.zoomVel          = zDiff;

        //                this.prevPosition.X = avgX;
        //                this.prevPosition.Y = avgY;
        //                this.prevPosition.Z = avgZ;

        //                // Update the circular history buffer index
        //                this.positionHistoryIdx = (this.positionHistoryIdx + 1) % this.positionHistory.Length;

        //                // Move the model accordingly
        //                this.sw.AdjustViewAngle(angleX, angleY);
        //                this.sw.AdjustZoom(factorZ);
        //                return;
        //            }
        //        }
        //    }

        //    if (this.isCoasting) {
        //        this.viewRotationXVel *= Inertia;
        //        this.viewRotationYVel *= Inertia;
        //        this.zoomVel *= Inertia;

        //        double xDiff = viewRotationXVel,
        //               yDiff = viewRotationYVel,
        //               zDiff = zoomVel;

        //        // If stopped moving (passed min threshold), stop coasting
        //        if (Math.Abs(this.viewRotationXVel) < 0.001 && Math.Abs(this.viewRotationYVel) < 0.001 && Math.Abs(this.zoomVel) < 0.001) {
        //            this.isCoasting = false;
        //            this.sw.PostRender();
        //        }

        //        // Convert the position to actual pixels
        //        double angleX = xDiff * RotationConversion,
        //               angleY = yDiff * RotationConversion,
        //              factorZ = zDiff * ZoomConversion;

        //        // Update the current position and previous position
        //        this.viewRotationX += angleX;
        //        this.viewRotationY += angleY;
        //        this.zoom          += factorZ;

        //        this.prevPosition.X += xDiff;
        //        this.prevPosition.Y += yDiff;
        //        this.prevPosition.Z += zDiff;

        //        // Move the model accordingly
        //        this.sw.AdjustViewAngle(angleX, angleY);
        //        this.sw.AdjustZoom(factorZ);
        //    }
        //}

        /// <summary>
        /// Process gripping
        /// </summary>
        /// <param name="info"></param>
        //private void ProcessUserInfo(UserInfo[] info) {
        //    if (this.activeSkel == null) return;

        //    // Find the hand pointers which are from the main skeleton
        //    UserInfo ui = info.FirstOrDefault(u => u.SkeletonTrackingId == activeSkel.TrackingId);
        //    if (ui == null) return;
            
        //    foreach (InteractionHandPointer hp in ui.HandPointers.Where(hp => hp.IsTracked)) {
        //        switch (hp.HandEventType) {
        //            case InteractionHandEventType.Grip:
        //                // If not gripping, start gripping
        //                if (this.activeGrippingHand == InteractionHandType.None) {
        //                    this.activeGrippingHand = hp.HandType;

        //                    // Save our new position
        //                    this.RefreshGripHandPosition();

        //                    this.isCoasting = false;
        //                    this.sw.PreRender();
        //                }
        //                // If already gripping, secondary grip
        //                else this.secondaryGripping = true;
        //                break;
        //            case InteractionHandEventType.GripRelease:
        //                // Only release the active grip
        //                if (hp.HandType == this.activeGrippingHand) {
        //                    // Transfer grip control to secondary hand if possible
        //                    if (this.secondaryGripping) {
        //                        this.activeGrippingHand = this.activeGrippingHand == InteractionHandType.Left ? InteractionHandType.Right : InteractionHandType.Left;
        //                        this.secondaryGripping  = false;

        //                        // Prevent jumping
        //                        this.RefreshGripHandPosition();
        //                    }
        //                    else {
        //                        // No secondary gripping, so stop control and activate coasting
        //                        this.activeGrippingHand = InteractionHandType.None;
        //                        this.isCoasting         = true;

        //                        this.Dispatcher.Invoke(() => {
        //                            this.helpTipWindow.FadeOut(false, true);
        //                            this.helpWindow.FadeOut(false, true);
        //                            this.helpWindowVisible = false;
        //                        });
        //                    }
        //                }
        //                else {
        //                    this.secondaryGripping = false;

        //                    // Prevent jumping
        //                    this.RefreshGripHandPosition();
        //                }
        //                break;
        //        }
        //    }
        //}

        private void RefreshGripHandPosition() {
            // Save current position
            prevPosition.X = activeSkel.GetArmRelativeX((HandType)activeGrippingHand);
            prevPosition.Y = activeSkel.GetArmRelativeY((HandType)activeGrippingHand);
            prevPosition.Z = activeSkel.GetArmRelativeZ((HandType)activeGrippingHand);

            // Fill position history with current position to clear previous values
            for (int i = 0; i < positionHistory.Length; i++)
                positionHistory[i] = prevPosition;
        }
    }
}
