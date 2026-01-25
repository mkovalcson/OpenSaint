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

        public static List<Command> Hi()
        {
            var commandList = (new List<Command>
            {
             // Turn to camera
              new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoMax , 0),
              new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoMax , 0),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 100 ),

              new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome,  1000),
              new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 1000),

              new Command(RobotControls.LeftIris, ButtonActions.ServoHomeDelta, 220, 1100 ),
              new Command(RobotControls.RightIris, ButtonActions.ServoHomeDelta, 200, 1100 ),

              new Command(RobotControls.RightLensHorizontal,ButtonActions.DisableServo, 1100),
              new Command(RobotControls.RightIris,ButtonActions.DisableServo, 1100),

              new Command(ButtonActions.PlayFirst, 1300),

              // tilt left
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 2300 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 2300 ),

              // tilt right
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 4100 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1600, 4100 ),

              // tilt flat
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 5500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 5500 ),
               
            });

            return commandList;
        }

        public static List<Command> Deep()
        {
            var commandList = (new List<Command>
            {
              // tilt down
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1417, 0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1597, 0 ),

              // awww
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 200),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 200),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -400,200),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 400,200),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 200 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 200),

              new Command(ButtonActions.PlayNext, 500),

               // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 2500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 2500 ),

                 new Command(ButtonActions.DisableAllServos, 4000),
            });

            return commandList;
        }
        public static List<Command> Great()
        {
            var commandList = (new List<Command>
            {
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 0),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1417, 0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1597, 0 ),

              new Command(ButtonActions.PlayNext, 750),

            
                // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 1000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 1000 ),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 1500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 1500 ),

                 new Command(ButtonActions.DisableAllServos, 3000),
            });

            return commandList;
        }

        public static List<Command> Causes()
        {
            var commandList = (new List<Command>
            {
                // Thinker pose

                // Close Irises a bit
                new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 0),
                new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 0),

                // Tilt neck up left
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoValue,  1603 , 0), // Right down half way
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoValue,  1517 , 0), // Left up half way

                // Gaze top left
                new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoMax , 0),
                new Command(RobotControls.LeftLensVertical,ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensVertical,  ButtonActions.ServoMin , 0),

                new Command(RobotControls.RightLensHorizontal,ButtonActions.DisableServo, 500),
                new Command(RobotControls.RightIris,ButtonActions.DisableServo, 500),

                // brows normal
                new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 0),

                   new Command(ButtonActions.DisableAllServos, 750),
                // will only disable the servos moved in this Action after the delay in ms
              //  new Command( ButtonActions.DisableAllRunningServos , 500 , 0),

                // low blue
                new Command( ButtonActions.RGBCommand , "Fade,0,0,255,30,eyes,lr,90,IN,1,0" , 0),

                // Causes of frustration?
                new Command(ButtonActions.PlayNext, 1500),
             
                //  return to normal.
            new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default,4500),
            new Command(RobotControls.NeckTiltRight,ButtonActions.ServoHome  ,4500),
            new Command(RobotControls.NeckTiltLeft,ButtonActions.ServoHome , 4500),
            new Command(RobotControls.LeftIris, ButtonActions.ServoHome, 4500),
            new Command(RobotControls.RightIris, ButtonActions.ServoHome, 4500),
            new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome ,4500),
            new Command(RobotControls.LeftLensVertical, ButtonActions.ServoHome ,4500),
            new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoHome ,4500),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome ,4500),

            new Command(RobotControls.RightLensHorizontal,ButtonActions.DisableServo, 5000),
            new Command(RobotControls.RightIris,ButtonActions.DisableServo, 5000),

            // low blue
            new Command(ButtonActions.RGBCommand , "Clear,eyes,lr", 4500),

             new Command(ButtonActions.DisableAllServos, 5250),

            });
            return commandList;
        }

        public static List<Command> Frustrations()
        {
            var oneoffset = 13000;
          
            var commandList = (new List<Command>
            {
               new Command(ButtonActions.PlayNext, 0),

               // Sure...

               // Open flaps
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMin , 0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMin ,0),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMax , 0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoMax , 0),

            // return to normal
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome , 1000),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1000),
           
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 1000),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 1000),


              // tilt left First frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 2100 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 2100 ),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, 200 , 3500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, -200 , 3500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 4500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 4500),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300,5000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,5000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 5000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 5000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , 100, 6500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,-100, 6500),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, -200 , 7500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, 200 , 7500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 8500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 8500),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 10000),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 10000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11000),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11000),

              // tilt right 2nd frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 12000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 12000 ),


              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, 200 , 3500+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, -200 , 3500+13000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,3500+13000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,3500+13000),

              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 3500+13000),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 3500+13000),


              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500+13000),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, -200 , 7500+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, 200 , 7500+13000),


              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 8000+13000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 8000+13000 ),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500+13000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11000+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11000+13000),
              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11000+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11000+13000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 11000+13000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,11000+13000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,  11000+13000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome , 11000+13000),

              // 4th frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 21000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 21000 ),

            new Command(RobotControls.BrowLeftTopTilt, ButtonActions.MaestroSet, ServoSpeed.Slow, 25000),
            new Command(RobotControls.BrowRightTopTilt, ButtonActions.MaestroSet, ServoSpeed.Slow, 25000),

            new Command(RobotControls.NoseBody,  ButtonActions.ServoModeValue, 29500),
             // 30 seconds Angry Face
            new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue, 1527 , 30000),
            new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoValue, 1295 , 30000),
            // Irises half closed
            new Command(RobotControls.LeftIris, ButtonActions.ServoMax , 30000),
            new Command(RobotControls.RightIris, ButtonActions.ServoMax , 30000),
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax , 30000),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax ,30000),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 30000),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoMin , 30000),
            new Command(RobotControls.LeftEyeVent,  ButtonActions.ServoMax , 30000),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMax , 30000),
            // Fadein to red over a couple seconds           
             new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 32000),

            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoHome, 42000),
            new Command(RobotControls.RightEyeVent,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.LeftIris,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.RightIris,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome , 42000),

            new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome , 42000),

            new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 42000),

            new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.LeftLensVertical,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 42000),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 42000),
             new Command(RobotControls.NoseBody,  ButtonActions.ServoHome, 42000),
            new Command( ButtonActions.RGBCommand , "ClearAll" , 42000),

                new Command(ButtonActions.DisableAllServos, 4400),
            });

            return commandList;
        }

        public static List<Command> DontUnderstand()
        {
            var commandList = new List<Command>
            {
               // Shakes head back and forth
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),

               new Command(ButtonActions.PlayNext, 250),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 1000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 1500 ),

            };
            return commandList;
        }

        public static List<Command> OK()
        {
            var commandList = new List<Command>
            {
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMin , 0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMin ,0),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMax , 0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoMax , 0),

              new Command(ButtonActions.PlayNext, 0),

            // return to normal
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome , 1000),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1000),

            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 1000),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 1000),          
              

            };
            return commandList;
        }

        public static List<Command> LotsInput()
        {
            var commandList = new List<Command>
            {
                // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 0 ),

               new Command(ButtonActions.PlayNext, 0),

                  // tilt down
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 1000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 1000 ),
            };
            return commandList;
        }

        public static List<Command> NoShapesSizes()
        {
            var commandList = new List<Command>
            {
                // Shakes head back and forth saying No
               // TODO: add gaze left/right in opposite direction of neck move.
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),

               new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 100),
               new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 100),

               new Command(ButtonActions.PlayNext, 250),             

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 750 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 1000 ),

               new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 1250),
               new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 1250),

                // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 1500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 1500 ),          

                  // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 2500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 2500),

                 // tilt left
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 4000),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 4000),

              // tilt right
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 5500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 5500 ),

                    // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 7000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 7000),

                 // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 8500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 8500 ),          

                  // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 10000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 10000),

               new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 10000 ),
               new Command(RobotControls.RightIris,  ButtonActions.ServoHome, 10000 ),

               new Command(ButtonActions.DisableAllServos, 11000),
            };
            return commandList;
        }

        public static List<Command> NotLikely()
        {
            var commandList = new List<Command>
            {
                  // awww
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 0),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),               

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 250 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 750 ),

               new Command(ButtonActions.PlayNext, 1000),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 2500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 8000),

               new Command(ButtonActions.DisableAllServos, 1000),
            };
            return commandList;
        }
        public static List<Command> WatchedYourVideos()
        {
            var commandList = new List<Command>
            {
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451,0),
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome,  0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome,  0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome, 0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,  0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome ,  0),
              
               new Command(ButtonActions.PlayNext, 150),

                 new Command(ButtonActions.DisableAllServos, 1500),
            };
            return commandList;
        }
        public static List<Command> OhBlameYou()
        {
            var commandList = new List<Command>
            {
                     // awww
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 0),           

               new Command(ButtonActions.PlayNext, 500),

               new Command(ButtonActions.DisableAllServos, 1500),
            };
            return commandList;
        }

        public static List<Command> SoYouAreSaying()
        {
            var commandList = new List<Command>
            {

              new Command(ButtonActions.PlayNext, 0),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 2100 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 2100 ),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, 200 , 3500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, -200 , 3500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 4500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 4500),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300,5000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,5000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 5000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 5000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5500),


              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 6000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 6000 ),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , 100, 6500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,-100, 6500),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, -200 , 7500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, 200 , 7500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 8500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 8500),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 10000),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 10000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11000),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11000),

                new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 12000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 12000 ),

                new Command(ButtonActions.DisableAllServos, 14500),
            };
            return commandList;
        }

        public static List<Command> PeopleDifferentExperiences()
        {
            var commandList = new List<Command>
            {
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome,  0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome,  0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome, 0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,  0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome ,  0),

               new Command(ButtonActions.PlayNext, 500),
            };
            return commandList;
        }

        public static List<Command> AwwwUnhappyPeople()
        {
            var commandList = new List<Command>
            {
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 0),

               new Command(ButtonActions.PlayNext, 500),
            };
            return commandList;
        }
        public static List<Command> VRMarket()
        {
            var commandList = new List<Command>
            {
               new Command(ButtonActions.PlayNext, 0),
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 1000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 1000 ),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 2000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 2000 ),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 3000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 3000 ),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300, 4000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,4000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 4000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 4000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 4000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,4000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 4000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 4000),

                  new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoHome, 4000),
              new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoHome, 4000),

            };
            return commandList;
        }
        public static List<Command> MarkValve()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),
               new Command(ButtonActions.PlayNext, 1000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300, 1000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,1000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 1000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 1000),


             new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 6000 ),


              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 6500),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,6500),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 6500 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 6500),

               // tilt up
               new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 7000),
               new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 7000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1500, 8000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1400, 10000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1500, 12000 ), 

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1400, 14000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 16000 ),
              // tilt straight
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 17000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 17000 ),
            };
            return commandList;
        }

        public static List<Command> ValveInsideoutTracking()
        {
            var commandList = new List<Command>
            {
               new Command(ButtonActions.PlayNext, 0),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300, 1000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,1000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 1000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 1000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 2000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 2000),


               new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 3000),
               new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 3000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 4000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 4000),

              new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 5000 ),
              new Command(RobotControls.RightIris,  ButtonActions.ServoHome,5000 ),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 6000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 6000),

          

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 6000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 6000),

            };
            return commandList;
        }
        public static List<Command> ooooDFWireless()
        {
            var commandList = new List<Command>
            {
             

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome , 0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome , 0),
              new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue, 0),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , 300, 250),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , -300,250),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , -300, 250 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , 300, 250),

              new Command(ButtonActions.PlayNext,500),         

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1417, 1000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1597, 1000),

                //Whip antenna pop up wiggle back and forth and drop
               new Command(RobotControls.Whip_Antenna_RaiseLower,  ButtonActions.ServoMax , 1500),
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1000, 1500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1800, 2500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1000, 3500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1800, 4500), //1400 Center
               new Command(RobotControls.Whip_Antenna_RaiseLower,  ButtonActions.ServoMin ,  5000),


              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 4500),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,4500),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 4500),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 4500),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 4500),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 4500),
              new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 4500),

                new Command(ButtonActions.DisableAllServos, 6500),
            };
            return commandList;
        }

        public static List<Command> WirelessWithoutComplaints()
        {
            var commandList = new List<Command>
            {
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),
               new Command(ButtonActions.PlayNext, 500),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 2500),
            };
            return commandList;
        }

        public static List<Command> WouldYouBuyaFrame()
        {            
            var commandList = new List<Command>
            {
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),
               new Command(ButtonActions.PlayNext, 500),
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 4500),
            };
            return commandList;
        }

        public static List<Command> RedditExpectations()
        {
            var commandList = new List<Command>
            {
               // awww
              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 0),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 0),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 0),
              new Command(ButtonActions.PlayNext, 500),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1550, 3000 ),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 4500),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1550, 6000 ),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 7500),

              //Look down at 13 seconds.
               // tilt down
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1417, 13500),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1597, 13500 ),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 19500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 19500),
            };
            return commandList;
        }

        public static List<Command> Dreams()
        {
            var commandList = new List<Command>
            {
               // Nod     // tilt up             
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 0 ),
               new Command(ButtonActions.PlayNext, 750),
                  // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 2000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 2000),
              
            };
            return commandList;
        }

        public static List<Command> WorthIt()
        {
            var commandList = new List<Command>
            {
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 0 ),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome , 500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome , 500),
              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , 300, 500),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , -300,500),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , -300, 500),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , 300, 500),

              new Command(ButtonActions.PlayNext, 1000),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 2500),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 3500),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,3500),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 3500 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 3500),

            };
            return commandList;
        }
    }
}
