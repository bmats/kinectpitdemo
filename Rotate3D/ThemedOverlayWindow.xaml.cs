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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rotate3D {
    /// <summary>
    /// Represents where to place an OverlayWindow when created.
    /// </summary>
    public enum OverlayPosition {
        /// <summary>
        /// In the center of the primary screen.
        /// </summary>
        Center,
        
        /// <summary>
        /// In the top right corner of the primary screen, 20px from each side.
        /// </summary>
        TopRight
    }

    /// <summary>
    /// A Kinect-themed window which can be overlayed onscreen with custom controls.
    /// </summary>
    public partial class ThemedOverlayWindow : Window {
        private bool doFadeIn;
        private Storyboard activeStoryboard;

        /// <summary>
        /// Creates a ThemedOverlayWindow with the specified contents and properties and fades it in.
        /// </summary>
        /// <param name="control">The content of the window.</param>
        /// <param name="size">The size of the window (centered onscreen).</param>
        /// <param name="position">The window position.</param>
        /// <param name="seconds">The time to display the window.</param>
        public ThemedOverlayWindow(UserControl control, Size size, OverlayPosition position, double seconds)
            : this(control, size, position, seconds, true) {
        }

        /// <summary>
        /// Creates a ThemedOverlayWindow with the specified contents and properties.
        /// </summary>
        /// <param name="control">The content of the window.</param>
        /// <param name="size">The size of the window (centered onscreen).</param>
        /// <param name="position">The window position.</param>
        /// <param name="seconds">The time to display the window.</param>
        /// <param name="fadeIn">Whether to fade the window in.</param>
        public ThemedOverlayWindow(UserControl control, Size size, OverlayPosition position, double seconds, bool fadeIn) {
            InitializeComponent();
            this.RootGrid.Children.Add(control);

            this.Width = size.Width;
            this.Height = size.Height;

            // Move window to appropriate position
            switch (position) {
                case OverlayPosition.Center:
                    this.Left = SystemParameters.PrimaryScreenWidth  * 0.5 - this.Width  * 0.5;
                    this.Top  = SystemParameters.PrimaryScreenHeight * 0.5 - this.Height * 0.5;
                    break;
                case OverlayPosition.TopRight:
                    this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
                    this.Top  = 20;
                    break;
            }

            // Set fading in if necessary
            this.doFadeIn = fadeIn;
            if (fadeIn) this.Show();

            // Create a timer to fade this window out and close it if a finite time is specified
            if (!double.IsInfinity(seconds))
                this.FadeOutAfter(seconds, true, false);
        }

        private void WindowLoaded(object sender, EventArgs e) {
            // Fade in _after_ the window is fully loadeed
            if (this.doFadeIn) FadeIn();
        }

        /// <summary>
        /// Fades in the window.
        /// </summary>
        public void FadeIn() {
            if (this.activeStoryboard != null) this.activeStoryboard.Stop();

            this.Show();

            // Create an animation to fade to opacity 1
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = 0.0;
            animation.To   = 1.0;
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5));

            // Attach the animation to the window opacity
            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(Window.Opacity)"));
            storyboard.Begin();

            this.activeStoryboard = storyboard;
        }

        /// <summary>
        /// Fades out the window, hiding it after the fade out is completed.
        /// </summary>
        public void FadeOut() {
            this.FadeOut(false, true);
        }

        /// <summary>
        /// Fades out the window, optionally hiding and/or closing it after the fade out is completed.
        /// </summary>
        /// <param name="close">Whether to Close() the window on completion.</param>
        /// <param name="hide">Whether to Hide() the window on completion.</param>
        public void FadeOut(bool close, bool hide) {
            if (this.activeStoryboard != null) this.activeStoryboard.Stop();

            // Create an animation to fade to opacity 0
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = 1.0;
            animation.To   = 0.0;
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5));

            // Attach the animation to the window opacity
            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(Window.Opacity)"));
            storyboard.Completed += delegate(object sender2, EventArgs e2) {
                // Trigger appropriate actions on completion
                if (close) this.Close();
                if (hide)  this.Hide();
            };
            storyboard.Begin();

            this.activeStoryboard = storyboard;
        }

        /// <summary>
        /// Fades out the window after the specified interval, optionally hiding and/or closing it after the fade out is completed.
        /// </summary>
        /// <param name="seconds">The time, in seconds, after which to fade out the window.</param>
        /// <param name="close">Whether to Close() the window on completion.</param>
        /// <param name="hide">Whether to Hide() the window on completion.</param>
        public void FadeOutAfter(double seconds, bool close, bool hide) {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan((long)(seconds * 1.0e7));
            timer.Tick += delegate(object sender, EventArgs e) {
                if (this.activeStoryboard != null) this.activeStoryboard.Stop();

                DoubleAnimation animation = new DoubleAnimation();
                animation.From = 1.0;
                animation.To = 0.0;
                animation.Duration = new Duration(TimeSpan.FromSeconds(0.5));

                Storyboard storyboard = new Storyboard();
                storyboard.Children.Add(animation);
                Storyboard.SetTarget(animation, this);
                Storyboard.SetTargetProperty(animation, new PropertyPath("(Window.Opacity)"));
                storyboard.Completed += delegate(object sender2, EventArgs e2) {
                    if (close)
                        this.Close();
                    if (hide)
                        this.Hide();
                };
                storyboard.Begin();
                this.activeStoryboard = storyboard;
            };
            timer.Start();
        }
    }
}
