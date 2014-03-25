using System;
using Microsoft.Kinect;

namespace Warlords.Kinect {
    public class SkeletonReadyEventArgs : EventArgs {
        internal SkeletonReadyEventArgs(Skeleton[] skeletons, Skeleton primarySkeleton, bool skeletonIsNew) {
            this.Skeletons = skeletons;
            this.PrimarySkeleton = primarySkeleton;
            this.SkeletonIsNew = skeletonIsNew;
        }

        public Skeleton[] Skeletons { get; private set; }

        public Skeleton PrimarySkeleton { get; private set; }

        public bool SkeletonIsNew { get; private set; }
    }
}
