using System;
using Microsoft.Kinect;

namespace Warlords.Kinect {
    public class WaveDetector {
        private Kinect kinect;
        private WaveState activeWaveState = WaveState.None;
        private HandType activeHand;

        public event EventHandler<WaveEventArgs> WaveStarted, StateChanged, WaveCompleted, WaveLost;

        public WaveDetector(Kinect kinect) {
            this.kinect = kinect;
            this.kinect.SkeletonReady += kinect_SkeletonReady;
        }

        ~WaveDetector() {
            this.kinect.SkeletonReady -= kinect_SkeletonReady;
        }

        #region Properties

        public WaveState State {
            get { return activeWaveState; }
        }

        public HandType Hand {
            get { return activeHand; }
        }

        #endregion

        private void kinect_SkeletonReady(object sender, SkeletonReadyEventArgs e) {
            if (e.PrimarySkeleton == null) return;
            Skeleton skel = e.PrimarySkeleton;

            if (e.SkeletonIsNew) Reset();

            WaveState prevWaveState = activeWaveState;
            HandType prevHand = activeHand;

            // Look for hand waving
            if (activeWaveState != WaveState.Active) {
                JointType wristJoint = 0, elbowJoint = 0;

                if (skel.Joints[JointType.WristRight].Position.Y - skel.Joints[JointType.ElbowRight].Position.Y > 0.05) {
                    activeHand = HandType.Right;
                    wristJoint = JointType.WristRight;
                    elbowJoint = JointType.ElbowRight;
                }
                else if (skel.Joints[JointType.WristLeft].Position.Y - skel.Joints[JointType.ElbowLeft].Position.Y > 0.05) {
                    activeHand = HandType.Left;
                    wristJoint = JointType.WristLeft;
                    elbowJoint = JointType.ElbowLeft;
                }
                else {
                    activeHand = HandType.None;
                    activeWaveState = WaveState.None;

                    if (prevHand != HandType.None && WaveLost != null) {
                        WaveLost(this, new WaveEventArgs(activeWaveState, activeHand, skel));
                    }
                }

                if (prevHand == HandType.None && activeHand != HandType.None && WaveStarted != null) {
                    WaveStarted(this, new WaveEventArgs(activeWaveState, activeHand, skel));
                }

                // If still waving
                if (wristJoint != 0) {
                    switch (activeWaveState) {
                    case WaveState.None:
                    case WaveState.FirstRight:
                    case WaveState.SecondRight:
                        if (skel.Joints[wristJoint].Position.X - skel.Joints[elbowJoint].Position.X < -0.04) {
                            activeWaveState++;
                        }
                        break;
                    case WaveState.FirstLeft:
                    case WaveState.SecondLeft:
                        if (skel.Joints[wristJoint].Position.X - skel.Joints[elbowJoint].Position.X > 0.03) {
                            activeWaveState++;
                        }
                        break;
                    }
                }

                if (prevWaveState != activeWaveState && StateChanged != null) {
                    StateChanged(this, new WaveEventArgs(activeWaveState, activeHand, skel));
                }
            }

            if (prevWaveState != WaveState.Active && activeWaveState == WaveState.Active && WaveCompleted != null) {
                WaveCompleted(this, new WaveEventArgs(activeWaveState, activeHand, skel));
            }
        }

        public void Reset() {
            HandType prevHand = activeHand;

            activeWaveState = WaveState.None;
            activeHand = HandType.None;

            if (prevHand != HandType.None && WaveLost != null) {
                WaveLost(this, new WaveEventArgs(activeWaveState, activeHand, null));
            }
        }
    }

    public enum WaveState {
        None,
        FirstLeft, FirstRight, SecondLeft, SecondRight,
        Active
    }

    public enum HandType {
        None  = 0,
        Left  = 1,
        Right = 2
    }
}
