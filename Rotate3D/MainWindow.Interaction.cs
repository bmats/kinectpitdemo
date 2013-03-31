using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Rotate3D {
    public partial class MainWindow {
        private const int MaxRotatePixels = 600, MaxZoomPixels = 600;

        // Blender: VK_CONTROL, SolidWorks: VK_SHIFT
        private const int ZoomKey = VK_SHIFT;

        // Player data
        private Skeleton[] foundSkeletons;
        private Skeleton activeSkeleton;
        private InteractionHandType activeGrippingHand;

        // View states
        private int viewRotationX, viewRotationY, zoom;
        private ViewMode viewingMode = ViewMode.Rotate;

        // Position caching for jitter reduction
        private SkelHandPosition prevPosition = new SkelHandPosition();
        private SkelHandPosition[] positionHistory = new SkelHandPosition[15];
        private int positionHistoryIdx = 0;

        private long guidePositionStartTime = 0;

        // Cached actions
        private Action helpTipWindowDisplayAction, helpWindowShowAction, helpWindowHideAction;

        #region User32.dll Input and Constants

        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms646310(v=vs.85).aspx
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SendInput(int nInputs, ref INPUT pInputs, int cbSize);

        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms646270(v=vs.85).aspx
        private struct INPUT {
            public uint type;
            public MOUSEKEYBDINPUT input;
        };

        // Serves as a union for the mi and ki events in INPUT
        [StructLayout(LayoutKind.Explicit)]
        private struct MOUSEKEYBDINPUT {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms646273(v=vs.85).aspx
        private struct MOUSEINPUT {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms646271(v=vs.85).aspx
        private struct KEYBDINPUT {
            public UInt16 wVk;
            public UInt16 wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int
            MOUSEEVENTF_ABSOLUTE   = 0x8000,
            MOUSEEVENTF_MOVE       = 0x0001,
            MOUSEEVENTF_MIDDLEDOWN = 0x0020,
            MOUSEEVENTF_MIDDLEUP   = 0x0040,
            KEYEVENTF_KEYUP        = 0x0002,
            VK_SHIFT               = 0x10,
            VK_CONTROL             = 0x11,
            INPUT_MOUSE            = 0,
            INPUT_KEYBOARD         = 1;

        #endregion

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

                    // Update the history with the current position
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
                    int pixelX = (int)(xDiff * MaxRotatePixels),
                        pixelY = (int)(yDiff * MaxRotatePixels),
                        pixelZ = (int)(zDiff * MaxZoomPixels);

                    // Update the current position and previous position
                    this.viewRotationX += pixelX;
                    this.viewRotationY += pixelY;
                    this.zoom          += pixelZ;

                    this.prevPosition.X += xDiff;
                    this.prevPosition.Y += yDiff;
                    this.prevPosition.Z += zDiff;

                    // Update the circular history buffer index
                    this.positionHistoryIdx++;
                    if (this.positionHistoryIdx >= this.positionHistory.Length) this.positionHistoryIdx = 0;

                    switch (this.viewingMode) {
                        case ViewMode.Rotate:
                            if (Math.Abs(zDiff) > 0.15 && Math.Abs(xDiff) < 0.15 && Math.Abs(yDiff) < 0.15) {
                                this.viewingMode = ViewMode.Zoom;
                                this.ZoomKeyDown();
                            }
                            break;
                        case ViewMode.Zoom:
                            if ((Math.Abs(xDiff) > 0.15 || Math.Abs(yDiff) > 0.15) && Math.Abs(zDiff) < 0.15) {
                                this.viewingMode = ViewMode.Rotate;
                                this.ZoomKeyUp();
                                this.MouseUngrip();
                                this.MouseGrip();
                            }
                            break;
                    }

                    // Move the mouse accordingly
                    switch (this.viewingMode) {
                        case ViewMode.Rotate:
                            this.AdjustViewAngle(pixelX, pixelY);
                            break;
                        case ViewMode.Zoom:
                            this.AdjustViewZoom(pixelZ);
                            break;
                    }
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

                                    MouseGrip();
                                }
                                break;
                            case InteractionHandEventType.GripRelease:
                                // Only release the active grip
                                if (hp.HandType == this.activeGrippingHand) {
                                    this.activeGrippingHand = InteractionHandType.None;
                                    MouseUngrip();
                                }
                                break;
                        }
                    }
                    break;
                }
            }
        }

        #region Mouse Movement

        private void AdjustViewAngle(int pixelX, int pixelY) {
            // Invert Y for control
            pixelY *= -1;

            // Call the WinApi SendInput() function with this data structure
            var input = new INPUT {
                type  = INPUT_MOUSE,
                input = new MOUSEKEYBDINPUT {
                    mi = new MOUSEINPUT {
                        dx          = pixelX,
                        dy          = pixelY,
                        mouseData   = 0,
                        dwFlags     = MOUSEEVENTF_MOVE,
                        time        = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Move mouse relatively (rotation)
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);
        }

        private void MouseGrip() {
            var input = new INPUT {
                type = INPUT_MOUSE,
                input = new MOUSEKEYBDINPUT {
                    mi = new MOUSEINPUT {
                        dx = 32767,
                        dy = 32767,
                        mouseData = 0,
                        dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Move back to center of screen (65535 / 2)
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);

            // Middle mouse button down
            input.input.mi.dx = 0;
            input.input.mi.dy = 0;
            input.input.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN;
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);
        }

        private void MouseUngrip() {
            var input = new INPUT {
                type = INPUT_MOUSE,
                input = new MOUSEKEYBDINPUT {
                    mi = new MOUSEINPUT {
                        dx = 0,
                        dy = 0,
                        mouseData = 0,
                        dwFlags = MOUSEEVENTF_MIDDLEUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Middle mouse button up
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);

            // Move back to center of screen (65535 / 2)
            input.input.mi.dx      = 32767;
            input.input.mi.dy      = 32767;
            input.input.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);
        }

        private void AdjustViewZoom(int zoomPixel) {
            var mouseInput = new INPUT {
                type  = INPUT_MOUSE,
                input = new MOUSEKEYBDINPUT {
                    mi = new MOUSEINPUT {
                        dx          = 0,
                        dy          = zoomPixel,
                        mouseData   = 0,
                        dwFlags     = MOUSEEVENTF_MOVE,
                        time        = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Move mouse relatively (rotation)
            SendInput(1, ref mouseInput, Marshal.SizeOf(mouseInput));
            Thread.Sleep(3);
        }

        private void ZoomKeyDown() {
            var input = new INPUT {
                type  = INPUT_KEYBOARD,
                input = new MOUSEKEYBDINPUT {
                    ki = new KEYBDINPUT {
                        wVk         = ZoomKey,
                        wScan       = 0,
                        dwFlags     = 0,
                        time        = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Zoom key down
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);
        }

        private void ZoomKeyUp() {
            var input = new INPUT {
                type  = INPUT_KEYBOARD,
                input = new MOUSEKEYBDINPUT {
                    ki = new KEYBDINPUT {
                        wVk         = ZoomKey,
                        wScan       = 0,
                        dwFlags     = KEYEVENTF_KEYUP,
                        time        = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Zoom key up
            SendInput(1, ref input, Marshal.SizeOf(input));
            Thread.Sleep(3);
        }

        #endregion

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

    enum ViewMode {
        Rotate, Zoom
    }
}
