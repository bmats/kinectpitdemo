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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rotate3D {
    /// <summary>
    /// A themed text control, for use with the ThemedOverlayWindow.
    /// </summary>
    public partial class ThemedTextControl : UserControl {
        /// <summary>
        /// Creates the control containing the specified text.
        /// </summary>
        /// <param name="text">The text to display.</param>
        public ThemedTextControl(string text) {
            InitializeComponent();

            this.Label.Content = text;
        }
    }
}
