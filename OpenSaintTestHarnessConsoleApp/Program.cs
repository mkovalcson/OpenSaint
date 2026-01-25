using HidSharp;
using HidSharp.Reports.Units;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenMacroBoard.SDK;
using OpenSaintLib.Utilities;
using OpenSaintTestHarnessConsoleApp;
using SharpDX.XInput;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Processing;
using StreamDeckSharp;
using System;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Media;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TicT249Control;
using static System.Collections.Specialized.BitVector32;
using static System.Reflection.Metadata.BlobBuilder;
using static System.Runtime.InteropServices.JavaScript.JSType;
//using SteamDeckControllerListener;

class Program
{    

    static void Main(string[] args)
    {
        try
        {   
            string leftTicSerialNumber = "00475552";  // TODO: needs  to be configurable
            string exeFolder = AppContext.BaseDirectory;

            // Dynamically finds the USB ports for Maestro, Arduino and Tic T-249's 
            // Needs serial number to assign Left and Right Tic.
            // Sets Maestro and Tic initial configuration from text files.
            var outputDevices = USBDevices.WindowsFindUSBDevices(leftTicSerialNumber, exeFolder);

            // Initialize Tic's
            if (outputDevices.LeftTic != null)
            {
                // Creep backwards to zero
                //outputDevices.LeftTic.MoveToPosition(-100);
                //outputDevices.LeftTic.SetCurrentPositionZero();

                outputDevices.LeftTic.MoveToPosition(0);
            }
            if (outputDevices.RightTic != null)
            {
                // Creep backwards to zero
                //outputDevices.RightTic.MoveToPosition(-100);
                //outputDevices.RightTic.SetCurrentPositionZero();

                outputDevices.RightTic.MoveToPosition(0);
            }

            outputDevices.lights.Command("ClearAll");

            // Sets all 24 servo channels, Min, Max, Home, speed accel ranges etc..
            // RobotControl enum names all servos
            Servo[] servos = ServoConfig.ConfigureServos(outputDevices.headPort);  // 24 servos for desktop J5          

            // Maps all X-Box controls to drive J5
            // Uses Shoulder button multiplexing for 4 mappings for each control
            IOMapping mappings = ControllerMapping.SetXBoxMapping();           

            // load everything configured into the settings.
            var settings = new SettingsObject(mappings, servos, outputDevices.LeftTic, outputDevices.RightTic, outputDevices.lights);

            var newMovie = CL.CreateUnitTests();
            settings.SelectedMovie= newMovie;

            //var movieName = "HappyBDayKatya";          
            //var newMovie = new Movie(movieName, exeFolder + "MOVIES\\" + movieName, new List<Scene> { Sequences.HappyBDay("Scene1") }, TriggerSource.Automatic);

            //var movieName = "DalekEncounter";           
            //var newMovie = new Movie(movieName, exeFolder + "MOVIES\\" + movieName, new List<Scene> { Sequences.BuildScene("Scene1") }, TriggerSource.Pedals);
            //newMovie.BackgroundMotion = new Sequence(Sequences.BackgroundLoop1(), 0);
            //newMovie.msInactivityTimeout = 5000; // 5 seconds of inactivity before background motion kicks in.

            //settings.SoundBites = soundbites;
            //settings.SoundBiteIndex = 0;
            // Saves all Settings
            //OpenSaintSettings.Save(settings);
            // Reads settings back to run the control loop.
            // var retrievedsettings = OpenSaintSettings.Load();

            RunControlLoop(settings);  // Stays in here until it's over.              

        }
        catch (Exception ex)
        {
            var msg = ex.Message;
        }
    }

