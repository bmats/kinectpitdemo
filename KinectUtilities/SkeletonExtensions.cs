using System;
using Microsoft.Kinect;

namespace Warlords.Kinect {
    public static class SkeletonExtensions {
        /// <summary>
        /// Gets the hand's position along the x-axis relative to the shoulder center.
        /// </summary>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <param name="hand">The hand whose position to report.</param>
        /// <returns>The position, from -1.0 to 1.0 (frame width?).</returns>
        public static float GetArmRelativeX(this Skeleton skel, HandType hand) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == HandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distance
            return handPoint.X - centerPoint.X;
        }

        /// <summary>
        /// Gets the hand's position along the y-axis relative to the shoulder center.
        /// </summary>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <param name="hand">The hand whose position to report.</param>
        /// <returns>The position, from -1.0 to 1.0 (frame width?).</returns>
        public static float GetArmRelativeY(this Skeleton skel, HandType hand) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == HandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            // Find the axis distance
            return handPoint.Y - centerPoint.Y;
        }

        /// <summary>
        /// Gets the hand's position along the z-axis relative to the shoulder center.
        /// </summary>
        /// <param name="skel">The skeleton to analyze.</param>
        /// <param name="hand">The hand whose position to report.</param>
        /// <returns>The zoom amount.</returns>
        public static float GetArmRelativeZ(this Skeleton skel, HandType hand, bool relativeToElbow = false) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint,
                handPoint = skel.Joints[hand == HandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

            if (relativeToElbow) {
                centerPoint = skel.Joints[hand == HandType.Left ? JointType.ElbowLeft : JointType.ElbowRight].Position;
            }
            else {
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position;
            }

            // Find the axis distance
            return centerPoint.Z - handPoint.Z;
        }

        public static double GetArmHorizontalAngle(this Skeleton skel, HandType hand) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == HandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

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

        public static double GetArmVerticalAngle(this Skeleton skel, HandType hand) {
            // Get the joints (shoulder and hand)
            SkeletonPoint
                centerPoint = skel.Joints[JointType.ShoulderCenter].Position,
                handPoint = skel.Joints[hand == HandType.Left ? JointType.HandLeft : JointType.HandRight].Position;

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
    }
}
