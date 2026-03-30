using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSaintTestHarnessConsoleApp
{
    public static class RunMovies
    {
        /// <summary>
        /// This is the main driving process for Animated sequences.
        /// 
        /// Movies -> Scenes -> Sequences -> Commands
        /// 
        /// Trigger = Automated - plays from beginning to end.
        /// Trigger = Pedals - Plays one Sequence at a time triggered by pressing a key.
        /// 
        /// Pedals is probably the wrong name since keyboard works as well. I just use a foot pedal.
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="verbose"></param>
        /// <param name="consoleKey"></param>
        /// <returns></returns>
        public static bool RunMovie(SettingsObject settings, bool verbose, ConsoleKey? consoleKey)
        {
            var now = DateTime.Now;
            var m = (Movie)settings.SelectedMovie;

            // if initial call to run the movie
            if (!m.CommandsExpanded)
            {
                // Expand Nested Commands (repeated and lists within lists )
                foreach (Scene sc in m.Scenes)
                {
                    foreach (Sequence seq in sc.Sequences)
                    {
                        Console.WriteLine("Command List before: "+ seq.CommandList.Count);
                        var expandedCommandList = ExpandCommandsRecursive(seq.CommandList, seq.MsDelay);
                        Console.WriteLine("Command List After: " + expandedCommandList.Count);

                        seq.CommandList = expandedCommandList;
                    }
                    var seqCount = 0;
                    foreach (Sequence seq in sc.Sequences)
                    {
                        Console.WriteLine("Sequence: " + seqCount++);
                        foreach (Command c in seq.CommandList)
                        {
                            if(c.Action == ButtonActions.ServoGangedDelta)
                            {
                                if (verbose) Console.WriteLine("Delay:" + c.Delay.TotalMilliseconds + " Action: " + c.Action.ToString() + " Control " + c.GangedServosName.ToString() + " Value: " + c.Value);
                            }
                            else if (verbose) Console.WriteLine("Delay:"+ c.Delay.TotalMilliseconds + " Action: "+ c.Action.ToString() + " Control " +c.robotControl.ToString()+ " Value: " + c.Value );
                        }
                    }
                }
              

                m.CommandsExpanded = true;
            }

            var scene = m.Scenes[m.SceneIndex];

            // If autopilot and movie is not done.
            if (m.Trigger == TriggerSource.Automatic && m.Done == false)
            {
                if (m.SceneIndex == 0 && scene.SceneRunning == false)
                {
                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                    {
                        c.lastFired = null;
                        c.TimeToFire = now + c.Delay;
                    }
                    scene.SceneRunning = true;
                }

                if (scene.SceneRunning)
                {
                    if (scene.Sequences[scene.SequenceIndex].FireTime < DateTime.Now && scene.Sequences[scene.SequenceIndex].IsComplete == false)
                    {
                        var sequenceComplete = RunCommandList(scene, settings, verbose, now);                      

                        if (sequenceComplete)
                        {
                            var thisSequence = scene.Sequences[scene.SequenceIndex];

                            var stillRepeating = false;

                            if (thisSequence.iterations > 0)
                            {
                                thisSequence.iterationCount++;

                                // Reset timed Commands for next iteration
                                if (thisSequence.iterationCount <= thisSequence.iterations)
                                {
                                    stillRepeating = true;
                                    scene.Sequences[scene.SequenceIndex].IsComplete = false;
                                    scene.SceneRunning = true;
                                    scene.Sequences[scene.SequenceIndex].FireTime = now + scene.Sequences[scene.SequenceIndex].MsDelay;
                                    // Reset the sequence timeout.
                                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                                    {
                                        c.lastFired = null;
                                        c.TimeToFire = now + scene.Sequences[scene.SequenceIndex].MsDelay + c.Delay;
                                    }
                                }
                            }

                            if (!stillRepeating)
                            {
                                scene.Sequences[scene.SequenceIndex].IsComplete = true;
                                scene.SequenceIndex++;

                                if (scene.SequenceIndex == scene.Sequences.Count)
                                {

                                    m.SceneIndex = 0;
                                    m.Scenes[0].SceneRunning = false;
                                    m.Done = true;
                                    return false;
                                }

                                scene.Sequences[scene.SequenceIndex].IsComplete = false;
                                scene.SceneRunning = true;
                                scene.Sequences[scene.SequenceIndex].FireTime = now + scene.Sequences[scene.SequenceIndex].MsDelay;
                                // Reset the sequence timeout.
                                foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                                {
                                    c.lastFired = null;
                                    c.TimeToFire = now + scene.Sequences[scene.SequenceIndex].MsDelay + c.Delay;
                                }
                            }


                            if (false && !stillRepeating)
                            {
                                scene.Sequences[scene.SequenceIndex].IsComplete = true;

                                scene.SequenceIndex++;

                                if (scene.SequenceIndex == scene.Sequences.Count)
                                {
                                    // If not last scene advance to next scene.
                                    if (m.SceneIndex + 1 < m.Scenes.Count)
                                    {
                                        scene.SceneRunning = false;
                                        m.SceneIndex++;

                                        scene = m.Scenes[m.SceneIndex];
                                        scene.SceneRunning = true;
                                        scene.SequenceIndex = 0;
                                    }
                                    else if (m.IsRepeating)
                                    {
                                        // If just scene repeat the scene
                                        if (m.Scenes.Count == 1)
                                            scene.SequenceIndex = 0;
                                        else
                                        {
                                            scene.SceneRunning = false;
                                            m.SceneIndex = 0;

                                            scene = m.Scenes[m.SceneIndex];
                                            scene.SceneRunning = true;
                                            scene.SequenceIndex = 0;
                                        }
                                    }
                                    else
                                    {
                                        scene.SequenceIndex--;
                                        return false;  // done
                                    }


                                    var nextSequence = scene.Sequences[scene.SequenceIndex];
                                    nextSequence.IsComplete = false;

                                    nextSequence.FireTime = now + nextSequence.MsDelay;

                                    // reset each command and set their fire times
                                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                                    {
                                        c.lastFired = null;
                                        c.TimeToFire = now + scene.Sequences[scene.SequenceIndex].MsDelay + c.Delay;
                                    }
                                }
                            }

                        }

                    }

                }
            }
            else if (consoleKey != null)
            {
                // Play Next Sequence
                if (consoleKey == ConsoleKey.RightArrow)
                {
                    // If not beginning save positions.
                    if (scene.SequenceIndex != 0)
                    {
                        // Save Servo Positions and Speed before moving forward.
                        var thisSequence = scene.Sequences[scene.SequenceIndex];
                        foreach (Servo servo in settings.Servos)
                        {
                            thisSequence.StartingServoPosition[(int)servo.Name] = servo.CurrentPosition;
                            thisSequence.StartingServoSpeed[(int)servo.Name] = servo.currentSpeed;
                        }

                    }
                    // Current Scene is done...
                    scene.Sequences[scene.SequenceIndex].IsComplete = true;
                    scene.SceneRunning = true;
                    scene.SequenceIndex++;
                    Console.WriteLine("Play Scene:" + scene.SceneName + "Sequence: " + scene.SequenceIndex);
                    var nextSequence = scene.Sequences[scene.SequenceIndex];
                    nextSequence.IsComplete = false;
                    m.BackgroundStart = DateTime.MaxValue;

                    nextSequence.FireTime = now + nextSequence.MsDelay;

                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                    {
                        c.lastFired = null;
                        c.TimeToFire = nextSequence.FireTime + c.Delay;
                    }

                }
                // Restart Scene
                else if (consoleKey == ConsoleKey.UpArrow)
                {
                    Console.WriteLine("Play Scene:" + scene.SceneName + "Sequence: " + scene.SequenceIndex);
                    var thisSequence = scene.Sequences[scene.SequenceIndex];

                    if (scene.SequenceIndex != 0)
                    {
                        // Reset Servos
                        foreach (Servo servo in settings.Servos)
                        {
                            servo.GoValue(thisSequence.StartingServoPosition[(int)servo.Name]);
                            servo.ConfigureSpeed(thisSequence.StartingServoSpeed[(int)servo.Name]);
                        }
                    }
                    thisSequence.IsComplete = false;

                    thisSequence.FireTime = now + thisSequence.MsDelay;

                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                    {
                        c.lastFired = null;
                        c.TimeToFire = thisSequence.FireTime + c.Delay;
                    }
                    scene.SceneRunning = true;
                    m.BackgroundStart = DateTime.MaxValue;
                }
                // Previous Scene
                else if (consoleKey == ConsoleKey.LeftArrow)
                {
                    scene.Sequences[scene.SequenceIndex].IsComplete = true;

                    scene.SequenceIndex--;
                    if (scene.SequenceIndex < 0) scene.SequenceIndex = 0;
                    Console.WriteLine("Play Scene:" + scene.SceneName + "Sequence: " + scene.SequenceIndex);
                    var thisSequence = scene.Sequences[scene.SequenceIndex];

                    if (scene.SequenceIndex != 0)
                    {
                        // Reset Servos
                        foreach (Servo servo in settings.Servos)
                        {
                            servo.GoValue(thisSequence.StartingServoPosition[(int)servo.Name]);
                            servo.ConfigureSpeed(thisSequence.StartingServoSpeed[(int)servo.Name]);
                        }
                    }
                    thisSequence.IsComplete = false;
                    scene.SceneRunning = true;
                    thisSequence.FireTime = now + thisSequence.MsDelay;
                    m.BackgroundStart = DateTime.MaxValue;
                    foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                    {
                        c.lastFired = null;
                        c.TimeToFire = thisSequence.FireTime + c.Delay;
                    }
                }
            }
            else
            {     

                // In loop see if it's time to do something.
                if (scene.SceneRunning)
                {
                    var thisSequence = scene.Sequences[scene.SequenceIndex];

                    if (thisSequence.FireTime < DateTime.Now && thisSequence.IsComplete == false)
                    {
                        RunSequence(scene, settings, verbose, now);
                    }

                }
                else
                {
                    // Run background Sequence in between other scenes.
                    if (m.BackgroundStart < DateTime.Now)
                    {
                        RunBackgroundSequence(m, settings, verbose, now);
                    }
                }

                // See if any moving servos have reached their end position
                // If they have disable them.
                var disableServos = new List<Servo>();
                foreach (Servo s in settings.Servos)
                {
                    if (!s.isDisabled)
                    {
                        disableServos.Add(s);
                    }
                }
               // if (disableServos.Count > 0) disableServos[0].GetPositionCompareDisable(disableServos);

            }


            return true;
        }

        private static void RunBackgroundSequence(Movie m, SettingsObject settings, bool verbose, DateTime now)
        {
            var sequenceComplete = true;
            var results = new ActionResults();
            if (m.BackgroundMotion != null)
            {
                foreach (Command c in m.BackgroundMotion.CommandList)
                {
                    if (c.lastFired == null && c.TimeToFire < now)
                    {
                        c.lastFired = now;

                        if (verbose) Console.WriteLine("Running Command:  " + c.Action.ToString());

                        RunCommands.RunCommand(results, settings, c, verbose);
                    }

                    if (c.lastFired == null)
                    {
                        sequenceComplete = false;
                    }
                }
            }

            if (results.DeltaValues.Count > 0)
            {
                var reEnableServos = new List<Servo>();

                foreach (Servo s in results.deltaServos)
                {
                    if (s.isDisabled) reEnableServos.Add(s);
                }

                if (reEnableServos.Count > 0)
                {
                    Servo.SetTargetsLast(reEnableServos); // Reenables any disabled servos, by sending their current position 
                    Servo.ConfigureSpeedLast(reEnableServos); // Resets the speed after they are re-enabled by sending a position
                }

                Servo.SetTargetsBatch(results.deltaServos, results.DeltaValues.ToArray());

            }

            if (results.TicDeltas.Count > 0)
            {
                foreach (TicDeltas ticDeltas in results.TicDeltas)
                {
                    if (ticDeltas.isLeft)
                        settings.LTicController.MoveToPosition(ticDeltas.position);
                    else
                        settings.RTicController.MoveToPosition(ticDeltas.position);
                }
            }

            if (sequenceComplete)
            {
                Console.WriteLine("Background Sequence Complete, Start over");
                if (m.BackgroundMotion != null)
                {
                    m.BackgroundStart = DateTime.Now;
                    int lastDelay = 0;
                    foreach (Command c in m.BackgroundMotion.CommandList)
                    {
                        c.lastFired = null;

                        if (c.Action == ButtonActions.ServoSetRandom)
                        {
                            Random rand = new Random();
                            c.Value = rand.Next(c.BottomValue, c.TopValue);
                            int delayValue = rand.Next(c.minDelay, c.maxDelay);
                            lastDelay = delayValue;
                            c.TimeToFire = DateTime.Now.AddMilliseconds(delayValue);
                        }
                        else if (c.Action == ButtonActions.DisableAllServos)
                        {
                            c.TimeToFire = DateTime.Now.AddMilliseconds(lastDelay + 250);
                        }
                        else
                        {
                            c.TimeToFire = DateTime.Now + c.Delay;
                        }
                    }
                }
            }

        }

        public static bool RunCommandList(Scene scene, SettingsObject settings, bool verbose, DateTime now)
        {
           
            var sequenceComplete = true;
            var results = new ActionResults();

            foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
            {
                if (c.lastFired == null && c.TimeToFire < now)
                {
                    c.lastFired = now;

                    Command? alternateCommand = null;
                    Console.WriteLine("Running Command:  " + c.Action.ToString());
                    switch (c.Action)
                    {
                        case ButtonActions.PlayFirst:
                            alternateCommand = new Command(ButtonActions.PlayThis, scene.Sequences[scene.SequenceIndex].AudioTracks[0] , c.Delay.Milliseconds); //scene.AudioTracks[0]
                            break;
                        case ButtonActions.PlayCurrent:
                            
                            alternateCommand = new Command(ButtonActions.PlayThis, scene.AudioTracks[scene.Sequences[scene.SequenceIndex].AudioIndex], c.Delay.Milliseconds);
                            break;
                        case ButtonActions.PlayPrevious:
                            scene.AudioIndex--;
                            scene.Sequences[scene.SequenceIndex].AudioIndex--;
                            alternateCommand = new Command(ButtonActions.PlayThis, scene.AudioTracks[scene.Sequences[scene.SequenceIndex].AudioIndex], c.Delay.Milliseconds);
                            break;
                        case ButtonActions.PlayNext:
                            scene.AudioIndex++;
                            scene.Sequences[scene.SequenceIndex].AudioIndex++;
                            alternateCommand = new Command(ButtonActions.PlayThis, scene.AudioTracks[scene.Sequences[scene.SequenceIndex].AudioIndex], c.Delay.Milliseconds);
                            break;
                    }

                    if (alternateCommand == null)
                        RunCommands.RunCommand(results, settings, c, verbose);
                    else
                        RunCommands.RunCommand(results, settings, alternateCommand, verbose);
                }

                if (c.lastFired == null)
                {
                    sequenceComplete = false;
                }
            }

            if (results.DeltaValues.Count > 0)
            {
                var reEnableServos = new List<Servo>();

                foreach (Servo s in results.deltaServos)
                {
                    if (s.isDisabled) reEnableServos.Add(s);
                }

                if (reEnableServos.Count > 0)
                {
                    Servo.SetTargetsLast(reEnableServos); // Reenables any disabled servos, by sending their current position 
                  //  Servo.ConfigureSpeedLast(reEnableServos); // Resets the speed after they are re-enabled by sending a position
                }

                Servo.SetTargetsBatch(results.deltaServos, results.DeltaValues.ToArray());
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
             //  if (disableServos.Count > 0) disableServos[0].GetPositionCompareDisable(disableServos);
            }

            if (results.TicDeltas.Count > 0)
            {
                foreach (TicDeltas ticDeltas in results.TicDeltas)
                {
                    if (ticDeltas.isLeft)
                        settings.LTicController.MoveToPosition(ticDeltas.position);
                    else
                        settings.RTicController.MoveToPosition(ticDeltas.position);
                }
            }

            return sequenceComplete;
        }

        private static void RunSequence(Scene scene, SettingsObject settings, bool verbose, DateTime now)
        {
            var sequenceComplete = RunCommandList(scene, settings, verbose, now);

            if (sequenceComplete)
            {
                var thisSequence = scene.Sequences[scene.SequenceIndex];

                var stillRepeating = false;

                if (thisSequence.iterations > 0)
                {
                    thisSequence.iterationCount++;
                    if (thisSequence.iterationCount <= thisSequence.iterations)
                    {
                        stillRepeating = true;
                        scene.Sequences[scene.SequenceIndex].IsComplete = false;
                        scene.SceneRunning = true;

                        // Reset the sequence timeout.
                        foreach (Command c in scene.Sequences[scene.SequenceIndex].CommandList)
                        {
                            c.lastFired = null;
                            c.TimeToFire = thisSequence.FireTime + c.Delay;
                        }
                    }

                }

                if (!stillRepeating)
                {
                    var m = (Movie)settings.SelectedMovie;
                    Console.WriteLine("Sequence Complete");
                    scene.Sequences[scene.SequenceIndex].IsComplete = true;
                    scene.SceneRunning = false;
                    if (m.BackgroundMotion != null)
                    {
                        m.BackgroundStart = DateTime.Now + scene.Sequences[scene.SequenceIndex].BackgroundMsDelay;
                        foreach (Command c in m.BackgroundMotion.CommandList)
                        {
                            c.lastFired = null;
                            int lastDelay = 0;
                            if (c.Action == ButtonActions.ServoSetRandom)
                            {
                                Random rand = new Random();
                                c.Value = rand.Next(c.BottomValue, c.TopValue);
                                int delayValue = rand.Next(c.minDelay, c.maxDelay);
                                lastDelay = delayValue;
                                c.TimeToFire = DateTime.Now.AddMilliseconds(delayValue);
                            }
                            else if (c.Action == ButtonActions.DisableAllServos)
                            {
                                c.TimeToFire = DateTime.Now.AddMilliseconds(lastDelay + 250);
                            }
                            else
                            {
                                c.TimeToFire = DateTime.Now + c.Delay;
                            }
                        }
                    }
                }
            }

        }

        public static List<Command> ExpandCommandsRecursive(List<Command> commandList, TimeSpan delay)
        {
            // Calls repeating expansion at the same level first
            List<Command> cumulativeCommandList = ExpandRepeatCommands(commandList, delay);

            List<Command> cumulativeCommandList2 = new List<Command>();

            foreach (Command c in cumulativeCommandList)
            {
                if (c.SubCommands != null)
                {
                    var list = ExpandCommandsRecursive(c.SubCommands, c.Delay + delay);
                    cumulativeCommandList2.AddRange(list);
                }
                else
                {
                    // Adds non-repeating non-nested Command
                    var x = c.Clone();
                    x.Delay = c.Delay + delay;
                    cumulativeCommandList2.Add(x);
                }
            }
            return cumulativeCommandList2;
        }

        public static List<Command> ExpandRepeatCommands(List<Command> commandList, TimeSpan delay)
        {           
            // New list with expanded repeating commands.
            List<Command> cumulativeCommandList = new List<Command>();

            foreach (Command c in commandList)
            {
                // Add each existing Command if it's not a repeating command
                if (c.SubCommands == null || c.RepeatLoops == 0)
                {
                    var x = c.Clone();
                    cumulativeCommandList.Add(x);
                }

                // If this Command is repeated, add additional Commands with Loop offsets.
                if (c.SubCommands != null && c.RepeatLoops > 0)
                {
                    var loopDelay = c.RepeatDelay;

                    for (int loop = 1; loop <= c.RepeatLoops; loop++)
                    {
                        // Adds RepeatLoops number of Commands with appropriate offset delay for each loop
                        foreach (Command sub in c.SubCommands)
                        {
                            var y = sub.Clone();
                            var LoopCommand = y;
                            LoopCommand.Delay = sub.Delay + loopDelay * loop;
                            cumulativeCommandList.Add(LoopCommand);
                        }
                    }
                }
            }

            return cumulativeCommandList;
        }

    

    }
}
