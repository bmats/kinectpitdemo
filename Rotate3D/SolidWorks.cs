using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using SldWorks;
using SwConst;
using SwMotionStudy;

namespace Rotate3D {
    /// <summary>
    /// Interfaces with SolidWorks using the COM API.
    /// </summary>
    class SolidWorks {
        private static ISldWorks app = new SldWorks.SldWorks();

        private const string SWAnimationName  = "KinectExplode";
        private const long   AnimLengthMillis = 8000, AnimDisplayLengthMillis = 1000;
        private const double AnimLengthSecs   = 8.0,  AnimDisplayLengthSecs   = 1.0;

        // SolidWorks interfaces
        private ModelDoc2          modelDoc;
        private AssemblyDoc        assemblyDoc;
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
            this.modelDoc    = (ModelDoc2)app.ActiveDoc;
            this.assemblyDoc = (AssemblyDoc)app.ActiveDoc;

            if (this.modelDoc == null)
                throw new SolidWorksException("No open SolidWorks document found!");

            this.extension   = this.modelDoc.Extension;
            this.view        = (ModelView)this.modelDoc.ActiveView;

            // Find the explode animation
            this.motionManager = (MotionStudyManager)this.extension.GetMotionStudyManager();
            this.explodeMotion = motionManager.GetMotionStudy(SWAnimationName);
            if (this.explodeMotion != null)
                this.explodeMotion.SetTime(0);

            // Set lower quality for quicker rendering
            this.view.DisplayMode = (int)swViewDisplayMode_e.swViewDisplayMode_Shaded;
        }

        /// <summary>
        /// Call before gripping, to pause SolidWorks dynamics.
        /// </summary>
        public void PreRender() {
            this.view.StopDynamics();
        }

        /// <summary>
        /// Call after gripping, to restart SolidWorks dynamics.
        /// </summary>
        public void PostRender() {
            this.view.StartDynamics();
        }

        /// <summary>
        /// Zoom the SolidWorks view to fit.
        /// </summary>
        public void ZoomToFit() {
            this.modelDoc.ViewZoomtofit2();
        }

        /// <summary>
        /// Rotate the camera around the center of the model by the specified angles.
        /// </summary>
        /// <param name="xAngle">The relative horizontal rotation, in radians.</param>
        /// <param name="yAngle">The relative vertical rotation, in radians.</param>
        public void AdjustViewAngle(double xAngle, double yAngle) {
            // Flip y for input
            yAngle *= -1;

            this.view.RotateAboutCenter(yAngle, xAngle);
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

            this.view.ZoomByFactor(zDist);
        }

        /// <summary>
        /// Steps the explode animation, if running.
        /// </summary>
        public void AnimateStep() {
            if (this.explodeMotion == null) return;

            // If mode has changed or in correct mode, but not finished animating
            if (this.targetAnim != currentAnim ||
                this.targetAnim == AnimationTarget.Explode  && this.currentAnimTime < AnimLengthSecs - 0.01 ||
                this.targetAnim == AnimationTarget.Collapse && this.currentAnimTime > 0.01) {
                
                if (animStartTime == 0) {
                    this.animStartTime   = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    this.tempTimeDiff    = 0;
                    this.tempAnimRelTime = 0;
                }
                else {
                    this.tempTimeDiff    = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - this.animStartTime;
                    this.tempAnimRelTime = ((double)this.tempTimeDiff / AnimDisplayLengthMillis) * AnimLengthSecs;
                }

                if (this.targetAnim == AnimationTarget.Explode) {
                    // Exploding
                    this.currentAnimTime = this.tempAnimRelTime;

                    if (this.currentAnimTime > AnimLengthSecs - 0.01) {
                        // Finished animation
                        this.currentAnimTime = AnimLengthSecs;
                        this.currentAnim     = this.targetAnim;
                        this.animStartTime   = 0;
                    }
                }
                else {
                    // Collapsing
                    this.currentAnimTime = AnimLengthSecs - this.tempAnimRelTime;

                    if (this.currentAnimTime < 0.01) {
                        // Finished animation
                        this.currentAnimTime = 0;
                        this.currentAnim     = this.targetAnim;
                        this.animStartTime   = 0;
                    }
                }

                // Go to the specified time (animate)
                this.explodeMotion.SetTime(this.currentAnimTime);
            }
        }

        /// <summary>
        /// Starts the explode animation.
        /// </summary>
        public void PlayExplode() {
            this.targetAnim = AnimationTarget.Explode;
        }

        /// <summary>
        /// Starts the collapse animation.
        /// </summary>
        public void PlayCollapse() {
            this.targetAnim = AnimationTarget.Collapse;
        }

        /// <summary>
        /// Gets whether or not an animation is currently running (and should be stepped).
        /// </summary>
        public bool Animating {
            get { return this.targetAnim != this.currentAnim; }
        }

        /// <summary>
        /// Gets whether an explode motion study (with the name "KinectExplode") was found in the active document.
        /// </summary>
        public bool ExplodeAnimationFound {
            get { return this.explodeMotion != null; }
        }
    }

    /// <summary>
    /// Thrown om errors communicating with SolidWorks.
    /// </summary>
    class SolidWorksException : Exception {
        /// <summary>
        /// Initializes a new instance of a SolidWorks exception.
        /// </summary>
        public SolidWorksException() : base() {
        }

        /// <summary>
        /// Initializes a new instance of a SolidWorks exception with the specified message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SolidWorksException(string message)
            : base(message) {
        }
    }
}
