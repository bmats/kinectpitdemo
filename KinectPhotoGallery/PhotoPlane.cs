using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace KinectPhotoGallery {
    public class PhotoPlane {
        public const double FlatRegion = 2.0, SideRegion = 4.0, OutRegion = 3.0,
            MaxWidth = 4.0, MaxHeight = 3.0, PullY = -2.0;

        private Uri source;
        private double x, y, rotation;

        private GeometryModel3D model;
        private BitmapImage image;
        private AxisAngleRotation3D rotation3d;
        private TranslateTransform3D translation3d;

        public PhotoPlane(Uri source, int index) {
            this.source = source;
            Update(index * FlatRegion);
        }

        public Uri Source {
            get { return source; }
            set {
                source = value;
                if (image != null) image.UriSource = source;
            }
        }

        public double X {
            get { return x; }
            set {
                Update(value);
                if (translation3d != null) translation3d.OffsetX = TransformX(x);
            }
        }

        public double Y {
            get { return y; }
            set {
                y = value;
                if (translation3d != null) translation3d.OffsetY = y;
            }
        }

        public double Rotation {
            get { return rotation; }
            set {
                rotation = value;
                if (rotation3d != null) rotation3d.Angle = rotation;
            }
        }

        private void Update(double newX) {
            x = newX;
            Y = TransformY(x);

            if (x > -FlatRegion && x < FlatRegion) {
                Rotation = 90 - (-Math.Cos(x * Math.PI / FlatRegion) + 1) * 40 * (x > 0 ? 1 : -1);
            }
            else if (x >= FlatRegion) Rotation = 10;
            else Rotation = 170;
        }

        private double TransformX(double input) {
            if (input < -OutRegion || input > OutRegion) return 3 * 1.1 * input;
            return (-Math.Cos(input * Math.PI / OutRegion) + 2) * 1.1 * input;
        }

        private double TransformY(double input) {
            if (x > -OutRegion && x < OutRegion)
                return (Math.Cos(x * Math.PI / OutRegion) + 1) * -1.5 + 3;
            return 0;
        }

        /// <summary>
        /// Pulls the plane to the foreground.
        /// </summary>
        public void Pull() {
            if (translation3d == null) return;

            translation3d.BeginAnimation(TranslateTransform3D.OffsetYProperty, new DoubleAnimation {
                From = y,
                To   = PullY,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                EasingFunction = new QuadraticEase {
                    EasingMode = EasingMode.EaseOut
                }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        /// <summary>
        /// Pushes the plane back into the row.
        /// </summary>
        public void Push() {
            if (translation3d == null) return;

            translation3d.BeginAnimation(TranslateTransform3D.OffsetYProperty, new DoubleAnimation {
                From = PullY,
                To   = TransformY(x),
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                EasingFunction = new QuadraticEase {
                    EasingMode = EasingMode.EaseOut
                }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        /// <summary>
        /// Gets a GeometryModel3D for this PhotoPlane.
        /// </summary>
        public GeometryModel3D Model {
            get {
                if (model == null) {
                    // Create references to model components that we need to change later
                    image         = new BitmapImage(source);
                    rotation3d    = new AxisAngleRotation3D(new Vector3D(0, 0, 1), rotation);
                    translation3d = new TranslateTransform3D(TransformX(x), y, 0);

                    Transform3DGroup transform = new Transform3DGroup();
                    transform.Children.Add(new RotateTransform3D(rotation3d));
                    transform.Children.Add(translation3d);

                    // Calculate the appropriate size for the plane
                    double scale = Math.Min(MaxWidth / image.Width, MaxHeight / image.Height);
                    double halfWidth = scale * image.Width * 0.5, halfHeight = scale * image.Height * 0.5;
                    Point3DCollection vertices = new Point3DCollection {
                        new Point3D(0, -halfWidth, -halfHeight),
                        new Point3D(0,  halfWidth, -halfHeight),
                        new Point3D(0,  halfWidth,  halfHeight),
                        new Point3D(0, -halfWidth,  halfHeight),
                    };

                    // Assemble the geometry model
                    model = new GeometryModel3D {
                        Geometry  = new MeshGeometry3D {
                            Positions = vertices,
                            TriangleIndices    = Int32Collection.Parse("0 3 2 2 1 0"),
                            TextureCoordinates = PointCollection.Parse("1,1 0,1 0,0 1,0")
                        },
                        Material  = new DiffuseMaterial(new ImageBrush(image)),
                        Transform = transform
                    };
                }

                return model;
            }
        }

        public static int ConvertXToPlaneIndex(double x) {
            return (int)Math.Round(x / FlatRegion);
        }
    }
}
