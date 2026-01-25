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

        public static Movie CreateUnitTests()
        {
            var scene1 = new Scene("Scene1");
         
            scene1.Sequences = new List<Sequence> 
            {
                new Sequence(CL.Init(), 0),
                new Sequence(CL.ServoDeltaTest(), 0),     
                new Sequence(CL.GangedServoDeltaTest(), 0),
                new Sequence(CL.RepeatTest(), 0),
                new Sequence(CL.NestedCommandTest(), 0),
                new Sequence(CL.NestedRepeatCommandTest(), 0)
            };

            var movieName = "UnitTests";
            var newMovie = new Movie(movieName, AppContext.BaseDirectory + "MOVIES\\" + movieName, new List<Scene> { scene1 }, TriggerSource.Pedals);
         
            return newMovie;
        }
                       
        public static List<Command> Init()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoAllGoHome, 0),
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 500),
              new Command(RobotControls.NeckTurn,ButtonActions.MaestroSet, ServoSpeed.Default, 750),   
            });

            return commandList;
        }

        public static List<Command> ServoDeltaTest()
        {
            var commandList = (new List<Command>
            {
                // 20% Right of Center 1000ms delay
                new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, 20, 1000 ),
            });

            return commandList;
        }

        public static List<Command> GangedServoDeltaTest()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 20 , 0),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 20 ,1000),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 20 , 2000),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 20 , 3000),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, 20 , 4000),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20 , 5000),
              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 20 , 6000),

              new Command(ButtonActions.ServoGangedDelta, GangedServoNames.VentsOpen, 20 , 7000),
            });

            return commandList;
        }

        public static List<Command> RepeatTest()
        {
            var commandList = (new List<Command>
            {
              // wait 1 seconds Repeat Side to Side Tilt 3 times 1000 second apart
              new Command(ButtonActions.RepeatCommands, 1000, SideToSideTilt(), 3, 1000 ),
            });

            return commandList;
        }
        public static List<Command> SideToSideTilt()
        {
            var commandList = (new List<Command> 
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -20, 1000),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 20, 2000)
            });

            return commandList;
        }

        public static List<Command> NestedCommandTest()
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.SubCommands, 1000, Nod() ),
            });

            return commandList;
        }

        public static List<Command> NestedRepeatCommandTest()
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.SubCommands, 1000, Nod() ),
            });

            return commandList;
        }

        public static List<Command> Nod()
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 1000),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -20, 2000),
                new Command(ButtonActions.SubCommands, 3000, ChildNestedEyes() ),
            });

            return commandList;
        }
        public static List<Command> ChildNestedEyes()
        {
            var commandList = (new List<Command>
            {
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 20, 1000),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -20, 2000)
            });

            return commandList;
        }


        public static List<Command> CloseEyes()
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

        //   new Command(ButtonActions.RGBCommand , "Fade,255,255,255,60,eyes,lr,90,in,1,0", 1500),

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

        /// <summary>
        /// One brow up  Flaps slightly closed
        /// </summary>
        /// <returns></returns>
        public static List<Command> What()
        {
            var commandList = new List<Command>
            {
                // Nose in higher home position
                new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 0),
                
                // Left Tilt level
                new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,0),

                // Right Tilt Angled up
                new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHomeDelta, 40, 0),           
               
                  // Brows slightly closed
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapsOpen, -20 ,0 ),              
            };

            return commandList;
        }
        
        public static List<Command> WinkRightEye()
        {
            var commandList = (new List<Command>
            {
                // Nose Body down so flaps close more closely.
                new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue,0),
                // Tilt level
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.FlapTiltUp, 0 ,0 ),                     
                // Brow Top closed
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax ,0 ),               
                // Bottom brows closed              
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin ,0),
                 // Brow Top open
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,1000 ),               
                // Bottom brows open         
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,1000),
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
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1000 ),               
                // Bottom brows open         
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,1000),
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
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 100, 2000),            
                // Look Down and left
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, -100, 2000),  
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, -100, 2000),

                // Look Right
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 100, 2500),             

                // Center Eyes and return Iris to normal
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesHorizontalRight, 0, 3000),
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.EyesVerticalUp, 0, 3000),                            
                new Command(ButtonActions.ServoGangedDelta, GangedServoNames.IrisClose, 0, 3000),
            };

            return commandList;
        }

        public static List<Command> ShakeHead()
        {
            var commandList = new List<Command>
            {
               // Shakes head back and forth
               new Command(RobotControls.NeckTurn, ButtonActions.ServoHome, 0 ),  

               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, 40, 500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoHome, 1000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoDelta, 40, 1500 ),

            };
            return commandList;
        }

        public static List<Command> TiltBackForthHome()
        {
            var commandList = new List<Command>
            {
               // Shakes head back and forth
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 20, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, -20, 1000 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckTiltRight, 0, 2000 ),
            };
            return commandList;
        }
      

        public static List<Command> Nod3Times()
        {
            var commandList = new List<Command>
            {
               // Nod once Fast
               new Command(ButtonActions.SubCommands, 1000, CL.NodQuick()),
               // Nod Larger 3 times Slow
               new Command(ButtonActions.RepeatCommands, 3000, CL.NodLong(), 3, 2000),
               
               // Move Neck back to Home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 12000),
            };
            return commandList;
        }

        public static List<Command> NodQuick()
        {
            var commandList = new List<Command>
            {
               // Nods Up, Down back to home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 20, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -20, 500 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 0, 1000 ),
            };
            return commandList;
        }

        public static List<Command> NodLong()
        {
            var commandList = new List<Command>
            {
               // Nods Up higher and lower at a slower pace, doesn't return home
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, 40, 0 ),
               new Command(ButtonActions.ServoGangedDelta, GangedServoNames.NeckNodUp, -40, 1000 ),             
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
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 30000),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin , 30000),

            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoMax , 30000),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMax , 30000),
            // Fadein to red over a couple seconds           
            new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 32000),

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
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 30000),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin , 30000),

            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoMax , 30000),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMax , 30000),
            // Fadein to red over a couple seconds           
            new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 32000),

            };
            return commandList;
        }

        /// <summary>
        ///  Whip antenna pop up wiggle back and forth and drop
        /// </summary>
        /// <returns></returns>
        public static List<Command> RaiseWhipRotate()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.Whip_Antenna_RaiseLower, ButtonActions.ServoMax , 1500),

               // Rotate Left Right 3 times
               new Command(ButtonActions.RepeatCommands, 1500, WhipRotate(), 3, 1000 ),              

               new Command(RobotControls.Whip_Antenna_RaiseLower, ButtonActions.ServoMin ,  5000),

            };
            return commandList;
        }

        public static List<Command> WhipRotate()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.Whip_Antenna_Rotate, ButtonActions.ServoHomeDelta , -50, 0), 
               new Command(RobotControls.Whip_Antenna_Rotate, ButtonActions.ServoHomeDelta , 50, 1000),            
            };
            return commandList;
        }


        public static List<Command> RaiseMFRRotate()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMax , 1500),

               // Rotate Left Right 3 times
               new Command(ButtonActions.RepeatCommands, 1500, MFRRotate(), 3, 1000 ),

               new Command(RobotControls.MFR_UpDown, ButtonActions.ServoMin ,  5000),

            };
            return commandList;
        }

        public static List<Command> MFRRotate()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoHomeDelta , -50, 0),
               new Command(RobotControls.MFR_Rotate, ButtonActions.ServoHomeDelta , 50, 1000),
            };
            return commandList;
        }
    }
}
