using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Rotate3D {
    /// <summary>
    /// Interaction logic for Hand.xaml
    /// </summary>
    public partial class Hand : Window {
        public enum Side {
            Left, Right
        }

        private Side currDirection = Side.Right;
        private const double RotationAngle = 30;

        public Hand(bool fade = false) {
            InitializeComponent();

            this.Left = SystemParameters.PrimaryScreenWidth / 2 - this.Width / 2;
            this.Top = 200;

            if (fade) {
                this.Opacity = 0;
                FadeIn();
            }
        }

        public Side Direction {
            get { return currDirection; }
            set {
                if (value != currDirection) {
                    RotateTo(value);
                    currDirection = value;
                }
            }
        }

        private void RotateTo(Side side) {
            double angle = side == Side.Left ? -RotationAngle : RotationAngle;

            if (Visibility == System.Windows.Visibility.Visible) {
                handRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation {
                    From = handRotate.Angle,
                    To = angle,
                    Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                    EasingFunction = new QuadraticEase {
                        EasingMode = EasingMode.EaseInOut
                    }
                }, HandoffBehavior.SnapshotAndReplace);
            }
            else {
                handRotate.Angle = angle;
            }
            
        }

        public void FadeIn() {
            this.Show();

            this.BeginAnimation(Window.OpacityProperty, new DoubleAnimation {
                From = this.Opacity,
                To   = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            }, HandoffBehavior.SnapshotAndReplace);
        }

        public void FadeOut() {
            DoubleAnimation anim = new DoubleAnimation {
                From = this.Opacity,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            };
            anim.Completed += (object sender, EventArgs e) => {
                this.Hide();
            };

            this.BeginAnimation(Window.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
