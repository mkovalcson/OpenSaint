using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSaintTestHarnessConsoleApp
{
    /// <summary>
    /// Currently this is just a Unit Test for what I hope to hold reusable motion sequences
    /// </summary>

    public partial class CL
    {
        public static int RotateMe = 70;
        public static int RotateCamera = 40;

        public static Movie HeadsetvsBrain()
        {
            var scene1 = new Scene("Scene1")
            {
                AudioTracks = new List<string> {
               //"1_Substance.mp3", 
               //"2_Assuming.mp3", 
               //"3_RememberThat.mp3",
               //"4_CanYourEyes.mp3", 
               //"5_DistortionProfile.mp3",
               //"6_DoesntSeeSame.mp3",
               //"7_TasteDifferently.mp3",
               //     "8_Perceive.mp3",
               //     "9_SmartProgramming.mp3",
               //     "10_BrainsFoolYou.mp3",
               //"11_Theory.mp3",
               //"12_IntoCharacter.mp3","13_HAL9000Talks.mp3", "14_ChatGPTSays.mp3",
               // "15_PeopleNeedToLearnThis.mp3",
               // "16_WhyArgue.mp3",
               // "17_GoodThingHomework.mp3", 
                },
            
                Sequences = new List<Sequence>
                {
                new Sequence(CL.Init(),new List<string> {}, 0),
                new Sequence(CL.NeedsSubstance(),new List<string> {"1_Substance.mp3"}, 0),
                new Sequence(CL.Assuming(),new List<string> {"2_Assuming.mp3"},0),
                new Sequence(CL.YouRemember(),new List<string> {"3_RememberThat.mp3"},0),
                new Sequence(CL.CanYourEyesDoThis(),new List<string> {"4_CanYourEyes.mp3"},0),
                new Sequence(CL.DistortionProfile(),new List<string> { "5_DistortionProfile.mp3"},0),
                new Sequence(CL.CorrectedImage(),new List<string> { "6_DoesntSeeSame.mp3"},0),
                new Sequence(CL.Cilantro(),new List<string> {"7_TasteDifferently.mp3"},0),
                new Sequence(CL.YouSaid(),new List<string> { "8_Perceive.mp3",},0),
                new Sequence(CL.SmartProgramming(),new List<string> { "9_SmartProgramming.mp3",},0),
                new Sequence(CL.FoolYou(),new List<string> { "10_BrainsFoolYou.mp3",},0),
                new Sequence(CL.Theory(),new List<string> {"11_Theory.mp3"},0),
                new Sequence(CL.Hal9000(),new List<string> {"12_IntoCharacter.mp3","13_HAL9000Talks.mp3", "14_ChatGPTSays.mp3"},0),
                new Sequence(CL.PeopleLearnThis(),new List<string> { "15_PeopleNeedToLearnThis.mp3"},0),
                new Sequence(CL.WhyArgue(),new List<string> { "16_WhyArgue.mp3"},0),
                new Sequence(CL.GoodthingsHomework(),new List<string> {"17_GoodThingHomework.mp3"},0),
                }
            };

            var movieName = "HeadsetvsBrain";
            var newMovie = new Movie(movieName, AppContext.BaseDirectory + "MOVIES\\" + movieName, new List<Scene> { scene1 }, TriggerSource.Pedals);
         
            return newMovie;
        }
                       
        public static List<Command> Init()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoAllGoHome, 0), 
              new Command(ButtonActions.EyePopClosed, 0),

              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default, .5),

              new Command(RobotControls.Microphone_RaiseLower,ButtonActions.ServoMax,0),
              new Command(RobotControls.Microphone_RaiseLower,ButtonActions.ServoMin,3),
            //  new Command(ButtonActions.PlayFirst, 1.0),

            //  new Command(ButtonActions.SubCommands, RaiseMFRRotateLower(8, 95, 3), 1.0 ),

          //    new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateCamera, 1.0), // Slight turn right

              new Command(ButtonActions.DisableAllServos, 5),
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 6),
            });

            return commandList;
        }

        public static List<Command> NeedsSubstance()
        {
            var commandList = (new List<Command>
            {
              new Command(RobotControls.Microphone_RaiseLower,ButtonActions.ServoMax,0),
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 0.1),
           
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateCamera,RotateMe),0 ),
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(0,50), 0 ),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 20 , 1.0), // tilt brows up    
              new Command(RobotControls.BrowLeftBottomOpen,ButtonActions.ServoDelta, -90, 1.0),
              new Command(RobotControls.BrowRightBottomOpen,ButtonActions.ServoDelta, -90, 1.0),  
              
              new Command(ButtonActions.PlayFirst, 1.0),  // Needs Substance, Continue                              
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 3.0 ), // tilt brows down    
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(50,0), 3 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 ,3.0 ), // Brows slightly closed
         

              new Command(RobotControls.Microphone_RaiseLower,ButtonActions.ServoMin,4),
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateMe,RotateCamera),5.5 ),
              new Command(ButtonActions.DisableAllServos, 6),
            
            });

            return commandList;
        }      


        public static List<Command> Assuming()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateMe,RotateCamera), 0 ),
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(0,-50), 0 ),
              //So assuming you have a good copy of a headset, and it fits your face, what can cause differences in people’s perception?
              new Command(ButtonActions.PlayFirst, 0.25), 
              

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 50 , 0 ), // Open brows 50%
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(-50,0), 0 ),            
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -100, 1.0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50 ,1.5 ),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 20 ,3.0 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 100, 3.0),
              new Command(ButtonActions.SubCommands, TiltBackForthHome(), 4 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 5.0),                 
              
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,5.5 ),
              new Command(ButtonActions.DisableAllServos, 6),
            });

            return commandList;
        }

        public static List<Command> YouRemember()
        {
            var commandList = (new List<Command>
            {
              //You remember that? ( Eye Pop ) What is going on behind your eyes?  (eyes back)
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateCamera,RotateMe), 0 ),             
              new Command(ButtonActions.PlayFirst, 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 0 ), // Open brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, RotateCamera , .75 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 , 1.75 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, 100 , 1.75 ),
              new Command(ButtonActions.DisableAllServos,2.25),
     //         new Command(ButtonActions.EyePopOpen, 2.5 ),
              new Command(ButtonActions.RGBCommand, RGBLight.SetRGBColor(255,255,255, 150,RGBLight.Ring.Both,RGBLight.Side.LR), 2.5),
     //         new Command(ButtonActions.EyePopClosed, 7 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, 0 , 6.0 ),
              new Command(ButtonActions.DisableAllServos, 6.5),
              new Command(ButtonActions.RGBCommand, RGBLight.ClearAll(),8.0),
            });

            return commandList;
        }

        public static List<Command> CanYourEyesDoThis()
        {
            var commandList = (new List<Command>
            {
              //: Can your eyes do this?  ( color wheel in circles )
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateMe,RotateCamera), 0 ),
              new Command(ButtonActions.PlayFirst, 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 50, 0 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -10 , 1.0 ), // close brows 10%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, -30 , 1.0 ),
              new Command(ButtonActions.RGBCommand, RGBLight.RainbowCycle(200,RGBLight.Side.LR,5), 1.0),  //"RAINBOWCYCLE,200,LR,5"
              new Command(ButtonActions.DisableAllServos, 2),             
            });

            return commandList;
        }

        
        public static List<Command> DistortionProfile()
        {
            var commandList = (new List<Command>
            {
                //In order to map around all those things to see an image that looks sharp,
                //your brain would need a far more complex distortion profile than any used by a
                //VR headset and it would need to be specific to your eyes.
              new Command(ButtonActions.PlayFirst, 0),
            
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 0 ), // tilt brows down
                                                                                                  // 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 0 ), // close brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -50, 1.0), // look down 

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 1.5 ), // close brows 50%

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 2.0), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 2.5 ), // close brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50 ,3.0 ), // close iris
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 3.0), // look up a bit 

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 70, 4), // look right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 4.5 ), // open brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,5.0), // iris normal

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 6.0), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -50, 6.0), // tilt left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 6.5 ), // close brows 50%

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 70, 8.0), // look right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 8.5 ), // close brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50 ,9.0 ), // close iris

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 50 , 9.5 ), // tilt brows up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 50, 10.0), // tilt right

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 11.0), // look straight
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 9.5 ), // tilt brows level
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 80 , 12.0 ), // open flaps 80%                                                                         
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, -10 ,12.5), // Open iris
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 0, 13.0), // tilt level

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 14.0 ), // close brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 9.5 ), // tilt brows down 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,14.0), // Normal iris

              new Command(ButtonActions.DisableAllServos, 14.5),
            });

            return commandList;
        }

        public static List<Command> CorrectedImage()
        {
            var commandList = (new List<Command>
            {
              // So everyone doesn’t see the same corrected image?               
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -50, 0), // look down 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 0 ), // close flaps half way
              new Command(ButtonActions.PlayFirst, 0.5), // Plays Audio File
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 90, .350), // look right
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateMe, 0.5), // Slight turn right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, .750), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -90, 1.350), // look left
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, 20, 1.50), // Slight turn left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 90, 2.40), // look right
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateMe, 2.50), // Slight turn right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 3.5), // look level 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 2.65), // look right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 0 ), // open flaps up 
            });

            return commandList;
        }
        public static List<Command> Cilantro()
        {
            var commandList = (new List<Command>
            {
             new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default, 0),
              // But people taste things differently. Some people think Cilantro tastes like soap and others love it. 
              new Command(ButtonActions.PlayFirst, 0),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 80 , .25 ), // tilt brows up        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 80, 1.0),  // Look up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -30 , 2.0 ), // tilt brows down    
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 3.0),  // neck level                                                                                                // 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 3.5 ), // open flaps up 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 5.5 ), // open flaps up 
            });

            return commandList;
        }
               
        public static List<Command> YouSaid()
        {
            var commandList = (new List<Command>
            {
              //You said that what you “perceive” goes well beyond a corrected image .            
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateMe, 0), // Slight turn right
              new Command(ButtonActions.PlayFirst, 0.5),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 0.5 ), // tilt brows level 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 80 , 0.5 ), // open flaps up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 80, 1.0),  // Look up

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 2.5),  // Look level

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 3.0 ), // tilt brows down 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 3.0 ), // slightly close flaps

              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateCamera, 5.0), // Slight turn part left
              new Command(ButtonActions.DisableAllServos, 6),
            });

            return commandList;
        }

        public static List<Command> SmartProgramming()
        {
            var commandList = (new List<Command>
            {
              //So your brain learns what to care about or be scared of and makes sure you notice those things first
              //to protect yourself before letting you notice other things.
              //Smart programming.          

              //new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateMe, 0), // Slight turn right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 0 ), // open flaps up
              new Command(ButtonActions.PlayFirst, 0.5),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 70 , 0.5 ), // tilt brows up              
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 1.0),  // Look up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 0.5 ), // tilt brows down         
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 2.5),  // Look level

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 3.0 ), // tilt brows down 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 3.0 ), // slightly close flaps

              // Smart Programming
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 90 , 3.0 ), //  flaps open
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateCamera,3.0), // Slight turn part left
              new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateMe,4.5), // Slight turn part left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 4.5 ), //  flaps close                                                        // 
              new Command(ButtonActions.DisableAllServos, 5.5),
            });

            return commandList;
        }


        public static List<Command> FoolYou()
        {
            var commandList = (new List<Command>
            {
              // Your brains can fool you and show something that that doesn’t exist?
              new Command(ButtonActions.PlayFirst, 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 80 , 0 ), // tilt brows up        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 70, 1.0),  // tilt right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 2.0 ), // tilt brows down    
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 0, 3.0),  // neck level

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 40 , 3.5 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 5 ), 
                                                                                                 
            //  new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateCamera,RotateMe),0 ),
            //  new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateMe,RotateCamera),0 ),
              new Command(ButtonActions.DisableAllServos, 5.75),
            });

            return commandList;
        }

        public static List<Command> Theory()
        {
            var commandList = (new List<Command>
            {
              // That’s a lot of theory and good input,
              // but can you give us a real world example of something that impacts your experience in VR?
              new Command(ButtonActions.SubCommands, RotateNeckwithEyes(RotateCamera,RotateMe), 0 ),
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(0,-RotateMe), 0 ),
              new Command(ButtonActions.PlayFirst, 1),
               new Command(ButtonActions.SubCommands, TiltNeckwithEyes(-RotateMe,20), 2 ),
              // Nod head
             
               new Command(ButtonActions.SubCommands, TiltNeckwithEyes(0,-RotateMe), 4 ),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 40 , 5 ), // open flaps up
              //Open Eyes at the end
                
                 new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 6.0),
              // new Command(ButtonActions.SubCommands, TiltNeckwithEyes(-RotateMe,20), 6 ),


              //Then close them
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 7 ), // open flaps up
             new Command(ButtonActions.DisableAllServos,8),
            });

            return commandList;
        }

        public static List<Command> Hal9000()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.PlayFirst, 0),  // Let me get into character    
                 new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, RotateCamera, 0.2),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 70, 1.0),  // Look up
              // Closes right flaps, extends left eye halfway, fade left eye to glowing red 
              new Command(RobotControls.NoseBody,ButtonActions.ServoMin, 1.5),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -0 , 1.5 ), // tilt brows flat 

              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoValue, 1600 , 1.5 ),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin ,1.5 ),

              new Command(RobotControls.BrowLeftBottomOpen,ButtonActions.ServoMax, 2),
              new Command(RobotControls.BrowLeftTopOpen,ButtonActions.ServoValue, 1800, 2),
              new Command(RobotControls.LeftIris,ButtonActions.ServoDelta, -90, 2),
              new Command(ButtonActions.EyePopLeftHalfOpen, 2.5 ),
              new Command(ButtonActions.RGBCommand, RGBLight.Fade(255,0,0,200,RGBLight.Ring.Eyes,RGBLight.Side.Left, 5,RGBLight.FadeDirection.In, 5, 20), 3),
               new Command(ButtonActions.DisableAllServos, 3.5),
              new Command(ButtonActions.PlayNext, 4), // Talks with Hal 9000 voice
              // The 9000 series has a perfect operational record….
              //  ChatGPT Said
         
              // Nod head
              //“Growing up with chronic exposure to high-glare environments like snow and
              //ice can lead to long-term adaptation that reduces subjective glare sensitivity,
              //and that adaptation can carry over into VR.
              //However, it is not a guarantee, and it does not eliminate all glare-related issues in head-mounted displays.”            

              new Command(ButtonActions.RGBCommand, RGBLight.ClearAll(), 38.0), // turn off red glowing eye
               new Command(ButtonActions.EyePopClosed, 38 ),
              // return to normal look
              new Command(RobotControls.NoseBody,ButtonActions.ServoHome, 38.0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 38.5 ), // open flaps up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 , 38.5), // Normal iris             

              new Command(ButtonActions.PlayNext, 39), // Talks with J5 voice

           
              new Command(ButtonActions.SubCommands, TiltNeckwithEyes(0,-30), 40 ),
     
               new Command(ButtonActions.SubCommands, TiltNeckwithEyes(-30,20), 42 ),
              // Nod head
             
               new Command(ButtonActions.SubCommands, TiltNeckwithEyes(20,-30), 44 ),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 40 , 45 ), // open flaps up
              //Open Eyes at the end
                

               new Command(ButtonActions.SubCommands, TiltNeckwithEyes(-30, 0), 46 ),


              //Then close them
             new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 47 ), // open flaps up
             new Command(ButtonActions.DisableAllServos,49),
            });

            return commandList;
        }

        public static List<Command> PeopleLearnThis()
        {           

            var commandList = (new List<Command>
            {
              new Command(ButtonActions.PlayNext, 0),            

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 0.1 ), // tilt brows down                                                                                                
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 0 ), // close brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -50, 1.0), // look down 
             
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 2.25), // look left
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 2.5 ), // Flaps up.
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 2.5 ), // flaps down

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 4.0), // look up a bit 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 70, 4), // look right

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 4.5 ), // open brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,5.0), // iris normal
               
            //    new Command(ButtonActions.RGBCommand, RGBLight.SetRGBColor(0,255,0,100,RGBLight.Ring.Eyes,RGBLight.Side.LR), 5.5), 

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 6.0), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -50, 6.0), // tilt left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 6.5 ), // close brows 50%

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 70, 8.0), // look right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 50 , 8.25 ), // open flaps "Serious problems"
                                                                                                   // 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50 ,9.0 ), // close iris

              new Command(RobotControls.BrowLeftTopTilt, ButtonActions.MaestroSet, ServoSpeed.Fast, 8.5),
             new Command(RobotControls.BrowRightTopTilt, ButtonActions.MaestroSet, ServoSpeed.Fast, 8.5),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 9.5 ), // tilt brows up
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 50, 10.1), // tilt right

           //   new Command(ButtonActions.RGBCommand, RGBLight.SetRGBColor(255,0,0,100,RGBLight.Ring.Eyes,RGBLight.Side.LR), 10.5),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 11.0), // look straight
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 10 , 11.5 ), // tilt brows level
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 0, 12.0), // tilt level
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 80 , 12.2 ), // open flaps 80%                                                                         
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, -10 ,13), // Open iris
         
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 14.0 ), // close brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 14.25 ), // tilt brows down 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,14.0), // Normal iris

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 15), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 15.5 ), // close brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50 ,16 ), // close iris
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 16.5), // look up a bit 

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 , 17 ), // close brows 20%    

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 40 , 19 ), // close brows 20%
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, -20 ,19 ), // close iris

             //     new Command(ButtonActions.RGBCommand, RGBLight.SetRGBColor(0,0,255,100,RGBLight.Ring.Eyes,RGBLight.Side.LR), offset+3.5),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 70, 19.5), // look right
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 20 ), // open brows 20%        
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,20.5), // iris normal

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -70, 21.5), // look left
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -50, 22.0), // tilt down
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -50 , 23.0 ), // close brows 50%

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 23.0), // tilt down
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 23.0 ),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 23.5), 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 60 , 24 ),   
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, -20 ,24 ), // close iris
                new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateCamera, 24 ), // Slight turn right     
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,25 ), // open flaps 80%
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 25 ), // tilt brows level                                                                                     // 
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 10, 25 ), // look level
                    

              new Command(ButtonActions.DisableAllServos, 25),

            });

            return commandList;
        }

        public static List<Command> WhyArgue()
        { 
           var commandList = (new List<Command>
            {
               new Command(ButtonActions.PlayNext,0),
              
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 80 , 0 ), // open flaps 80%         
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 50, .5), // look up
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -30, 1.5), // look down
                   new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 40 ,2.0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -30 , 2.0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -40 , 2.0), // tilt brows level                                                                                     // 
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 10, 2.5), // look level
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0 ,2 ), // open flaps 80% 

               new Command(ButtonActions.DisableAllServos, 5.5),
              
            });

            return commandList;
        }
        public static List<Command> GoodthingsHomework()
        {
            var commandList = (new List<Command>
            {
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 40, 0), // look up
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 30 , 0 ), // open flaps 80%
               new Command(ButtonActions.PlayNext, 0.5),

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 30, 2), // tilt right

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -20, 2),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -30 , 2 ), 
               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, RotateMe, 2.5),
               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, RotateCamera, 3.5),

               new Command(RobotControls.MFR_Rotate, ButtonActions.MaestroSet, ServoSpeed.Slow, 3.0),
             //  new Command(ButtonActions.SubCommands, RaiseMFRRotateLower(8, 75, 4), 4.0 ),

               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMin , 4),  // raise
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,0, 4),
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,80, 4.5),
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,60, 4.5),

               new Command(ButtonActions.RGBCommand, RGBLight.Fade(0,0, 255,200,RGBLight.Ring.Eyes,RGBLight.Side.LR, 5,RGBLight.FadeDirection.In, 5, 20), 4.0),

               new Command(RobotControls.LeftLensHorizontal, ButtonActions.MaestroSet, ServoSpeed.Fast, 4.0),
               new Command(RobotControls.RightLensHorizontal, ButtonActions.MaestroSet, ServoSpeed.Fast, 4.0),

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 50, 4.5),

                 new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,40, 5),

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -50, 6.5),
                  new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 50, 8.5),

               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,60, 9),            
                 
               //new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 50 , 4.5),
               //new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 6.5),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 , 8.0),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -30 , 9.0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 9.5),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -40, 9.0),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 10.0),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20 , 10.5),
                 new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,30, 10),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -50, 10.5),
              // new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -0, 11.0),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 , 11.5),
              // new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -30, 12.0),
                 new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,50, 12),
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta ,0, 16.5),
               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMax , 17),
               new Command(ButtonActions.DisableAllServos, 17.5),
               new Command(ButtonActions.RGBCommand, RGBLight.ClearAll(), 18),
            });

            return commandList;
        }

      
          
        public static List<Command> Sleeping()
        {
            var commandList = (new List<Command>
            {
               new Command(RobotControls.NeckTurn,ButtonActions.ServoDelta, RotateCamera, 0), // Slight turn right
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -50, 1),  // Look down
               new Command(ButtonActions.SubCommands, CloseFlaps(), 2 ),  // Close Flaps
               new Command(ButtonActions.DisableAllServos, 4),

              // new Command(ButtonActions.RepeatCommands, 5, Snoring(100, 1.5), 10, 3 ), 
              
            });

            return commandList;
        }

        public static List<Command> Snoring(int howfar, double delay)
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, howfar, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, 0, delay)

                // Vents breathing
                // play snoring
                // little top flap tilt motions.
                // Antenna motions                          
            });

            return commandList;
        }

      

        public static List<Command> WakeUp()
        {
            var commandList = (new List<Command>
            {
               
               new Command(RobotControls.Microphone_RaiseLower, ButtonActions.ServoMax, 0), // Raise mic
               new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 2),
               new Command(ButtonActions.SubCommands, OpenEyesWakingUp(), 5 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 10, 7),  // Look Up
               new Command(RobotControls.Microphone_RaiseLower, ButtonActions.ServoMin, 8), // Raise mic
               new Command(ButtonActions.DisableAllServos, 9),
            });

            return commandList;
        }

        public static List<Command> NeckTests()
        {
            var commandList = (new List<Command>
            {
                 new Command(ButtonActions.RepeatCommands, 0, SideToSideTilt(30, 1.5), 2, 3 ), // 2 fast side to side 20% shallow tilts 1 sec delay                                                                                             // 
                 new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 6),  // Look level                                                                   
              
                 new Command(ButtonActions.RepeatCommands, 0, Nod(40,1.25), 3, 2.5 ),   // Nod (20% 1sec delay) repeat twice 2 sec delay, 
               
                 new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 10),  // Look level        
                                                                                                 
                 new Command(ButtonActions.DisableAllServos, 12),                 
            });
            return commandList;
        }
        public static List<Command> EyeTests()
        {
            var commandList = (new List<Command>
            {
                 new Command(ButtonActions.SubCommands, EyeRole(2), 0 ), // Eyes look up then back down 2s later
               
                 new Command(ButtonActions.RepeatCommands, 4, EyesCircle4(90, 2), 2, 3 ), // eyes go into a big circle. repeat twice
                
                 new Command(ButtonActions.ServoAllGoHome, 11), // Center everything
                 new Command(ButtonActions.DisableAllServos, 12.25),
            });
            return commandList;
        }

        public static List<Command> TopTest()
        {
            var commandList = (new List<Command>
            {
                 new Command(ButtonActions.SubCommands, RaiseMFRRotateLower(3, 75, 1.5), 0 ), // MFR Up - rotate 3 times +/- 50% - MFR down

                 new Command(ButtonActions.SubCommands, RaiseWhipRotate(3, 50, 1.5), 7 ),  // Whip Up - rotate 3 times +/- 50% - Whip down
                  
                 new Command(RobotControls.Microphone_RaiseLower, ButtonActions.ServoMax, 13), // Raise mic
                 new Command(RobotControls.Microphone_RaiseLower, ButtonActions.ServoMin, 16), // Lower mic             
               
                 new Command(ButtonActions.DisableAllServos, 18),              
            }); 
            return commandList;
        }
        public static List<Command> ExpressionsTest()
        {
            var commandList = (new List<Command>
            {
              //  new Command(ButtonActions.SubCommands, 0, What() ),   // right flap angled, iris partly closed
                                                                      // 
               // new Command(ButtonActions.SubCommands, 5, Awww() ), // slightly close brows tilt top brows down slightly
              
                new Command(ButtonActions.SubCommands, WinkRightEye(), 0 ),
                

                //new Command(ButtonActions.SubCommands, 14, AngryFace() ),

                //new Command(ButtonActions.SubCommands, 24, VeryAngryFace() ),
            });
            return commandList;
        }



        public static List<Command> GangedServoDeltaTest()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 20 , 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 20 ,1),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 20 , 2),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 20 , 3),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 4),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20 , 5),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 20 , 6),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, 20 , 7),
            });

            return commandList;
        }

    
        public static List<Command> SideToSideTilt(int howfar, double delay)
        {
            var commandList = (new List<Command> 
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -howfar, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, howfar, delay)
            });

            return commandList;
        }
          
        public static List<Command> Nod(int howfar, double delay)
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, howfar, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -howfar, delay),
              
            });

            return commandList;
        }
           

        public static List<Command> ChildNestedEyes()
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 20, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -20, 1.0)
            });

            return commandList;
        }

        public static List<Command> EyeRole(double delay)
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -90, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 0, delay)
            });

            return commandList;
        }
        public static List<Command> EyesCircle4(int howmuch, double delay)
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, howmuch, 0),   // up
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight,howmuch,0), // right
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -howmuch, delay), // right down
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight,-howmuch,delay*2.0), // left down
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, howmuch, delay*3.0), // left up
            });

            return commandList;
        }

        public static List<Command> CloseFlaps()
        {
            var commandList = (new List<Command>
            {
                // Nose Body down so flaps close more closely.
                new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue,0),
                // Tilt level
                new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,0),
                new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHome ,0),           
                // Brow Top closed
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax ,0 ),
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax ,0 ),
                // Bottom brows closed
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin ,0 ),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin ,0),
             });

            return commandList;
        }

        //   new Command(ButtonActions.RGBCommand , "Fade,255,255,255,RotateMe,eyes,lr,90,in,1,0", 1500),

        public static List<Command> OpenEyes()
        {
            var commandList = new List<Command>
            {
                // Nose in higher home position
                new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 0),
                //Tilt level
                new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,0),
                new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHome ,0),           
                // Brow Top open
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,0 ),
                new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome ,0 ),
                // Bottom brows open
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,0 ),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,0),
            };

            return commandList;
        }

       
        
        public static List<Command> WinkRightEye()
        {
            var commandList = (new List<Command>
            { 
                // Nose Body down so flaps close more closely.
                new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue,0.01),                
                // Tilt level
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 ,0.01),
                 new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0 ,0.01 ),

                new Command(RobotControls.BrowRightTopOpen, ButtonActions.MaestroSet, ServoSpeed.Fast, 0.1),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.MaestroSet, ServoSpeed.Fast, 0.1),

                // Brow Top closed
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax ,0.5 ),               
                // Bottom brows closed              
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin ,0.5),
                 // Brow Top open
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,1.50 ),               
                // Bottom brows open         
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,1.50),

                new Command(RobotControls.BrowRightTopOpen, ButtonActions.MaestroSet, ServoSpeed.Default, 1.75),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.MaestroSet, ServoSpeed.Default, 1.75),
             });

            return commandList;
        }

        public static List<Command> WinkLeftEye()
        {
            var commandList = (new List<Command>
            {
                // Nose Body down so flaps close more closely.
                new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue,0),
                // Tilt level
                new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,0),
                new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHome ,0),           
                // Brow Top closed
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax ,0 ),               
                // Bottom brows closed              
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin ,0),
                 // Brow Top open
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1.0 ),               
                // Bottom brows open         
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,1.0),
             });

            return commandList;
        }

        /// <summary>
        /// Open Eyes
        /// Squint
        /// Look left then right then center and relax Iris
        /// </summary>
        /// <returns></returns>
        public static List<Command> OpenEyesWakingUp()
        {
            var commandList = new List<Command>
            {             
                // Open Eye flaps to Home
                new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0, 0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 0, 0),                  

                // close Iris after open after opening flaps
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 100, 2.0),            
                // Look Down and left
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -100, 2.0),  
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -100, 2.0),

                // Look Right
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 100, 2.5),             

                // Center Eyes and return Iris to normal
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 3.0),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 0, 3.0),                            
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0, 3.0),
            };

            return commandList;
        }

       

       /// <summary>
       /// Rotate Neck with eyes moving
       /// </summary>
       /// <param name="start">rotate start position</param>
       /// <param name="end">rotate end position</param>
       /// <returns></returns>
        public static List<Command> RotateNeckwithEyes(int start, int end)
        {
            var move = end - start;

            var eye = move * 2;

            if( eye > 100) eye = 100;
            if( eye < -100) eye = -100;

            double timeout = (double)Math.Abs(move) / (double)30;

            var commandList = new List<Command>
            {
              new Command(RobotControls.LeftLensHorizontal, ButtonActions.MaestroSet,ServoSpeed.Slow, 0),
              new Command(RobotControls.RightLensHorizontal, ButtonActions.MaestroSet,ServoSpeed.Slow, 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, eye, 0.1),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, end, 0.2), 
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, timeout),
             };

            return commandList;
        }
        /// <summary>
        /// Tilt Neck with eyes moving
        /// </summary>
        /// <param name="start">rotate start position</param>
        /// <param name="end">rotate end position</param>
        /// <returns></returns>
        public static List<Command> TiltNeckwithEyes(int start, int end)
        {
            var move = end - start;

            var eye = move;

            if (eye > 100) eye = 100;
            if (eye < -100) eye = -100;

            double timeout = (double)Math.Abs(move)/(double)100;

            var commandList = new List<Command>
            {
              new Command(RobotControls.LeftLensVertical, ButtonActions.MaestroSet,ServoSpeed.Slow, 0),
              new Command(RobotControls.RightLensVertical, ButtonActions.MaestroSet,ServoSpeed.Slow, 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, eye, 0.1),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, end, 0),             
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 0, timeout),
             };

            return commandList;
        }
        public static List<Command> ShakeHead()
        {
            var commandList = new List<Command>
            {
               // Shakes head back and forth
               new Command(RobotControls.NeckTurn, ButtonActions.ServoHome, 0 ),  

               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, 40, .500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoHome, 1.0 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, 40, 1.500 ),

            };
            return commandList;
        }

        public static List<Command> TiltBackForthHome()
        {
            var commandList = new List<Command>
            {
               // Shakes head back and forth
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 30, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, RotateMe, 0),               
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -RotateMe, 0),

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -30, 1.0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, RotateMe, 1.0),

               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 0, 2.0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 0, 2.0),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 2.0),
            };
            return commandList;
        }
      

        public static List<Command> Nod3Times()
        {
            var commandList = new List<Command>
            {
               // Nod once Fast
               new Command(ButtonActions.SubCommands, CL.NodQuick(), 1.0),
               // Nod Larger 3 times Slow
               new Command(ButtonActions.RepeatCommands, 3.0, CL.NodLong(), 3, 2.0),
               
               // Move Neck back to Home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 12.0),
            };
            return commandList;
        }

        public static List<Command> NodQuick()
        {
            var commandList = new List<Command>
            {
               // Nods Up, Down back to home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -20, .500 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 1.000 ),
            };
            return commandList;
        }

        public static List<Command> NodLong()
        {
            var commandList = new List<Command>
            {
               // Nods Up higher and lower at a slower pace, doesn't return home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 40, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -40, 1.0 ),             
            };
            return commandList;
        }

        /// <summary>
        /// Tilt brows down a little
        /// </summary>
        /// <returns></returns>
        public static List<Command> Awww()
        {
            var commandList = new List<Command>
            {
               // Nose in higher home position
               new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 0), 
               // Brows slightly closed
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 ,0 ),
               // Top Flaps Tilt down slightly
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, -20, 0 ),              
            };
            return commandList;
        }

        public static List<Command> AngryFace()
        {
            var commandList = new List<Command>
            {
            // 30 seconds Angry Face
            new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 50, 0 ),
            // Irises half closed
            new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50, 0),
         
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax , 0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax , 0),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 30.0),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin , 30.0),

            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoMax , 30.0),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMax , 30.0),
            // Fadein to red over a couple seconds           
            new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 32.0),

            };
            return commandList;
        }
        public static List<Command> VeryAngryFace()
        {
            var commandList = new List<Command>
            {
            // 30 seconds Angry Face
            new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 50, 0 ),
            // Irises half closed
            new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 50, 0),
         
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax , 0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax , 0),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 30.0),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin , 30.0),

            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoMax , 30.0),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMax , 30.0),
            // Fadein to red over a couple seconds           
            new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 32.0),

            };
            return commandList;
        }

        /// <summary>
        ///  Whip antenna pop up wiggle back and forth and drop
        /// </summary>
        /// <returns></returns>
        public static List<Command> RaiseWhipRotate(int repeat, int howmuch, double delay)
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.Whip_Antenna_RaiseLower, ButtonActions.ServoMax , 0), //  Up             
               new Command(ButtonActions.RepeatCommands, .75, WhipRotate(howmuch), repeat, delay ), // Rotate repeat times               
               new Command(RobotControls.Whip_Antenna_Rotate, ButtonActions.ServoDelta , 0, .75 + repeat*delay), // center
               new Command(RobotControls.Whip_Antenna_RaiseLower, ButtonActions.ServoMin ,  1 + repeat*delay),  // Down
            };
            return commandList;
        }
        public static List<Command> WhipRotate(int howmuch)
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.Whip_Antenna_Rotate, ButtonActions.ServoDelta , -howmuch, 0), 
               new Command(RobotControls.Whip_Antenna_Rotate, ButtonActions.ServoDelta , howmuch, .75),            
            };
            return commandList;
        }


        public static List<Command> RaiseMFRRotateLower(int repeat, int howmuch, double delay)
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMin , 0),  // raise
               new Command(ButtonActions.SubCommands,  MFRRotate(howmuch, delay), 0),
             
               // Rotate Left Right 3 times
               new Command(ButtonActions.RepeatCommands, 0, MFRRotate(howmuch, delay), repeat, delay), // rotate 3 times

               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoHome , 1.25 + repeat*delay),   // goes to center           
               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMax,  1.5 + repeat*delay), // drop
            };
            return commandList;
        }
        public static List<Command> MFRRotate(int howmuch, double delay)
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta , -howmuch, 0),
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoDelta , howmuch, delay/2),
            };
            return commandList;
        }
    }
}
