using System;

namespace Warlords.Kinect {
    public class ColorFrameReadyEventArgs : EventArgs {
        internal ColorFrameReadyEventArgs(byte[] pixels, int frameWidth, int frameHeight) {
            this.Pixels = pixels;
            this.FrameWidth = frameWidth;
            this.FrameHeight = frameHeight;
        }

        public byte[] Pixels { get; private set; }

        public int FrameWidth { get; private set; }

        public int FrameHeight { get; private set; }
    }
}
