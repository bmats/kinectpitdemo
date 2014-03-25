using System;
using Microsoft.Kinect;

namespace Warlords.Kinect {
    public class WaveEventArgs : EventArgs {
        internal WaveEventArgs(WaveState state, HandType hand, Skeleton skeleton) {
            this.State = state;
            this.Hand = hand;
            this.Skeletons = skeleton;
        }

        public WaveState State { get; private set; }

        public HandType Hand { get; private set; }

        public Skeleton Skeletons { get; private set; }
    }
}
