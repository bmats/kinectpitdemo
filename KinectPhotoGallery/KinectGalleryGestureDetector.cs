using System;
using System.Linq;
using System.Timers;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Interaction;
using Warlords.Kinect;
using System.Diagnostics;

namespace KinectPhotoGallery {
    class KinectGalleryGestureDetector {
        private const float Inertia = 0.98f, PushDistance = 0.4f, MinSwipeVelocity = 0.0009f, SwipeResetVelocity = 0.0003f;
        private const int MinSwipeTime = 400;

        private Kinect kinect;
        private WaveDetector waver;
        private HandType activeHand, prevHand;
        private bool pressing, prevPressing, swiping;
        private Timer coastTimer;
        private Skeleton activeSkel;
        private Stopwatch frameStopwatch, swipeStopwatch;
        private bool isSwiping = false;
        //private InteractionHandType activeGripHand, prevGripHand;

        private float prevX = 0.0f;
        private float[] posHist = new float[15];
        private int posHistIndex = 0;

        public event EventHandler<SwipedEventArgs> Swiped;
        public event EventHandler<MovedEventArgs> Moved;
        public event EventHandler<PressedEventArgs> Pressed;

        internal KinectGalleryGestureDetector(Kinect kinect, WaveDetector waver) {
            this.kinect = kinect;
            this.waver  = waver;

            kinect.SkeletonReady += kinect_SkeletonReady;
            kinect.UserInfoReady += kinect_UserInfoReady;

            frameStopwatch = Stopwatch.StartNew();
            swipeStopwatch = new Stopwatch();

            coastTimer = new Timer(30);
            coastTimer.Elapsed += timer_Elapsed;
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e) {
            //float avgX = posHist.Sum() / posHist.Length;

            //// Slow speed
            //float xDiff = (avgX - prevX) * Inertia;

            //posHist[posHistIndex] = prevX + xDiff;

            //// Update the circular history buffer index
            //posHistIndex = (posHistIndex + 1) % posHist.Length;

            //prevX += xDiff;

            //Moved(this, new MovedEventArgs(avgX, xDiff, HandType.None));

            //if (xDiff < 0.001) coastTimer.Stop();
        }

        #region Properties

        public Skeleton ActiveSkeleton {
            get { return activeSkel; }
        }

        #endregion

        #region Kinect Event Handlers

        private void kinect_SkeletonReady(object sender, SkeletonReadyEventArgs e) {
            if (e.PrimarySkeleton == null || waver.State != WaveState.Active) return;
            activeSkel = e.PrimarySkeleton;

            // Find if someone might be trying to pan
            if (activeSkel.Joints[JointType.WristRight].Position.Y > activeSkel.Joints[JointType.ElbowRight].Position.Y)
                activeHand = HandType.Right;
            else if (activeSkel.Joints[JointType.WristLeft].Position.Y > activeSkel.Joints[JointType.ElbowLeft].Position.Y)
                activeHand = HandType.Left;
            else
                activeHand = HandType.None;

            // If we just detected a hand, reset the history
            if (prevHand == HandType.None && activeHand != HandType.None) {
                // Save current position
                prevX = activeSkel.GetArmRelativeX(activeHand);

                // Fill position history with current position to clear previous values
                for (int i = 0; i < posHist.Length; i++) posHist[i] = prevX;
            }
            prevHand = activeHand;

            if (activeHand == HandType.None) return;

            // Calculate the new arm position
            float newX = activeSkel.GetArmRelativeX(activeHand);

            // Update the history with the current position
            posHist[posHistIndex] = newX;

            // Average the history to remove jitter and provide easing
            float avgX = posHist.Sum() / posHist.Length;
            float xDiff = avgX - prevX;

            // Update the circular history buffer index
            posHistIndex = (posHistIndex + 1) % posHist.Length;

            prevX = avgX;

            if (Moved != null) {
                Moved(this, new MovedEventArgs(avgX, xDiff, activeHand));
            }

            // Calculate velocity
            float velX = xDiff / frameStopwatch.ElapsedMilliseconds;
            //Console.WriteLine("{0}\t{1}\t{2}", velX, xDiff, frameStopwatch.ElapsedMilliseconds);
            frameStopwatch.Restart();

            swiping = Math.Abs(velX) > MinSwipeVelocity;
            if (swiping && !isSwiping) {
                if (!swipeStopwatch.IsRunning) {
                    swipeStopwatch.Restart();
                }
                else if (swipeStopwatch.ElapsedMilliseconds > MinSwipeTime) {
                    if (Swiped != null) {
                        Swiped(this, new SwipedEventArgs(velX, activeHand));
                    }
                    isSwiping = true;
                }
            }
            else if (Math.Abs(velX) < SwipeResetVelocity) {
                isSwiping = false;
            }

            //pushing = activeSkel.GetArmRelativeZ(activeHand) > PushDistance;
            //if (pushing && !prevPushing && Pushed != null) {
            //    Pushed(this, new EventArgs());
            //}
            //prevPushing = pushing;
        }

        private void kinect_UserInfoReady(object sender, UserInfoReadyEventArgs e) {
            if (activeSkel == null) return;

            // Find the hand pointers which are from the main skeleton
            UserInfo info = e.UserInfo.FirstOrDefault(ui => ui.SkeletonTrackingId == activeSkel.TrackingId);
            if (info == null) return;

            InteractionHandPointer pointer = info.HandPointers.FirstOrDefault(hp => hp.IsTracked && (HandType)hp.HandType == activeHand);
            if (pointer != null) {
                pressing = pointer.IsPressed;
                if (pressing && !prevPressing && Pressed != null) {
                    Pressed(this, new PressedEventArgs(activeHand));
                }
                prevPressing = pressing;
            }

            //foreach (InteractionHandPointer hp in info.HandPointers.Where(hp => hp.IsTracked)) {
            //    switch (hp.HandEventType) {
            //        case InteractionHandEventType.Grip:
            //            // If not gripping, start gripping
            //            if (activeGripHand == InteractionHandType.None) {
            //                activeGripHand = hp.HandType;
            //            }
            //            break;
            //        case InteractionHandEventType.GripRelease:
            //            // Only release the active grip
            //            if (hp.HandType == activeGripHand) {
            //                activeGripHand = InteractionHandType.None;
            //                coastTimer.Start();
            //            }
            //            break;
            //    }
            //}
        }

        #endregion
    }

    enum SwipeDirection {
        Left, Right
    }
}
