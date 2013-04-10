using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SldWorks;
using SwConst;

namespace Rotate3D {
    /// <summary>
    /// Interfaces with SolidWorks using the COM API.
    /// </summary>
    class SolidWorks {
        private static ISldWorks app = new SldWorks.SldWorks();

        private ModelDoc2 modelDoc;
        private ModelDocExtension extension;
        private ModelView view;

        /// <summary>
        /// Creates a new SolidWorks connector to the currently active document in SolidWorks.
        /// </summary>
        /// <exception cref="Rotate3D.SolidWorksException">On an error communicating with SolidWorks.</exception>
        public SolidWorks() {
            this.modelDoc  = (ModelDoc2)app.ActiveDoc;

            if (this.modelDoc == null)
                throw new SolidWorksException("No open SolidWorks document found!");

            this.extension = this.modelDoc.Extension;
            this.view      = (ModelView)this.modelDoc.ActiveView;

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
