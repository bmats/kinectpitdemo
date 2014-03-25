using System;
using Warlords.Kinect;

namespace KinectPhotoGallery {
    public class SwipedEventArgs : EventArgs {
        internal SwipedEventArgs(float velocity, HandType hand) {
            this.Velocity = velocity;
            this.Hand     = hand;
        }

        public float Velocity { get; private set; }

        public HandType Hand { get; private set; }
    }

    public class MovedEventArgs : EventArgs {
        internal MovedEventArgs(float xOffset, float distance, HandType hand) {
            this.XOffset  = xOffset;
            this.Distance = distance;
            this.Hand     = hand;
        }

        public float XOffset { get; private set; }

        public float Distance { get; private set; }

        public HandType Hand { get; private set; }
    }

    public class PressedEventArgs : EventArgs {
        internal PressedEventArgs(HandType hand) {
            this.Hand = hand;
        }

        public HandType Hand { get; private set; }
    }
}
