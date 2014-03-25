using System;
using Microsoft.Kinect;

namespace Warlords.Kinect {
    public class DepthFrameReadyEventArgs : EventArgs {
        internal DepthFrameReadyEventArgs(DepthImagePixel[] depthPixels, ColorImagePoint[] colorPoints) {
            this.DepthPixels = depthPixels;
            this.ColorPoints = colorPoints;
        }

        public DepthImagePixel[] DepthPixels { get; private set; }

        public ColorImagePoint[] ColorPoints { get; private set; }
    }
}
