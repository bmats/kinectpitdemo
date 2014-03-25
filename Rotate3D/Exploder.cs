using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Kinect;

namespace Rotate3D {
    /// <summary>
    /// Assists in calculating and running explode animations.
    /// </summary>
    class Exploder {
        private SolidWorks sw;

        // Loaded from App.config
        private static float ExplodeDiffThreshold = 0.3f, CollapseDiffThreshold = -0.3f;
        static Exploder() {
            float temp;
            if (float.TryParse(ConfigurationManager.AppSettings["ExplodeThreshold"],  out temp)) ExplodeDiffThreshold  = temp;
            if (float.TryParse(ConfigurationManager.AppSettings["CollapseThreshold"], out temp)) CollapseDiffThreshold = temp;
        }

        private float initGripDist = 0, distance = 0;

        private bool firstProcessing      = true;
        private bool isGripping           = false;
        private bool previousBothGripping = false;

        // Position caching for jitter reduction
        private SkeletonPoint prevLeftPoint, prevRightPoint;
        private Skeleton[] skeletonHistory = new Skeleton[15];
        private int skeletonHistoryIdx = 0;

        /// <summary>
        /// Creates a new Exploder linked to the provided SolidWorks interface.
        /// </summary>
        /// <param name="sw">A SolidWorks interface to explode.</param>
        public Exploder(SolidWorks sw) {
            this.sw = sw;
        }

        /// <summary>
        /// Computes explode or collapse animations based on the provided skeleton and gripping state.
        /// </summary>
        /// <param name="skeleton">The active skeleton to process.</param>
        /// <param name="bothGripping">Whether both hands are gripping, triggering explode gesture recognition.</param>
        public void ProcessSkeleton(Skeleton skeleton, bool bothGripping) {
            if (firstProcessing || bothGripping && !previousBothGripping) {
                // Fill the history buffer if this is the first processing for this grip
                for (int i = 0; i < skeletonHistory.Length; i++) skeletonHistory[i] = skeleton;
                firstProcessing = false;
            }
            else {
                // Save current skeleton into buffer and advance
                skeletonHistory[skeletonHistoryIdx] = skeleton;
                skeletonHistoryIdx = (skeletonHistoryIdx + 1) % 15;
            }

            // Calculate the average point for each hand
            SkeletonPoint leftPoint = new SkeletonPoint() {
                X = skeletonHistory.Sum(p => p.Joints[JointType.HandLeft].Position.X) / 15,
                Y = skeletonHistory.Sum(p => p.Joints[JointType.HandLeft].Position.Y) / 15,
                Z = skeletonHistory.Sum(p => p.Joints[JointType.HandLeft].Position.Z) / 15
            }, rightPoint = new SkeletonPoint() {
                X = skeletonHistory.Sum(p => p.Joints[JointType.HandRight].Position.X) / 15,
                Y = skeletonHistory.Sum(p => p.Joints[JointType.HandRight].Position.Y) / 15,
                Z = skeletonHistory.Sum(p => p.Joints[JointType.HandRight].Position.Z) / 15
            };

            // Don't use Math.Sqrt() unless necessary because it's slow.
            // Variable names with a "2" at the end are squared values (no Math.Sqrt() used).
            float handDist2 =
                (rightPoint.X - leftPoint.X) * (rightPoint.X - leftPoint.X) +
                (rightPoint.Y - leftPoint.Y) * (rightPoint.Y - leftPoint.Y) +
                (rightPoint.Z - leftPoint.Z) * (rightPoint.Z - leftPoint.Z);

            // First gripping
            if (bothGripping && !previousBothGripping) {
                initGripDist = (float)Math.Sqrt(handDist2);
                isGripping = true;
            }
            else if (bothGripping) {
                float
                    prevHandDist2 =
                        (prevRightPoint.X - prevLeftPoint.X) * (prevRightPoint.X - prevLeftPoint.X) +
                        (prevRightPoint.Y - prevLeftPoint.Y) * (prevRightPoint.Y - prevLeftPoint.Y) +
                        (prevRightPoint.Z - prevLeftPoint.Z) * (prevRightPoint.Z - prevLeftPoint.Z),
                    handDistDiff = (float)Math.Sqrt(handDist2) - initGripDist; // distance difference since initial grip

                // Play an animation if passed the thresholds (it's OK if Play() methods are called multiple times)
                if (handDistDiff > ExplodeDiffThreshold) {
                    sw.PlayExplode();
                }
                else if (handDistDiff < CollapseDiffThreshold) {
                    sw.PlayCollapse();
                }
            }
            // Grip released
            else isGripping = false;

            // Save values for next iteration
            prevLeftPoint        = leftPoint;
            prevRightPoint       = rightPoint;
            previousBothGripping = bothGripping;
            distance             = handDist2;
        }

        /// <summary>
        /// Gets whether the exploder is active (if the user is gripping both hands).
        /// </summary>
        public bool Active {
            get { return isGripping; }
        }

        /// <summary>
        /// Gets the last initial distance.
        /// </summary>
        public float InitialGripDistance {
            get { return initGripDist; }
        }

        /// <summary>
        /// Gets the last hand distance measured.
        /// </summary>
        public float HandDistance {
            get { return distance; }
        }
    }
}
