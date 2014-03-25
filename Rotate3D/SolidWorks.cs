using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swmotionstudy;

namespace Rotate3D {
    /// <summary>
    /// Interfaces with SolidWorks using the COM API.
    /// </summary>
    class SolidWorks {
        private static SldWorks app = new SldWorks();

        // Loaded and generated from App.config
        private static string SWAnimationName;
        private static double AnimLengthSecs,   AnimDisplayLengthSecs;
        private static long   AnimLengthMillis, AnimDisplayLengthMillis;
        static SolidWorks() {
            SWAnimationName = System.Configuration.ConfigurationManager.AppSettings["SWMotionStudyName"];
            if (string.IsNullOrWhiteSpace(SWAnimationName)) SWAnimationName = "KinectExplode";

            double temp;
            AnimLengthSecs        = double.TryParse(System.Configuration.ConfigurationManager.AppSettings["SWMotionStudyLength"],           out temp) ? temp : 8.0;
            AnimDisplayLengthSecs = double.TryParse(System.Configuration.ConfigurationManager.AppSettings["ExplodeAnimationDisplayLength"], out temp) ? temp : 1.0;

            AnimLengthMillis        = (long)(AnimLengthSecs        * 1000);
            AnimDisplayLengthMillis = (long)(AnimDisplayLengthSecs * 1000);
        }

        // SolidWorks interfaces
        private IModelDoc2         modelDoc;
        private ModelDocExtension  extension;
        private ModelView          view;
        private MotionStudyManager motionManager;
        private MotionStudy        explodeMotion;

        private enum AnimationTarget {
            Explode, Collapse
        }

        // Explode animation states
        private AnimationTarget  targetAnim = AnimationTarget.Collapse;
        private AnimationTarget currentAnim = AnimationTarget.Collapse;
        private double currentAnimTime = 0;
        private long animStartTime = 0, tempTimeDiff;
        private double tempAnimRelTime;

        #region Move to Foreground

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Moves the first instance of SLDWORKS.exe to the foreground.
        /// </summary>
        /// <returns>A boolean indicating success.</returns>
        public static bool MoveToForeground() {
            Process[] instances = Process.GetProcessesByName("SLDWORKS");
            foreach (Process p in instances) {
                if (p.MainWindowHandle != IntPtr.Zero) {
                    return SetForegroundWindow(p.MainWindowHandle);
                }
            }
            return false;
        }

        #endregion

        /// <summary>
        /// Creates a new SolidWorks connector to the currently active document in SolidWorks.
        /// </summary>
        /// <exception cref="Rotate3D.SolidWorksException">On an error communicating with SolidWorks.</exception>
        public SolidWorks() {
            modelDoc = (ModelDoc2)app.ActiveDoc;
            if (modelDoc == null) throw new SolidWorksException("No open SolidWorks document found!");

            extension = modelDoc.Extension;
            view      = (ModelView)modelDoc.ActiveView;

            // Find the explode animation
            motionManager = (MotionStudyManager)extension.GetMotionStudyManager();
            explodeMotion = motionManager.GetMotionStudy(SWAnimationName);
            if (explodeMotion != null) explodeMotion.SetTime(0);

            // Set lower quality for quicker rendering
            view.DisplayMode = (int)swViewDisplayMode_e.swViewDisplayMode_Shaded;
        }

        ~SolidWorks() {
            // Reset view to high quality on exit
            if (view != null) view.DisplayMode = (int)swViewDisplayMode_e.swViewDisplayMode_ShadedWithEdges;
        }

        /// <summary>
        /// Call before gripping to start faster rotation of model view.
        /// </summary>
        public void PreRender() {
            view.StartDynamics();
        }

        /// <summary>
        /// Call after gripping to start faster rotation of model view.
        /// </summary>
        public void PostRender() {
            view.StopDynamics();
        }

        /// <summary>
        /// Zoom the SolidWorks view to fit.
        /// </summary>
        public void ZoomToFit() {
            modelDoc.ViewZoomtofit2();
        }

        /// <summary>
        /// Rotate the camera around the center of the model by the specified angles.
        /// </summary>
        /// <param name="xAngle">The relative horizontal rotation, in radians.</param>
        /// <param name="yAngle">The relative vertical rotation, in radians.</param>
        public void AdjustViewAngle(double xAngle, double yAngle) {
            // Flip y for input
            yAngle *= -1;

            view.RotateAboutCenter(yAngle, xAngle);
        }

        /// <summary>
        /// Zoom the camera by the specified amount.
        /// </summary>
        /// <param name="zDist">The zoom factor (1.0 is no zooming).</param>
        public void AdjustZoom(double zDist) {
            // Invert z for input
            zDist *= -1;

            // Convert to a factor relative to 1.0
            zDist += 1;

            view.ZoomByFactor(zDist);
        }

        /// <summary>
        /// Steps the explode animation, if running.
        /// </summary>
        public void AnimateStep() {
            if (explodeMotion == null) return;

            // If mode has changed or in correct mode, but not finished animating
            if (targetAnim != currentAnim ||
               (targetAnim == AnimationTarget.Explode  && currentAnimTime < AnimLengthSecs - 0.01) ||
               (targetAnim == AnimationTarget.Collapse && currentAnimTime > 0.01)) {
                
                if (animStartTime == 0) {
                    animStartTime   = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    tempTimeDiff    = 0;
                    tempAnimRelTime = 0;
                }
                else {
                    tempTimeDiff    = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - animStartTime;
                    tempAnimRelTime = ((double)tempTimeDiff / AnimDisplayLengthMillis) * AnimLengthSecs;
                }

                if (targetAnim == AnimationTarget.Explode) {
                    // Exploding
                    currentAnimTime = tempAnimRelTime;

                    if (currentAnimTime > AnimLengthSecs - 0.01) {
                        // Finished animation
                        currentAnimTime = AnimLengthSecs;
                        currentAnim     = targetAnim;
                        animStartTime   = 0;
                    }
                }
                else {
                    // Collapsing
                    currentAnimTime = AnimLengthSecs - tempAnimRelTime;

                    if (currentAnimTime < 0.01) {
                        // Finished animation
                        currentAnimTime = 0;
                        currentAnim     = targetAnim;
                        animStartTime   = 0;
                    }
                }

                // Go to the specified time (animate)
                explodeMotion.SetTime(currentAnimTime);
            }
        }

        /// <summary>
        /// Starts the explode animation.
        /// </summary>
        public void PlayExplode() {
            // Finish animation before changing
            if (Animating) return;

            targetAnim = AnimationTarget.Explode;
        }

        /// <summary>
        /// Starts the collapse animation.
        /// </summary>
        public void PlayCollapse() {
            // Finish animation before changing
            if (this.Animating) return;

            targetAnim = AnimationTarget.Collapse;
        }

        /// <summary>
        /// Gets whether or not an animation is currently running (and should be stepped).
        /// </summary>
        public bool Animating {
            get {
                return targetAnim != currentAnim ||
                      (targetAnim == AnimationTarget.Explode  && currentAnimTime < AnimLengthSecs - 0.01) ||
                      (targetAnim == AnimationTarget.Collapse && currentAnimTime > 0.01);
            }
        }

        /// <summary>
        /// Gets whether an explode motion study (with the name "KinectExplode") was found in the active document.
        /// </summary>
        public bool ExplodeAnimationFound {
            get { return explodeMotion != null; }
        }
    }
}
