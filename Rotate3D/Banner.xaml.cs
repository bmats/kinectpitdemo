using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Threading;

namespace Rotate3D {
    public partial class Banner : Window {
        private Storyboard activeStoryboard;
        private Timer timer;

        /// <summary>
        /// Creates a new Banner at the top of the screen.
        /// </summary>
        /// <param name="slideDown">Whether to animate the window sliding down from the top of the screen</param>
        public Banner(bool slideDown = false) {
            InitializeComponent();
            this.Left  = 0;
            this.Top   = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;

            this.timer = new Timer((object bleh) => {
                this.Dispatcher.Invoke(() => {
                    this.Clock.Content = DateTime.Now.ToString("hh:mm tt");
                });
            }, null, 0, 2000);

            if (slideDown) {
                this.Opacity = 0;
                this.Top = -this.Height;
                SlideDown();
            }
        }

        /// <summary>
        /// Gets or sets the text below the logo
        /// </summary>
        public String BannerText {
            get { return instructions.Text; }
            set { instructions.Text = value; }
        }

        /// <summary>
        /// Animates the banner sliding down from the top of the screen
        /// </summary>
        public void SlideDown() {
            if (this.activeStoryboard != null) this.activeStoryboard.Stop();
            this.Show();

            DoubleAnimation yAnim = new DoubleAnimation {
                From = this.Top,
                To   = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.8)),
                EasingFunction = new QuadraticEase {
                    EasingMode = EasingMode.EaseOut
                }
            };
            DoubleAnimation opacityAnim = new DoubleAnimation {
                From = this.Opacity,
                To   = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.8))
            };

            activeStoryboard = new Storyboard();
            activeStoryboard.Children.Add(yAnim);
            activeStoryboard.Children.Add(opacityAnim);
            Storyboard.SetTarget(yAnim, this);
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(yAnim, new PropertyPath("(Window.Top)"));
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("(Window.Opacity)"));
            activeStoryboard.Begin();
        }

        /// <summary>
        /// Animates the banner sliding up to to the top of the screen.
        /// </summary>
        /// <param name="close">Whether the close the window (or just hide it) when sliding up.</param>
        public void SlideUp(bool close = false) {
            if (this.activeStoryboard != null) this.activeStoryboard.Stop();

            DoubleAnimation yAnim = new DoubleAnimation {
                From = this.Top,
                To   = -this.Height,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                EasingFunction = new QuadraticEase {
                    EasingMode = EasingMode.EaseIn
                }
            };
            DoubleAnimation opacityAnim = new DoubleAnimation {
                From = this.Opacity,
                To   = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.5))
            };

            activeStoryboard = new Storyboard();
            activeStoryboard.Children.Add(yAnim);
            activeStoryboard.Children.Add(opacityAnim);
            Storyboard.SetTarget(yAnim, this);
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(yAnim, new PropertyPath("(Window.Top)"));
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("(Window.Opacity)"));
            activeStoryboard.Completed += (object sender, EventArgs e) => {
                if (close) this.Close();
                else this.Hide();
            };
            activeStoryboard.Begin();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e) {
            close.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e) {
            close.Visibility = Visibility.Hidden;
        }

        private void Close_Click(object sender, MouseButtonEventArgs e) {
            Application.Current.Shutdown();
        }
    }
}
