using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace Rotate3D {
    /// <summary>
    /// Contains static extension methods for use with the Kinect SDK.
    /// </summary>
    public static class KinectExtensions {
        /// <summary>
        /// Finds whether the skeleton has its left arm at a 45 degree angle and its right arm at its side (similar to Xbox guide position).
        /// </summary>
        /// <param name="skeleton">(extension) the skeleton to analyze</param>
        /// <returns>whether the skeleton is in the guide position</returns>
        public static bool IsInGuidePosition(this Skeleton skeleton) {
            SkeletonPoint
                shoulderLeft  = skeleton.Joints[JointType.ShoulderLeft].Position,
                shoulderRight = skeleton.Joints[JointType.ShoulderRight].Position,
                handLeft      = skeleton.Joints[JointType.HandLeft].Position,
                handRight     = skeleton.Joints[JointType.HandRight].Position;

            // Similar distances between left shoulder and left hand on axes (45 degrees) and
            //  left shoulder above left hand and
            //  right shoulder above right hand
            return
                Math.Abs((shoulderLeft.X - handLeft.X) - (shoulderLeft.Y - handLeft.Y)) < 0.2 &&
                Math.Abs(shoulderLeft.Z  - handLeft.Z)  < 0.25 &&
                Math.Abs(shoulderRight.X - handRight.X) < 0.1 && Math.Abs(shoulderRight.Z - handRight.Z) < 0.25;
        }
    }
}
