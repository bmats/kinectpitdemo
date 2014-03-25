using System;
using System.Configuration;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Warlords.Kinect;
using System.Diagnostics;

namespace KinectPhotoGallery {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private const double DistanceConversion = 3.9, PressXChangeLimit = 0.5, SwipeAnimTime = 300.0;

        private Kinect kinect;
        private KinectGalleryGestureDetector detector;
        private WaveDetector waver;
        private Timer timer;

        private Stopwatch swipeAnimStopwatch;

        private PhotoPlane[] planes;
        private double center = 0;

        private bool pressing = false;
        private double pressX;

        public MainWindow() {
            InitializeComponent();
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            String[] uris = ConfigurationManager.AppSettings["images"].Split(new char[] { ';' });
            planes = new PhotoPlane[uris.Length];

            for (int i = 0; i < uris.Length; i++) {
                planes[i] = new PhotoPlane(new Uri(uris[i]), i);
                this.World.Children.Add(planes[i].Model);
            }

            globalTranslate.OffsetX = 0;

            timer = new Timer(30);
            timer.Elapsed += (object sender, ElapsedEventArgs e) => {
                try {
                    Dispatcher.Invoke(() => {
                        if (ld && !rd) {
                            Move(-0.1);
                        }
                        else if (rd && !ld) {
                            Move(0.1);
                        }
                    });
                }
                catch (TaskCanceledException) { }
            };
            timer.Start();

            swipeAnimStopwatch = new Stopwatch();

            try {
                kinect = new Kinect(s => s.Position.Z < 2 && Math.Abs(s.Position.X) < 0.35) {
                    GripEnabled  = false,
                    PressEnabled = true
                };

                waver = new WaveDetector(kinect);
                waver.WaveStarted += (object sender, WaveEventArgs e) => {
                    Dispatcher.Invoke(() => {
                        label.Content = "Started";
                    });
                };
                waver.StateChanged += (object sender, WaveEventArgs e) => {
                    Dispatcher.Invoke(() => {
                        label.Content = String.Format("State change {0}", e.State);
                    });
                };
                waver.WaveCompleted += (object sender, WaveEventArgs e) => {
                    Dispatcher.Invoke(() => {
                        label.Content = "Finished waving";
                    });
                };

                detector = new KinectGalleryGestureDetector(kinect, waver);
                detector.Swiped  += detector_Swiped;
                detector.Moved   += detector_Moved;
                detector.Pressed += detector_Pressed;

                kinect.Start();
            }
            catch (KinectException e) {
                MessageBox.Show(e.Message);
                Application.Current.Shutdown();
            }
        }

        private void detector_Swiped(object sender, SwipedEventArgs e) {
            Dispatcher.Invoke(() => {
                //label.Content = String.Format("Swipe {0} {1}", e.Hand, e.Velocity);

                double dist = PhotoPlane.FlatRegion * (e.Velocity > 0 ? 1 : -1);

                if (center + dist < 0) return;
                center += dist;

                if (pressing && Math.Abs(pressX - center) > PressXChangeLimit) {
                    pressing = false;
                    planes[PhotoPlane.ConvertXToPlaneIndex(pressX)].Push();
                }

                // Save state
                prevSwipeVal = center;
                swipeOffset  = dist;

                swipeAnimStopwatch.Start();
                timer.Elapsed += AnimateSwipe;
            });
        }

        private double prevSwipeVal, swipeOffset;

        private void AnimateSwipe(object sender, ElapsedEventArgs e) {
            double targetVal = easeInOutQuad(swipeAnimStopwatch.ElapsedMilliseconds, center, swipeOffset, SwipeAnimTime);
            Dispatcher.Invoke(() => {
                label.Content = String.Format("Animating {0} {1}", swipeAnimStopwatch.ElapsedMilliseconds, targetVal);
                Move(targetVal - prevSwipeVal);
            });
            prevSwipeVal = targetVal;

            //Console.WriteLine("{0} > {1}? {2}", swipeAnimStopwatch.ElapsedMilliseconds, targetVal, swipeAnimStopwatch.ElapsedMilliseconds > SwipeAnimTime);
            if (swipeAnimStopwatch.ElapsedMilliseconds > SwipeAnimTime) {
                //Console.WriteLine("Stopping");
                timer.Elapsed -= AnimateSwipe;
                swipeAnimStopwatch.Stop();
            }
        }

        private void detector_Moved(object sender, MovedEventArgs e) {
            Dispatcher.Invoke(() => {
                label.Content = String.Format("{0} {1}", e.Hand, e.XOffset);
                Move(-e.Distance * DistanceConversion);
            });
        }

        private void detector_Pressed(object sender, EventArgs e) {
            pressing = !pressing;
            pressX = center;

            Dispatcher.Invoke(() => {
                label.Content = "Pushed";

                if (pressing) planes[PhotoPlane.ConvertXToPlaneIndex(center)].Pull();
                else          planes[PhotoPlane.ConvertXToPlaneIndex(center)].Push();
            });
        }

        private void Move(double x) {
            if (center + x < 0) return;
            center += x;

            if (pressing && Math.Abs(pressX - center) > PressXChangeLimit) {
                pressing = false;
                planes[PhotoPlane.ConvertXToPlaneIndex(pressX)].Push();
            }

            foreach (PhotoPlane p in planes) {
                p.X -= x;
            }
        }

        private static double easeInOutQuad(double currTime, double startValue, double changeValue, double duration) {
            currTime /= duration / 2;
            if (currTime < 1) return changeValue / 2 * currTime * currTime + startValue;
            currTime--;
            return -changeValue / 2 * (currTime * (currTime - 2) - 1) + startValue;
        }

        #region Window Events

        private bool ld, rd;

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Left:
                    ld = true;
                    break;
                case Key.Right:
                    rd = true;
                    break;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Left:
                    ld = false;
                    break;
                case Key.Right:
                    rd = false;
                    break;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            pressing = !pressing;
            pressX = center;

            if (pressing) planes[PhotoPlane.ConvertXToPlaneIndex(center)].Pull();
            else          planes[PhotoPlane.ConvertXToPlaneIndex(center)].Push();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (kinect != null) kinect.Stop();
        }

        #endregion
    }
}
