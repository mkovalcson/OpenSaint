
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace OpenSaintTestHarnessConsoleApp
{
    public static class RunCommands
    {
        /// <summary>
        /// RunCommand switches on the various ButtonActions to drive controls based on the XBox controller
        /// 
        /// Technical Debt:
        /// This needs to be integrated with the RunActions method.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="settings"></param>
        /// <param name="command"></param>
        /// <param name="verbose"></param>
        /// <returns></returns>

        public static bool RunCommand(ActionResults results, SettingsObject settings, Command command, bool verbose)
        {
            Servo thisServo;
            var servoList = new List<RobotControls>();

            switch (command.Action)
            {

                // Indexed array of Ganged Servos is preset
                // Value is +/- 100 
                case ButtonActions.ServoGangedDelta:

                    var gangedList = settings.GangedServoList[(int)command.GangedServosName];

                    foreach (GangedServo o in gangedList.list)
                    {
                       
                        var servo = settings.Servos[(int)o.control];
                        var servoValue = Servo.MapDeltatoServo(command.Value, servo, (o.orientation == MultiOutput.Reversed), gangedList.isCentered);
                        if (verbose) Console.WriteLine("Output: " + servo.Name + " Command Value: " + command.Value + " Servo Value: " + servoValue);
                        results.deltaServos.Add(servo);
                        results.DeltaValues.Add((int)servoValue);
                    }
                    break;

                // Value is +/- 100
                case ButtonActions.ServoDelta:
                    var servod = settings.Servos[(int)command.robotControl];
                    var servodValue = Servo.MapDeltatoServo(command.Value, servod, servod.Reverse, (servod.Startposition == StartPosition.Home));
                    results.deltaServos.Add(servod);
                    results.DeltaValues.Add((int)servodValue);
                    if (verbose) Console.WriteLine("Output: " + servod.Name + " Command Value: " + command.Value +" Servo Value: "+ servodValue);
                    break;

                case ButtonActions.ServoSetRandom:

                    var dummycontrol = new Control(ControlType.Axis, 0, "dummy");
                    dummycontrol.Low = -32767;
                    dummycontrol.High = 32767;

                    Random rand = new Random();
                    command.Value = rand.Next(command.BottomValue, command.TopValue);

                    //foreach (GangedServo o in command.GangedServos)
                    //{
                    //    var isRaw = true;
                    //    dummycontrol.Value = command.Value;
                    //    var servo = settings.Servos[(int)o.control];
                    //    var servoValue = Servo.MapAxis(dummycontrol, servo, (o.orientation == MultiOutput.Reversed),isRaw);
                    //    results.deltaServos.Add(servo);
                    //    results.DeltaValues.Add((int)servoValue);
                    //}
                    break;

                case ButtonActions.ServoSetMode:
                    thisServo = settings.Servos[(int)command.robotControl];
                    thisServo.Mode = command.Mode;
                    break;


                #region Single / Multi Servo Commands

                case ButtonActions.ServoMin:
                    //if (command.GangedServos.Count > 0)
                    //{
                    //    foreach (GangedServo gs in command.GangedServos)
                    //    {
                    //        thisServo = settings.Servos[(int)gs.control];
                    //        results.deltaServos.Add(thisServo);
                    //        results.DeltaValues.Add(thisServo.Reverse ? thisServo.LimitUpper : thisServo.LimitLower);

                    //        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + "Min");
                    //    }
                    //}
                    //else
                    //{
                    thisServo = settings.Servos[(int)command.robotControl];
                    if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                    {
                        if (command.robotControl == RobotControls.NoseBody) settings.NoseUp = false;
                        // output.Servo.GoMin();
                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add(thisServo.Reverse ? thisServo.LimitUpper : thisServo.LimitLower);
                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Min");
                    }
                    //}
                    break;
                case ButtonActions.ServoMax:
                    //if (command.GangedServos.Count > 0)
                    //{
                    //    foreach (GangedServo gs in command.GangedServos)
                    //    {
                    //        thisServo = settings.Servos[(int)gs.control];
                    //        results.deltaServos.Add(thisServo);
                    //        results.DeltaValues.Add(thisServo.Reverse ? thisServo.LimitLower : thisServo.LimitUpper);

                    //        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + "Max");
                    //    }
                    //}
                    //else
                    //{
                    thisServo = settings.Servos[(int)command.robotControl];
                    if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                    {
                        // output.Servo.GoMax();
                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add((thisServo.Reverse ? thisServo.LimitLower : thisServo.LimitUpper));
                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Max");
                    }
                    //}
                    break;
                case ButtonActions.ServoHome:
                    //if (command.GangedServos.Count > 0)
                    //{
                    //    foreach (GangedServo gs in command.GangedServos)
                    //    {
                    //        thisServo = settings.Servos[(int)gs.control];
                    //        results.deltaServos.Add(thisServo);
                    //        results.DeltaValues.Add(thisServo.HomeValue);

                    //        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + "Home");
                    //    }
                    //}
                    //else
                    //{
                    thisServo = settings.Servos[(int)command.robotControl];
                    if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                    {
                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add(thisServo.HomeValue);
                        if (command.robotControl == RobotControls.NoseBody) settings.NoseUp = false;
                        //output.Servo.GoHome();
                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Home");
                    }
                    //}
                    break;


                case ButtonActions.ServoHomeDelta:

                    //if (command.GangedServos.Count > 0)
                    //{
                    //    foreach (GangedServo gs in command.GangedServos)
                    //    {
                    //        thisServo = settings.Servos[(int)gs.control];
                    //        results.deltaServos.Add(thisServo);

                    //        var delta = (gs.orientation == MultiOutput.Reversed) ? -command.Value : command.Value;
                    //        results.DeltaValues.Add(thisServo.HomeValue + delta);

                    //        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + delta);
                    //    }
                    //}
                    //else
                    //{
                    thisServo = settings.Servos[(int)command.robotControl];

                    results.deltaServos.Add(thisServo);
                    results.DeltaValues.Add(thisServo.HomeValue + command.Value);

                    if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + command.Value);
                    //}

                    break;
                                   

                #endregion


                case ButtonActions.ServoValue:
                    thisServo = settings.Servos[(int)command.robotControl];
                    if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                    {
                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add((int)command.Value);
                        if (command.robotControl == RobotControls.NoseBody) settings.NoseUp = (command.Value <= thisServo.ModeValue);
                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + command.Value);
                    }
                    break;

                #region Eye Pop Commands

                case ButtonActions.EyePopValue:
                    var ticValue = command.Value;
                    results.TicDeltas.Add(new TicDeltas(true, ticValue));
                    results.TicDeltas.Add(new TicDeltas(false, ticValue));
                    break;


                case ButtonActions.EyePopLeftOpen:
                    results.SetBeforeSpeed = true;
                    results.BeforeSpeed = ServoSpeed.Default;

                    servoList = new List<RobotControls> { RobotControls.BrowLeftTopTilt, RobotControls.BrowLeftTopOpen,
                    RobotControls.BrowLeftBottomOpen,  RobotControls.NoseBody };

                    foreach (var control in servoList)
                    {
                        results.deltaServos.Add(settings.Servos[(int)control]);
                        results.DeltaValues.Add(settings.Servos[(int)control].HomeValue);
                    }

                    settings.EyesPopped = true;
                    results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.maxValue));

                    if (verbose) Console.WriteLine("Left EyePop Open ");
                    break;


                case ButtonActions.EyePopNoSafety:
                    settings.EyesPopped = true;
                    settings.EyesUnpopped = DateTime.MinValue;
                    results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.maxValue));
                    results.TicDeltas.Add(new TicDeltas(false, settings.RTicController.maxValue));
                    break;

                case ButtonActions.EyePopOpen:
                    results.SetBeforeSpeed = true;
                    results.BeforeSpeed = ServoSpeed.Default;

                    servoList = new List<RobotControls> { RobotControls.BrowLeftTopTilt, RobotControls.BrowRightTopTilt,
                    RobotControls.BrowRightTopOpen, RobotControls.BrowLeftTopOpen,
                    RobotControls.BrowLeftBottomOpen,  RobotControls.BrowRightBottomOpen};


                    foreach (var control in servoList)
                    {
                        results.deltaServos.Add(settings.Servos[(int)control]);
                        results.DeltaValues.Add(settings.Servos[(int)control].HomeValue);
                    }
                    results.deltaServos.Add(settings.Servos[(int)RobotControls.NoseBody]);
                    results.DeltaValues.Add(settings.Servos[(int)RobotControls.NoseBody].ModeValue);

                    settings.EyesPopped = true;
                    settings.EyesUnpopped = DateTime.MinValue;
                    results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.maxValue));
                    results.TicDeltas.Add(new TicDeltas(false, settings.RTicController.maxValue));

                    if (verbose) Console.WriteLine("EyePop Open ");
                    break;

                case ButtonActions.EyePopHalfOpen:
                    results.SetBeforeSpeed = true;
                    settings.EyesUnpopped = DateTime.MinValue;
                    results.BeforeSpeed = ServoSpeed.Default;

                    servoList = new List<RobotControls> { RobotControls.BrowLeftTopTilt, RobotControls.BrowRightTopTilt,
                    RobotControls.BrowRightTopOpen, RobotControls.BrowLeftTopOpen,
                    RobotControls.BrowLeftBottomOpen,  RobotControls.BrowRightBottomOpen,
                   };

                    foreach (var control in servoList)
                    {
                        results.deltaServos.Add(settings.Servos[(int)control]);
                        results.DeltaValues.Add(settings.Servos[(int)control].HomeValue);
                    }
                    results.deltaServos.Add(settings.Servos[(int)RobotControls.NoseBody]);
                    results.DeltaValues.Add(settings.Servos[(int)RobotControls.NoseBody].ModeValue);

                    settings.EyesPopped = true;
                    results.TicDeltas.Add(new TicDeltas(true, 1000));
                    results.TicDeltas.Add(new TicDeltas(false, 1000));

                    if (verbose) Console.WriteLine("EyePop Open ");
                    break;

                case ButtonActions.EyePopClosed:
                    results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.minValue));
                    results.TicDeltas.Add(new TicDeltas(false, settings.RTicController.minValue));
                    settings.EyesUnpopped = DateTime.Now.AddSeconds(0.5);
                    if (verbose) Console.WriteLine("EyePop Close ");
                    break;

                case ButtonActions.EyePopLeftClosed:
                    results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.minValue));
                    // settings.LTicController.MoveMin();              
                    settings.EyesUnpopped = DateTime.Now.AddSeconds(0.5);
                    if (verbose) Console.WriteLine("EyePop Close ");
                    break;

                #endregion


                case ButtonActions.RGBCommand:
                    if (verbose) Console.WriteLine("RGB " + command.pathName);
                    settings.Lights.Command(command.pathName);
                    break;

                case ButtonActions.ServoModeValue:
                    thisServo = settings.Servos[(int)command.robotControl];
                    results.deltaServos.Add(thisServo);
                    results.DeltaValues.Add(thisServo.ModeValue);
                    if (command.robotControl == RobotControls.NoseBody) settings.NoseUp = true;
                    if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Mode");

                    break;

                #region Audio Commands


                case ButtonActions.PlayThis:
                    var m = settings.SelectedMovie;
                    var sceneName = m.Scenes[m.SceneIndex].SceneName;
                    var audioPath = m.MovieFolder + "\\" + sceneName + "\\" + command.pathName;
                    if (verbose) Console.WriteLine("Play This: " + command.pathName);
                    var pt = new Sounds(audioPath);
                    pt.PlayMp3Async();
                    break;

                case ButtonActions.PlayFirst:
                    settings.SoundBiteIndex = 0;

                    if (verbose) Console.WriteLine("Play First sound " + settings.SoundBites[settings.SoundBiteIndex]);
                    var pf = new Sounds(settings.SoundBites[settings.SoundBiteIndex]);
                    pf.PlayMp3Async();
                    break;

                case ButtonActions.PlayCurrent:
                    var s = new Sounds(settings.SoundBites[settings.SoundBiteIndex]);
                    if (verbose) Console.WriteLine("Play Current sound " + settings.SoundBites[settings.SoundBiteIndex]);
                    s.PlayMp3Async();
                    break;

                case ButtonActions.PlayPrevious:
                    settings.SoundBiteIndex = settings.SoundBiteIndex - 1;
                    if (settings.SoundBiteIndex < 0) settings.SoundBiteIndex = 0;
                    if (verbose) Console.WriteLine("Play Previous " + settings.SoundBites[settings.SoundBiteIndex]);
                    var ps = new Sounds(settings.SoundBites[settings.SoundBiteIndex]);
                    ps.PlayMp3Async();
                    break;

                case ButtonActions.PlayNext:
                    settings.SoundBiteIndex = settings.SoundBiteIndex + 1;
                    if (settings.SoundBiteIndex >= settings.SoundBites.Length) settings.SoundBiteIndex = 0;
                    if (verbose) Console.WriteLine("Play Next " + settings.SoundBites[settings.SoundBiteIndex]);
                    var ns = new Sounds(settings.SoundBites[settings.SoundBiteIndex]);
                    ns.PlayMp3Async();
                    break;

                #endregion

                case ButtonActions.MaestroSet:
                    thisServo = settings.Servos[(int)command.robotControl];
                    thisServo.ConfigureSpeed(command.Speed);
                    //if (output.RunOrder == RunOrder.Before)
                    //{
                    //    results.SetBeforeSpeed = true;
                    //    results.BeforeSpeed = output.Speed;
                    //}
                    //else
                    //{
                    //    results.MaestroSettingsAfter.Add(output.Servo);
                    //    results.AfterSpeed = output.Speed;
                    //}
                    break;

                case ButtonActions.MaestroSetAll:

                    Servo.ConfigureSpeedAll(settings.Servos, command.Speed);

                    //foreach (Servo s1 in settings.Servos)
                    //{
                    //   s1.ConfigureSpeed(output.Speed);
                    //}

                    if (verbose) Console.WriteLine("Set Speed " + command.Speed.ToString());
                    //results.SetBeforeSpeed = true;
                    //results.BeforeSpeed = output.Speed;
                    break;

                case ButtonActions.DisableServo:

                    settings.Servos[(int)command.robotControl].DisableServo();
                    settings.Servos[(int)command.robotControl].isDisabled = true;
                    break;

                case ButtonActions.DisableAllRunningServos:
                    results.DisableServosAfterRunning = true;
                    if (command.Value > 0)
                    {
                        results.msDelay = command.Value;
                    }
                    break;

                case ButtonActions.DisableAllServos:
                    foreach (Servo s1 in settings.Servos)
                    {

                        s1.DisableServo();
                        s1.isDisabled = true;

                    }

                    break;

                case ButtonActions.ServoAllGoHome:
                    foreach (Servo s1 in settings.Servos)
                    {
                        s1.GoHome();
                    }

                    break;

                default:
                    // How did we get here.

                    break;
            }

            return true;
        }


    }
}
