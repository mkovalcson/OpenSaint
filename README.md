Requires a Windows Computer with Visual Studio 2026 C# and .NET 10.

1. Copy the animatorConfig and ServoAnimator folders into the same parent folder
2. Open the project file in the ServoAnimator folder in VS 2026

It should compile and run and let you play with animating to audio files.

The animatorConfig folder includes the sketch file used to drive the LED ring lights compatible with the commands it sends.
The animatorConfig folder also includes the ticcmd.exe file to drive the eye pop. 

A test movie file with audio will open by default, so it can be played immediately. 

It does not require a physical robot to run the URDF virtual twin.

If you are testing a J5 head, the servo config allows configuring servo ports differently than I have them.
The software assumes it will find a 24 port maestro card and an Arduino Nano and two Tic Controllers. However it will run whatever it can find. 
If you just connect a Maestro card by USB, you should be able to test out your head with it.
If you only have one Tic controller driving both stepper motors, specify it as the left Tic and put the SN in the config screen and it will drive it as the left eye pop.

The included sketch file for the Arduino assumes separate defined pinouts for each ring light.
You can easily modify those pins by changing the defines at the very top of the sketch file.  
You will need to upload that sketch file to the Arduino before it will work. 