    static void RunControlLoop(SettingsObject settings)
    {      
       // TODO: Check SharpDX for Linux Compatibility
       SharpDX.XInput.Controller controller = new Controller(UserIndex.One);
    
        //Robot Model
        var stickDeadBand = 1800;
        var isVerbose = true;

        var xBoxController = new XBoxController(stickDeadBand, isVerbose);
       // var robotModel = new Robot();

        if (!controller.IsConnected)
        {
            Console.WriteLine("Xbox controller not connected.");
            return;
        }

        TimeSpan LongestTime = new TimeSpan();
        TimeSpan LongestServoTime = new TimeSpan();
        TimeSpan cumulativeTime = TimeSpan.Zero;
        double counter = 0;
        bool inMovieMode = false;
        try
        {
            var verbose =true;
            int frameTime = 20;

            bool playingMovie = false;
            int msKeyboardDebounce = 1; // seconds
            DateTime NextAllowedPress = DateTime.Now;

            while (true)
            {
                ConsoleKey? consoleKey = null;
                // Checks for waiting Keyboard input so it doesn't block waiting for a key press 
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    Console.WriteLine($"Keyboard: {key}");                                       

                    if (key == ConsoleKey.UpArrow)
                    {
                        if (!playingMovie)
                        {
                            Console.WriteLine("****Movie Mode On****");
                            playingMovie = true;
                            consoleKey = null;
                        }
                        else if (DateTime.Now > NextAllowedPress)
                        {
                            consoleKey = key;
                            NextAllowedPress = DateTime.Now.AddSeconds(msKeyboardDebounce);
                        }
                    }  
                    else if ( playingMovie && (key == ConsoleKey.LeftArrow || key == ConsoleKey.RightArrow) && DateTime.Now > NextAllowedPress)
                    {
                        consoleKey = key;
                        NextAllowedPress = DateTime.Now.AddSeconds(msKeyboardDebounce);
                    }
                    
                    if (key == ConsoleKey.Escape)                           
                        break;

                    // See if we are going into Movie mode
                   
                }  
                
                var start = DateTime.Now;
                var state = controller.GetState();
                var gamepad = state.Gamepad;

                // check all XBox controls
                xBoxController.ProcessInputs(gamepad);
               
                if(playingMovie && xBoxController.StartButton.Value == 1)
                {
                    Console.WriteLine("-- X-Box Controller On --");
                    playingMovie = false;
                }
                // Map them to Model

                if (!playingMovie)
                {
                    var servoWrite = ProcessMapping(xBoxController, settings, verbose, start);
                }
                else
                {
                    try
                    {
                        var running = RunMovies.RunMovie(settings, verbose, consoleKey);
                        if (!running) playingMovie = false;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace); 
                    }
                }

                var timeTaken = DateTime.Now;
                TimeSpan diff = timeTaken - start;

                //if (servoWrite)
                //{
                //    if (diff > LongestServoTime)
                //    {
                //        LongestServoTime = diff;
                //    }
                //    cumulativeTime += diff;
                //    counter++;
                //    var average = cumulativeTime.Divide(counter);
                //}

                //if (diff > LongestTime)
                //{
                //    LongestTime = diff;
                //}

                //Console.WriteLine(diff.Milliseconds);

                if ((diff.Milliseconds < frameTime))
                {
                    Thread.Sleep(frameTime - diff.Milliseconds);
                }

                //var end = DateTime.Now;
                //TimeSpan adjusted = end - start;

            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xbox"></param>
    /// <param name="settings"></param>
    /// <param name="verbose"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    static bool ProcessMapping(XBoxController xbox, SettingsObject settings, bool verbose, DateTime now)
    {
        var multiplexValue = xbox.LMUX.Value * 10 + xbox.RMUX.Value;

        List<IOMap> iO_Mapping;  

        switch (multiplexValue)
        {
            case 0: // Default
                iO_Mapping = settings.IO_Mapping.Default;
                break;
            case 1: // Right
                iO_Mapping = settings.IO_Mapping.R_Shoulder;
                break;
            case 10: // Left
                iO_Mapping = settings.IO_Mapping.L_Shoulder;
                break;
            case 11: // Both
                iO_Mapping = settings.IO_Mapping.LR_Shoulder;
                break;
            default: // 00
                iO_Mapping = settings.IO_Mapping.Default;
                break;
        }       

        var loopNow = DateTime.Now;
        var servoWrite = false;    
        var axisServos = new List<Servo>();
        var axisTargetus = new List<int>();
        //var servoDisable = false;
        //var disableChannels = new List<int>();
      
        var USBPort = settings.Servos[0].USBPort;

        if(settings.EyesPopped == true && settings.EyesUnpopped != DateTime.MinValue)
        {
            if (settings.EyesUnpopped < loopNow)
            {
                settings.EyesPopped = false;
                settings.EyesUnpopped = DateTime.MinValue;
                settings.Servos[(int)RobotControls.NoseBody].GoHome();               
                settings.Servos[(int)RobotControls.BrowLeftBottomOpen].GoHome();
                settings.Servos[(int)RobotControls.BrowRightBottomOpen].GoHome();             
            }
        }

        if (settings.IO_Delay.Count > 0)
        {
            var mapFired = false;
            foreach (IOMap map in settings.IO_Delay)
            {
                if (map.FireTime != DateTime.MinValue && map.FireTime < loopNow)
                {
                    mapFired = true;
                    map.FireTime = DateTime.MinValue;
                    ParseIOMap(map, settings, verbose, now);
                }
            }

            // if map fired remove it from the list
            if (mapFired == true)
            {
                var remainingToFire = new List<IOMap>();
                foreach (IOMap map in settings.IO_Delay)
                {
                    if(map.FireTime != DateTime.MinValue)
                        remainingToFire.Add(map);
                }
                settings.IO_Delay = remainingToFire;
            }
        }
        // Process Axis List, only those that changed that have changed        
        foreach (Control c in xbox.CAxis)  
        {
            // Look just for mappings for this MUX option.
            foreach (IOMap io in iO_Mapping)
            {                
                if (c.Name == io.Input.ToString())
                {
                    var controlValue = c.Value;

                    if (verbose) Console.WriteLine(" Input: " + c.Name + ": " + c.Value);

                    foreach (Output output in io.OutputList)
                    {
                        if(output.robotControl != null)
                            {
                            var servo = settings.Servos[(int)output.robotControl];

                            if (settings.EyesPopped && (servo.Name == RobotControls.BrowLeftBottomOpen || servo.Name == RobotControls.BrowRightBottomOpen || servo.Name == RobotControls.NoseBody))
                            {
                                continue;
                            }

                            var servoValue = Servo.MapAxis(c, servo, (output.MultiOutput == MultiOutput.Reversed),false);

                            if (servo.CurrentPosition == servoValue)
                            {
                                //Not moving anymore, disable servo
                                // output.Servo.DisableServo();

                                //disableChannels.Add(output.Servo.Channel);
                                //servoDisable = true;
                            }
                            else
                            {
                                axisServos.Add(servo);
                                axisTargetus.Add(servoValue);
                                servoWrite = true;
                            }

                            servo.CurrentPosition = servoValue;

                            //servo.WritePwmMicroseconds(servo.USBPort, servo.Channel, servoValue);

                            if (verbose) Console.WriteLine("Output: " + servo.Name + " Port " + servo.USBPort + " Channel " + servo.Channel + " uSEc " + servoValue);
                        }
                    }
                }
            }
        }


        IOMap doneAction = null;

        //foreach (IOMap timerActions in settings.IO_Mapping.TimerActions)
        //{

        //    foreach (Output io in timerActions.OutputList)
        //    {
        //        if (now > io.lastFired + io.Timer)
        //        {
        //            io.lastFired = now;

        //            // If negative run until stopped.
        //            if (io.iterations >= 0)
        //            {
        //                io.iterationCount++;
        //                if (io.count > io.iterations)
        //                {
        //                    doneAction = timerActions;
        //                }
        //            }

        //            var action = io.Actions[io.count++];
        //            if (verbose) Console.WriteLine("Run Timer Action");
        //            RunAction(action, settings, io, verbose, now, true);

        //            if (io.count == io.Actions.Count) io.count = 0;

        //        }
        //    }
        //}
        //if (doneAction != null)
        //{
        //    settings.IO_Mapping.TimerActions.Remove(doneAction);
        //}

        //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.B, new List<Output> {
        //    new Output(browLeftTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1327 }),
        //    new Output(browRightTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1495 }),
        //    new Output(leftIris, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1675 }),
        //    new Output(rightIris, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1000 }),
        //    new Output(browRightTop, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
        //    new Output(browLeftTop, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
        //    new Output(browLeftBottom, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
        //    new Output(browRightBottom, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
        //    // Fade, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, (In,Out), step, lowest brightness
        //    new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"Fade,255,0,0,200,both,lr,5,IN,1,0"})
        //}));


        foreach (Control c in xbox.CButtons)
        {
           
            // Look just for mappings for this MUX option.
            foreach (IOMap io in iO_Mapping)
            {
                if (c.Name == io.Input.ToString())
                {
                   
                    var controlValue = c.Value;

                    if (verbose) Console.WriteLine(" Input: " + c.Name + ": " + c.Value);
                                       
                 
                    ParseIOMap(io, settings, verbose, now);

                }
            }
        }

        // Initially setup to control servos only.
        foreach (Control c in xbox.CTriggers)
        {        
            foreach (IOMap io in iO_Mapping)
            {
                if (c.Name == io.Input.ToString())
                {
                    var controlValue = c.Value;
                   
                    if (verbose) Console.WriteLine(" Input: " + c.Name + ": " + c.Value);

                    foreach (Output output in io.OutputList)
                    {
                        if (output.robotControl != null)
                        {
                            if (output.robotControl == RobotControls.RightEyePop || output.robotControl == RobotControls.LeftEyePop)
                            {
                                var stepperValue = Servo.MapTriggerEyePop(c);

                                if (output.robotControl == RobotControls.LeftEyePop)
                                {
                                    if(settings.LTicController != null && settings.LTicController.CurrentValue != stepperValue)
                                            settings.LTicController.MoveToPosition(stepperValue);
                                }
                                else
                                {
                                    if (settings.RTicController != null && settings.RTicController.CurrentValue != stepperValue)
                                        settings.RTicController.MoveToPosition(stepperValue);
                                }
                            }
                            else
                            {
                                var servo = settings.Servos[(int)output.robotControl];

                                var servoValue = Servo.MapTriggertoServo(c, servo, (output.MultiOutput == MultiOutput.Reversed));

                                if (servo.CurrentPosition == servoValue)
                                {
                                    //Not moving anymore, disable servo
                                    // output.Servo.DisableServo();

                                    //disableChannels.Add(output.Servo.Channel);
                                    //servoDisable = true;
                                }
                                else
                                {
                                    axisServos.Add(servo);
                                    axisTargetus.Add(servoValue);
                                    servoWrite = true;
                                }

                                servo.CurrentPosition = servoValue;

                                if (verbose) Console.WriteLine("Output: " + servo.Name + " Port " + servo.USBPort + " Channel " + servo.Channel + " uSEc " + servoValue);
                            }
                        }
                    }
                }
            }

        }

        if (servoWrite)
        {
            var disabledServos = new List<Servo>();
            foreach (Servo s in axisServos)
            {
                if (s.isDisabled)
                    disabledServos.Add(s);
            }
            if (disabledServos.Count > 0) Servo.SetTargetsLastPositionBatch(disabledServos);

            Servo.SetTargetsBatch(axisServos, axisTargetus.ToArray());
        }
        else
        {
            var disableServos = new List<Servo>();
            foreach (Servo s in settings.Servos)
            {
                if (!s.isDisabled)
                {
                    disableServos.Add(s);
                }
            }

            if (disableServos.Count > 0) disableServos[0].GetPositionCompareDisable(disableServos);
        }

        return servoWrite;
    }

    private static void ParseIOMap(IOMap io, SettingsObject settings, bool verbose, DateTime now)
    {
        var actionResults = new ActionResults();            

        if (io.OutputList.Count > 0)
        {
            foreach (Output output in io.OutputList)
            {
                // This is debounce logic so toggles can work giving you one second to get your finger off a button before you can run it again.
                if (output.Timer != TimeSpan.Zero)
                {
                    if (settings.IO_Mapping.TimerActions.Contains(io))
                    {
                        settings.IO_Mapping.TimerActions.Remove(io);
                    }
                    else
                    {
                        output.count = 0;
                        output.lastFired = DateTime.MinValue;
                        settings.IO_Mapping.TimerActions.Add(io);
                    }

                    continue;
                }

                var action = output.Actions[0];
                int? value = null;
                var pathname = "";

                if (output.pathNames != null)
                    pathname = output.pathNames[0];

                if (output.Values != null)
                    value = output.Values[0];

                // Handles Toggle or sequences
                if (output.Actions.Count > 1)
                {
                    action = output.Actions[output.count];
                    if (output.pathNames != null && output.pathNames.Count > 1)
                        pathname = output.pathNames[output.count];
                }

                var ranAction = RunActions.RunAction(actionResults, action, settings, output, verbose, now, false, value, pathname);

                // Toggle logic allows cycling through values
                if (ranAction && output.Actions.Count > 1)
                {
                    output.count = output.count + 1;
                    if (output.count == output.Actions.Count) output.count = 0;
                }
            }
        }
        else if ( io.CommandList != null ) 
        {
            foreach (Command command in io.CommandList)
            {
                var ranAction = RunCommands.RunCommand(actionResults, settings, command, verbose);
            }
        }

        var actionMoved = (actionResults.deltaServos.Count > 0);

        if (actionMoved)
        {
            var reEnableServos = new List<Servo>();

            foreach (Servo s in actionResults.deltaServos)
            {
                if (s.isDisabled) reEnableServos.Add(s);
            }

            if (reEnableServos.Count > 0)
            {
                Servo.SetTargetsLast(reEnableServos); // Reenables any disabled servos, by sending their current position 
                Servo.ConfigureSpeedLast(reEnableServos); // Resets the speed after they are re-enabled by sending a position
            }
            Servo.SetTargetsBatch(actionResults.deltaServos, actionResults.DeltaValues.ToArray());

            foreach (TicDeltas ticDeltas in actionResults.TicDeltas)
            {
                if (ticDeltas.isLeft)
                    settings.LTicController.MoveToPosition(ticDeltas.position);
                else
                    settings.RTicController.MoveToPosition(ticDeltas.position);
            }

           
        }
        else if (actionResults.TicDeltas.Count > 0)
        {
            {
                foreach (TicDeltas ticDeltas in actionResults.TicDeltas)
                {
                    if (ticDeltas.isLeft)
                        settings.LTicController.MoveToPosition(ticDeltas.position);
                    else
                        settings.RTicController.MoveToPosition(ticDeltas.position);
                }
            }

        }
    }

}


public class ActionResults
{
    public bool ranAction ;
    public List<Servo> deltaServos ;
    //public List<int> DeltaChannels;
    public List<int> DeltaValues;
    public bool SetBeforeSpeed;
    public ServoSpeed BeforeSpeed;
    public ServoSpeed AfterSpeed;
    public List<Servo> MaestroSettingsAfter;
    public List<TicDeltas> TicDeltas;
    public bool DisableServosAfterRunning;
    public int msDelay;
    public int AudioIndex;
    public ActionResults()
    {
        ranAction = false;
        deltaServos = new List<Servo>();
        //DeltaChannels = new List<int>();
        DeltaValues = new List<int>();        
        TicDeltas = new List<TicDeltas>();
        SetBeforeSpeed = false;
        MaestroSettingsAfter = new List<Servo>();
        DisableServosAfterRunning = false;
        msDelay = 0;
        AudioIndex = 0;
    }
}

public class TicDeltas
{
    public bool isLeft;
    public int position;

    public TicDeltas(bool isLeft, int position)
    {
        this.isLeft = isLeft;
        this.position = position;
    }
}
public class Deltas
{
    public int Channel;
    public int USec;

    public Deltas(int Channel, int USec)
    {
        this.Channel = Channel;
        this.USec = USec;
    }
}



public class Sounds
{
    public string Path;

    public Sounds(string Path)
        { this.Path = Path;}
    public void PlayMp3Async()
    {
        Task.Run(() =>
        {
            using var reader = new AudioFileReader(Path);
            using var output = new WaveOutEvent();
            output.Init(reader);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(10);
        });
    }

}