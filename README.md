## Kinect Pit Demo ##

*Bryce Matsumori*

Using a Kinect in our pit to manipulate our robot's CAD assembly in SolidWorks.

This project was written in C# and uses the .NET Framework and Kinect SDK to detect users' skeletons and report their positions and gestures. Hand shape recognition was added in the Kinect SDK 1.7 and is used to detect users' gripping hands.

This program takes input gestures from the Kinect and converts them into SolidWorks commands. These commands are sent to SolidWorks using the SolidWorks API SDK.

For explode support, the SolidWorks model must have an explode animation with the name "KinectExplode" (or as configured in the `App.config` file included with the built executable).
Other settings such as motion study length and animation length can be configured in the config as well.

### Gestures ###

Two major gestures are currently supported.
To view the help screen, move your left arm straight out at a 45° angle from your body and leave your right arm at your side.

**Rotating**

"Grip" the model with one fist (likely with your other arm at your side) and move your fist around on all axes.
The model will rotate and zoom.

**Exploding**

Grip with both hands and pull your hands outward or push them inward. Use the circles onscreen as guides.
The model will play the explode animation or collapse animation based on your hand movement.

### Prerequisites ###
 - Microsoft .NET Framework 4.5 ([download][dotnet]) ([direct link][dotnet-direct])
 - Microsoft Kinect SDK 1.7 ([download][kinect]) ([direct link][kinect-direct])
 - SolidWorks API SDK 2012 (found on SolidWorks installation disk under `/apisdk/`)
 - *(for development)* Microsoft Visual Studio (Express) for Desktop 2012 ([download][vs2012])

[dotnet]: http://www.microsoft.com/en-us/download/details.aspx?id=30653
[dotnet-direct]: http://download.microsoft.com/download/B/A/4/BA4A7E71-2906-4B2D-A0E1-80CF16844F5F/dotNetFx45_Full_setup.exe
[kinect]: http://www.microsoft.com/en-us/kinectforwindows/develop/developer-downloads.aspx
[kinect-direct]: http://go.microsoft.com/fwlink/?LinkId=275588
[vs2012]: http://www.microsoft.com/visualstudio/eng/downloads

#### Required libraries at runtime ####
These must be in the same folder as `KinectRotate3D.exe`.

 - `KinectInteraction170_32.dll`
 - `KinectInteraction170_64.dll`
 - `Microsoft.Kinect.Toolkit.Interaction.dll`
