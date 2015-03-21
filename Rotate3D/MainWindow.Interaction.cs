using System;
using System.Configuration;
using System.Linq;
using System.Timers;
using System.Windows;
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
        private Skeleton activeSkel;
        private InteractionHandType activeGrippingHand;

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
                    float newX = activeSkel.GetArmRelativeX((HandType) activeGrippingHand),
                          newY = activeSkel.GetArmRelativeY((HandType) activeGrippingHand),
                          newZ = activeSkel.GetArmRelativeZ((HandType) activeGrippingHand);

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

        private void RefreshGripHandPosition() {
            // Save current position
            prevPosition.X = activeSkel.GetArmRelativeX((HandType)activeGrippingHand);
            prevPosition.Y = activeSkel.GetArmRelativeY((HandType)activeGrippingHand);
            prevPosition.Z = activeSkel.GetArmRelativeZ((HandType)activeGrippingHand);

            // Fill position history with current position to clear previous values
            for (int i = 0; i < positionHistory.Length; i++)
                positionHistory[i] = prevPosition;
        }

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
    }
}
