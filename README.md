# Kinect Pit Demos

These demos were developed for my [FIRST](http://www.usfirst.org/roboticsprograms/frc) robotics team (The W.A.R. Lords, [@team2485](https://github.com/team2485)) to allow other students and judges in the team pit area to learn about our robot in an interactive way.

This solution contains three projects:

- **[Rotate3D](Rotate3D/README.md)** (CAD Explorer) is an application which allows users to rotate/zoom 3D models using using hand gestures. This is intended primarily for use with [SolidWorks](http://www.solidworks.com/), but see the old `blender` branch for how the code can be adapted for other 3D applications which use mouse input.

- **KinectUtilities** contains the main Kinect processing code and event dispatching. There is also a wave detector for ensuring that people walking by the demo are actually trying to interact with the Kinect.

- **[KinectPhotoGallery](KinectPhotoGallery/README.md)** lets users pan through and enlarge a gallery of photos using the Kinect. This was not used during competition and is a little buggy.
