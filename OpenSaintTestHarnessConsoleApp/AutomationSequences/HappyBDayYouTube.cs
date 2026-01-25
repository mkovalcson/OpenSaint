using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OpenSaintTestHarnessConsoleApp
{
    public partial class Sequences
    {
        /// <summary>
        /// BuildScene  
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public static Scene HappyBDay(string sceneName)
        {
            var scene1 = new Scene(sceneName);

            scene1.AudioTracks = new List<string> { "memememe.wav", "happybirthday.wav" };

            scene1.Sequences.Add(new Sequence(Sequences.HBInit(), 0));  // Set Servos to slow  Close Eyes      
            scene1.Sequences.Add(new Sequence(Sequences.OpenEyes(), 1000));  // Wakes up             
            scene1.Sequences.Add(new Sequence(Sequences.MeMeMe(), 1500)); // prepares to sing        
            scene1.Sequences.Add(new Sequence(Sequences.HappyBDayRepeat(), 1500, 6)); // rotate and tilt head while singing 6 x back and forth moves 
            scene1.Sequences.Add(new Sequence(Sequences.HappyDone(), 500)); // recenter at end.
            return scene1;
        }

        public static List<Command> HBInit()
        {
            var commandList = (new List<Command>
            {
           
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 0),

              new Command(RobotControls.NoseBody, ButtonActions.ServoModeValue,500),

              new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,500),
              new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHome ,500),           
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax ,500 ),
            new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoMax ,500 ),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin ,500 ),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin ,500),
             });

            return commandList;
        }
        public static List<Command> OpenEyes()
        {
            var commandList = new List<Command>
            {
                  new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 0),
                new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 1000),

              new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoHome ,1000),
              new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoHome ,1000),           
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,1000 ),
            new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome ,1000 ),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome ,1000 ),
            new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,1000),

            new Command(ButtonActions.RGBCommand , "Fade,255,255,255,60,eyes,lr,90,in,1,0", 1500),

            new Command(RobotControls.LeftIris,   ButtonActions.ServoMin, 2000),
            new Command(RobotControls.RightIris,  ButtonActions.ServoMin, 2000),

            new Command(RobotControls.LeftLensVertical,  ButtonActions.ServoMin , 2500),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoMax , 2500),

            new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoMin , 3000),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoMin , 3000),

            new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoMax , 3500),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoMax , 3500),

            new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome , 4000),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 4000),
            new Command(RobotControls.LeftLensVertical,  ButtonActions.ServoHome , 4000),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 4000),

            new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 4000),
            new Command(RobotControls.RightIris,  ButtonActions.ServoHome, 4000),

            };

            return commandList;
        }

        public static List<Command> MeMeMe()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 0), // 1820
                new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 0),
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,0),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,0),
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 0 ),
                new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 0),
                new Command(ButtonActions.PlayFirst, 50),


                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoHomeDelta, -100,  2000),
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoHomeDelta, 175, 2000),

                new Command(ButtonActions.PlayNext, 2550),

            };

            return commandList;
        }


        public static List<Command> HappyBDayRepeat()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.NeckTurn,  ButtonActions.ServoHomeDelta, -100,  0),            
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoHomeDelta, -50,  0),
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoHomeDelta, 175, 0),

                new Command(RobotControls.NeckTurn,  ButtonActions.ServoHomeDelta, 75,  1000),
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoHomeDelta, -100,  1000),
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoHomeDelta, 125, 1000)
            };

            return commandList;
        }

        public static List<Command> HappyDone()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.NeckTurn,  ButtonActions.ServoHome,  2000),
                new Command(ButtonActions.DisableAllServos, 2750)
            };

            return commandList;
        }

    }
}
    

