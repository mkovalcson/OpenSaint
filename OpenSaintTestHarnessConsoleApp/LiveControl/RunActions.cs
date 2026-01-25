using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSaintTestHarnessConsoleApp
{
    public static class RunActions
    {
        /// <summary>
        /// This was created first for live XBox control, but it now overlaps with the RunCommands functionality which was created for animated sequences.
        /// 
        /// Technical Debt:
        /// The shared functionality of these two need to be merged.
        /// 
        /// </summary>
        /// <param name="results"></param>
        /// <param name="action"></param>
        /// <param name="settings"></param>
        /// <param name="output"></param>
        /// <param name="verbose"></param>
        /// <param name="now"></param>
        /// <param name="runAlways"></param>
        /// <param name="value"></param>
        /// <param name="pathname"></param>
        /// <returns></returns>

        public static bool RunAction(ActionResults results, ButtonActions action, SettingsObject settings, Output output, bool verbose, DateTime now, bool runAlways, int? value, string pathname)
        {
            try
            {
                Servo thisServo;
                var servoList = new List<RobotControls>();
                if (!runAlways && output.lastFired.AddSeconds(1) > now) return false;

                switch (action)
                {
                    case ButtonActions.Snapshot:
                        SaveServoSnapshot(settings, false);
                        break;

                    case ButtonActions.SnapshotSeries:
                        SaveServoSnapshot(settings, true);
                        break;

                    case ButtonActions.ServoValue:
                        thisServo = settings.Servos[(int)output.robotControl];
                        if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                        {
                            results.deltaServos.Add(thisServo);
                            results.DeltaValues.Add((int)value);
                        }
                        break;

                    case ButtonActions.ServoHomeDelta:
                        thisServo = settings.Servos[(int)output.robotControl];

                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add(thisServo.HomeValue + (int)value);

                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Value: " + (int)value);

                        break;


                    case ButtonActions.EyePopLeftOpen:
                        results.SetBeforeSpeed = true;
                        results.BeforeSpeed = ServoSpeed.Default;

                        results.deltaServos.Add(settings.Servos[(int)RobotControls.NoseBody]);
                        results.DeltaValues.Add((settings.Servos[(int)RobotControls.NoseBody].Reverse ? settings.Servos[(int)RobotControls.NoseBody].LimitLower : settings.Servos[(int)RobotControls.NoseBody].LimitUpper));

                        //settings.LeftBottomBrow.GoHome();  
                        results.deltaServos.Add(settings.Servos[(int)RobotControls.BrowLeftBottomOpen]);
                        results.DeltaValues.Add(settings.Servos[(int)RobotControls.BrowLeftBottomOpen].HomeValue);

                        settings.EyesPopped = true;
                        results.TicDeltas.Add(new TicDeltas(true, settings.LTicController.maxValue));

                        if (verbose) Console.WriteLine("Left EyePop Open ");
                        break;


                    case ButtonActions.EyePopSetZero:
                        settings.LTicController.MoveToPosition(-100);
                        settings.RTicController.MoveToPosition(-100);
                        Thread.Sleep(500);  // Give it half a second to move before setting zero point
                        settings.LTicController.SetCurrentPositionZero();
                        settings.RTicController.SetCurrentPositionZero();
                        break;

                    case ButtonActions.EyePopOpen:
                        results.SetBeforeSpeed = true;
                        results.BeforeSpeed = ServoSpeed.Default;

                        servoList = new List<RobotControls> { RobotControls.BrowLeftTopTilt, RobotControls.BrowRightTopTilt,
                    RobotControls.BrowRightTopOpen, RobotControls.BrowLeftTopOpen,
                    RobotControls.BrowLeftBottomOpen,  RobotControls.BrowRightBottomOpen,
                    RobotControls.NoseBody };

                        foreach (var control in servoList)
                        {
                            results.deltaServos.Add(settings.Servos[(int)control]);
                            results.DeltaValues.Add(settings.Servos[(int)control].HomeValue);
                        }

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
                    RobotControls.NoseBody };

                        foreach (var control in servoList)
                        {
                            results.deltaServos.Add(settings.Servos[(int)control]);
                            results.DeltaValues.Add(settings.Servos[(int)control].HomeValue);
                        }

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

                    case ButtonActions.RGBCommand:
                        if (verbose) Console.WriteLine("RGB " + pathname);
                        settings.Lights.Command(pathname);
                        break;

                    case ButtonActions.ServoMin:
                        thisServo = settings.Servos[(int)output.robotControl];
                        if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                        {
                            // output.Servo.GoMin();
                            results.deltaServos.Add(thisServo);
                            results.DeltaValues.Add(thisServo.Reverse ? thisServo.LimitUpper : thisServo.LimitLower);
                            if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Min");
                        }
                        break;

                    case ButtonActions.ServoModeValue:
                        thisServo = settings.Servos[(int)output.robotControl];
                        results.deltaServos.Add(thisServo);
                        results.DeltaValues.Add(thisServo.ModeValue);
                        if (output.robotControl == RobotControls.NoseBody) settings.NoseUp = true;
                        if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Mode");

                        break;

                    case ButtonActions.ServoMax:
                        thisServo = settings.Servos[(int)output.robotControl];
                        if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                        {
                            // output.Servo.GoMax();
                            results.deltaServos.Add(thisServo);
                            results.DeltaValues.Add((thisServo.Reverse ? thisServo.LimitLower : thisServo.LimitUpper));
                            if (verbose) Console.WriteLine("Output: " + thisServo.Name + "Max");
                        }
                        break;
                    case ButtonActions.ServoHome:
                        thisServo = settings.Servos[(int)output.robotControl];
                        if (!thisServo.EyePopSensitive || !settings.EyesPopped)
                        {
                            results.deltaServos.Add(thisServo);
                            results.DeltaValues.Add(thisServo.HomeValue);
                            //output.Servo.GoHome();
                            if (verbose) Console.WriteLine("Output: " + thisServo.Name + " Home");
                        }
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

                    case ButtonActions.MaestroSet:
                        thisServo = settings.Servos[(int)output.robotControl];
                        thisServo.ConfigureSpeed(output.Speed);
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

                        Servo.ConfigureSpeedAll(settings.Servos, output.Speed);

                        //foreach (Servo s1 in settings.Servos)
                        //{
                        //   s1.ConfigureSpeed(output.Speed);
                        //}

                        if (verbose) Console.WriteLine("Set Speed " + output.Speed.ToString());
                        //results.SetBeforeSpeed = true;
                        //results.BeforeSpeed = output.Speed;
                        break;

                    case ButtonActions.DisableServo:
                        settings.Servos[(int)output.robotControl].DisableServo();
                        break;

                    case ButtonActions.DisableAllRunningServos:
                        results.DisableServosAfterRunning = true;
                        if (output.Values.Count > 0)
                        {
                            results.msDelay = output.Values[0];
                        }
                        break;

                    case ButtonActions.DisableAllServos:
                        foreach (Servo s1 in settings.Servos)
                        {
                            s1.DisableServo();
                        }
                        break;

                    case ButtonActions.ServoAllGoHome:
                        foreach (Servo s1 in settings.Servos)
                        {
                            s1.GoHome();
                        }

                        break;

                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            output.lastFired = now;
            return true;
        }




        public static void SaveServoSnapshot(SettingsObject settings, bool isSeries)
        {
            string baseName = "";
            string exeFolder = AppContext.BaseDirectory;

            if (!isSeries || string.IsNullOrEmpty(settings.SnapshotBaseName))
            {
                Console.Write("Enter snapshot name: ");
                baseName = Console.ReadLine();
                settings.SnapshotBaseName = baseName;
            }
            else
            {
                baseName = settings.SnapshotBaseName;
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                Console.WriteLine("Invalid name. Aborting.");
                return;
            }

            // sanitize filename
            foreach (var c in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(c, '_');

            string dir = Directory.GetCurrentDirectory();

            // find all matching files
            int highest = GetHighestSnapshotNumber(dir, baseName);

            int next = highest + 1;
            string filePath = $"{exeFolder}{baseName}_{next}.txt";

            using (var writer = new StreamWriter(filePath))
            {
                foreach (var s in settings.Servos)
                {
                    writer.WriteLine(
                        $"new Command(RobotControls.{s.Name}, ButtonActions.MaestroSetting, {s.currentSpeed}, 0),");
                }

                foreach (var s in settings.Servos)
                {
                    writer.WriteLine(
                        $"new Command(RobotControls.{s.Name}, ButtonActions.ServoValue, {s.CurrentPosition}, 1000),");
                }
            }

            Console.WriteLine($"Snapshot written to: {filePath}");
        }

        public static int GetHighestSnapshotNumber(string directory, string baseName)
        {
            int highest = 0;

            // pattern: baseName_123.txt
            string searchPattern = $"{baseName}_*.txt";

            foreach (string file in Directory.GetFiles(directory, searchPattern))
            {
                string name = Path.GetFileNameWithoutExtension(file);

                // extract the suffix after last underscore
                int underscoreIndex = name.LastIndexOf('_');
                if (underscoreIndex < 0)
                    continue;

                string numberPart = name.Substring(underscoreIndex + 1);

                if (int.TryParse(numberPart, out int n))
                {
                    if (n > highest)
                        highest = n;
                }
            }

            return highest;
        }


    }
}
