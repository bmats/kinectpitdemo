using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SldWorks;
using SwConst;

namespace Rotate3D {
    class SolidWorks {
        private static ISldWorks app = new SldWorks.SldWorks();

        private ModelDoc2 modelDoc;
        private ModelDocExtension extension;
        private ModelView view;

        public SolidWorks() {
            try {
                this.modelDoc  = (ModelDoc2)app.ActiveDoc;
                this.extension = this.modelDoc.Extension;
                this.view      = (ModelView)this.modelDoc.ActiveView;

                //app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swPerformanceRemoveDetailDuringZoomPanRotate, true);
                //app.SetUserPreferenceIntegerValue((int)swUserPreferenceToggle_e.swPerformanceRemoveDetailDuringZoomPanRotate, true);
                //this.view.DisplayMode = (int)swViewDisplayMode_e.swViewDisplayMode_Faceted;
                //app.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swEdgesTangentEdgeDisplay, (int)swEdgesTangentEdgeDisplay_e.swEdgesTangentEdgeDisplayRemoved);
            }
            catch (Exception e) {
                MessageBox.Show("Error connecting to SolidWorks:\r\n" + e, "Kinect Demo", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }

        public void PreRender() {
            this.view.StopDynamics();
            //this.modelDoc.SetTessellationQuality(0);
            //this.modelDoc.GraphicsRedraw2();
        }

        public void PostRender() {
            this.view.StartDynamics();
        }

        public void ZoomToFit() {
            this.modelDoc.ViewZoomtofit2();
        }

        public void AdjustViewAngle(double xAngle, double yAngle) {
            // flip y for input
            yAngle *= -1;

            this.view.RotateAboutCenter(yAngle, xAngle);
        }

        public void AdjustZoom(double zDist) {
            // invert z for input
            zDist *= -1;

            // convert to a factor
            zDist += 1;

            this.view.ZoomByFactor(zDist);
        }
    }
}
