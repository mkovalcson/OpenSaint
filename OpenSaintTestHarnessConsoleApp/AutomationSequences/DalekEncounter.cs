using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSaintTestHarnessConsoleApp
{
    public partial class Sequences
    {

        /// <summary>
        /// Build Dalek Movie Scene  
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public static Scene BuildDalekScene(string sceneName)
        {
            var scene1 = new Scene(sceneName);

            scene1.AudioTracks = new List<string> {"aww_output.wav","nodissassemble_output.wav",
                "Friends_output.wav","Notthatkind_output.wav",
                "Hmmm_output.wav", "helpyou_output.wav","Lookeyes_output.wav",
                "Nice2Humans_output.wav","Nice2Humans2_output.wav","LeaveEarth_output.wav","Superior_output.wav"};

            scene1.Sequences.Add(new Sequence(Sequences.Init(), 0));   // Set Servos to slow  speed for effect        
            // Dalek approaches and stops
            scene1.Sequences.Add(new Sequence(Sequences.TurnTilt(), 200));  // Awww   
            //Dalek: Large robot,  join forces with the Daleks to exterminate all humans!           
            scene1.Sequences.Add(new Sequence(Sequences.PartlyAngry(), 500));  // No, no dissassemble        
                                                                               //Dalek:  You will join force with the Daleks
            scene1.Sequences.Add(new Sequence(Sequences.FriendsInstead(), 500)); // You don't want to be friends instead
            //Dalek: Friends.. ? Searching…
            //Dalek: Bender voice “Hey Baby want to kill all Humans”
            scene1.Sequences.Add(new Sequence(Sequences.VeryAngry(), 250));// Not that kind of friends
            //Dalek: You will obey the Daleks, we are the superior beings.
            scene1.Sequences.Add(new Sequence(Sequences.NormalEyesEyePop(), 250));

            scene1.Sequences.Add(new Sequence(Sequences.ThinkCycleTilt(), 250)); // Hmmmm  // I think I will help you.
            // Start rainbow color          
            scene1.Sequences.Add(new Sequence(Sequences.HypnoEyes(), 250));  // Look into my eyes // You will be nice to Humans      
            // Dalek: What?
            scene1.Sequences.Add(new Sequence(Sequences.PlayAudioNext(), 250));  // You WILL be nice to Humans
            //Dalek: I will be nice to humans.
            scene1.Sequences.Add(new Sequence(Sequences.PlayAudioNext(), 250)); // You will leave Earth and never come back.
            //Dalek: I will Obey.
            //Dalek turns  around and leaves...
            scene1.Sequences.Add(new Sequence(Sequences.ShakeHead(), 250)); // Superior Beings...  Fade to black         

            return scene1;
        }

        public static List<Command> Init()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoAllGoHome, 0),
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 2000),
              new Command(ButtonActions.DisableAllServos, 2500),
            });

            return commandList;
        }
        public static List<Command> TurnTilt()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1675, 0),  // .5 secon Turn right 60 degrees
                 
                // Tilt neck down left
                //new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoPercentoffHome, -.2, 2000), // 3 sec move
                //new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoPercentoffHome, -.2, 2000),

                new Command(ButtonActions.RGBCommand, "Fade,255,255,255,20,eyes,lr,20,IN,1,0" , 2500), //"SetRGBColor, 255,255,255, 20,eyes,lr,0"
                
               // new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 1000),  // Set Slow Movement     
                // Close Irises
                new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 3500), // 5 sec move
                new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 3500),
               
                // tilt eye flaps down
               new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1820, 4000),
               new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 4000),

                new Command(ButtonActions.DisableAllServos, 4500),

                new Command(ButtonActions.PlayFirst, 5000),  // Aww isn't it cute              
            };

            return commandList;
        }

        public static List<Command> FriendsInstead()
        {
            var commandList = new List<Command>
            {
                // Tilt left
                //new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoPercentoffHome, -0.20,  0), // 3 sec move
                //new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoPercentoffHome, 0.20, 0),

                //new Command(ButtonActions.PlayNext, 1000),

                //// Tilt right
                //new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoPercentoffHome, 0.20,  1500), // 3 sec move
                //new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoPercentoffHome, -0.20, 1500),

                //// Straight
                //new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoPercentoffHome, -0.20,  2500), // 3 sec move
                //new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoPercentoffHome, -0.20, 2500),

            };

            return commandList;
        }

        public static List<Command> NormalEyesEyePop()
        {
            var commandList = new List<Command>
            {


            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoHome, 0),
            new Command(RobotControls.RightEyeVent,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftIris,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightIris,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome , 0),

            new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome , 0),

            new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 0),

            new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftLensVertical,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 0),

            new Command( ButtonActions.RGBCommand , "ClearAll" , 0),

            new Command(ButtonActions.EyePopNoSafety, 2000),  // Eye Pop Open                                 
                             
            new Command(RobotControls.LeftIris, ButtonActions.ServoMin, 4000),
            new Command(RobotControls.RightIris, ButtonActions.ServoMin, 4000),

            new Command(ButtonActions.EyePopClosed, 7000),

            new Command(RobotControls.LeftIris, ButtonActions.ServoHome, 9000),
            new Command(RobotControls.RightIris, ButtonActions.ServoHome, 9000),

            new Command(ButtonActions.DisableAllServos, 10000),

            };

            return commandList;
        }
        public static List<Command> ThinkCycleTilt()
        {
            var commandList = new List<Command>
            { 
                //Thinking

              // Close Irises a bit
                new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 0),
                new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 0),

                // Tilt neck up left
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoValue,  1600 , 0), // Right down half way
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoValue,  1156 , 0), // Left up half way

                // Gaze top left
                new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoMax , 0),
                new Command(RobotControls.LeftLensVertical,ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensVertical,  ButtonActions.ServoMin , 0),

                // brows normal
                new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 0),

                // will only disable the servos moved in this Action after the delay in ms
              //  new Command( ButtonActions.DisableAllRunningServos , 500 , 0),

                // low blue
                new Command( ButtonActions.RGBCommand , "Fade,0,0,255,30,eyes,lr,90,IN,1,0" , 0),

                new Command(ButtonActions.PlayNext, 1500),  //  Hmmmm

                // Unthinking
            new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default,2500),
            new Command(RobotControls.NeckTiltRight,ButtonActions.ServoHome  , 2500),
            new Command(RobotControls.NeckTiltLeft,ButtonActions.ServoHome , 2500),
            new Command(RobotControls.LeftIris, ButtonActions.ServoHome, 2500),
            new Command(RobotControls.RightIris, ButtonActions.ServoHome, 2500),
            new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 2500),
            new Command(RobotControls.LeftLensVertical, ButtonActions.ServoHome , 2500),
            new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoHome , 2500),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 2500),         
            // low blue
            new Command(ButtonActions.RGBCommand , "Fade,0,0,255,30,eyes,lr,90,OUT,1,0", 2500),

                 // Tilt neck down left
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoMin,  3500), // 3 sec move
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoMin, 3500),

                new Command(ButtonActions.PlayNext, 5000), // I think I  will help you
            };


            return commandList;
        }

        public static List<Command> BackgroundMotion()
        {
            var commandList = (new List<Command>
            {
            new Command(RobotControls.NeckTurn, ButtonActions.ServoValue , 1600, 500),
            });

            return commandList;
        }

        public static List<Command> SetSpeed(ServoSpeed speed)
        {
            var commandList = (new List<Command>
            {
            new Command(ButtonActions.MaestroSetAll, speed, 0),
            });

            return commandList;
        }

        public static List<Command> ResetAll()
        {
            var commandList = new List<Command>
            {
                new Command(ButtonActions.ServoAllGoHome, 0),
             };

            return commandList;
        }

        public static List<Command> HypnoEyes()
        {

            var commandList = new List<Command>
            {
                new Command(ButtonActions.PlayNext,0),
                new Command(ButtonActions.RGBCommand,  "RainbowCycle,150,lr,6", 2000),  // Set Slow Movement        
                new Command(ButtonActions.PlayNext, 4000),
            };

            return commandList;
        }

        public static List<Command> ShakeHead()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.NeckTurn,ButtonActions.ServoValue, 1500, 0 ), //Left
                new Command(ButtonActions.PlayNext, 500),
                new Command(RobotControls.NeckTurn,ButtonActions.ServoValue, 1600, 1000 ),
                new Command(RobotControls.NeckTurn,ButtonActions.ServoValue, 1500, 1500 ),
                new Command(RobotControls.NeckTurn,ButtonActions.ServoValue, 1600, 1500),
            };

            return commandList;
        }


        public static List<Command> PlayAudioFirst()
        {
            var commandList = new List<Command>
            {
                new Command(ButtonActions.PlayFirst, 0),  // No, no dissassemble humnans..
            };

            return commandList;
        }

        public static List<Command> PartlyAngry()
        {
            var commandList = new List<Command>
            {
            new Command(RobotControls.NoseBody,ButtonActions.ServoMin, 0),
            new Command(RobotControls.NoseBasket, ButtonActions.ServoMin,0),
            // Brows tilted
            // TODO: need to pull these back some...
            new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue, 1527 , 0),
            new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoValue, 1295 , 0),
            // Irises half closed
            new Command(RobotControls.LeftIris, ButtonActions.ServoMin , 0),
            new Command(RobotControls.RightIris, ButtonActions.ServoMin , 0),
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax , 0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax ,0),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoMin , 0),
            new Command(RobotControls.LeftEyeVent,  ButtonActions.ServoMin , 0),
            new Command(RobotControls.RightEyeVent, ButtonActions.ServoMin , 0),
            // Fadein to red over a couple seconds           
            new Command(ButtonActions.RGBCommand , "Fade,255,0,0,100,eyes,lr,40,IN,1,0" , 0),
            //"SetRGBColor, 255,0,0,50,eyes,lr,0" }), //"Fade,255,0,0,100,eyes,lr,40,IN,1,0"})
            new Command(ButtonActions.DisableAllRunningServos, 1500),
            new Command(ButtonActions.PlayNext, 2500),
            new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 2500),
            };
            return commandList;
        }


        public static List<Command> VeryAngry()
        {
            var commandList = new List<Command>
            {
                new Command(RobotControls.LeftEyeVent, ButtonActions.ServoValue  , 2086, 0 ),
                new Command(RobotControls.RightEyeVent, ButtonActions.ServoValue , 835 , 0),
                // Nose down
                new Command(RobotControls.NoseBody, ButtonActions.ServoMin , 0),
                new Command(RobotControls.NoseBasket, ButtonActions.ServoMin , 0),
                // Brows tilted
                new Command(RobotControls.BrowLeftTopTilt, ButtonActions.ServoValue , 1327 , 0),
                new Command(RobotControls.BrowRightTopTilt, ButtonActions.ServoValue , 1495 , 0),
                // Irises half closed
                new Command(RobotControls.LeftIris, ButtonActions.ServoMin , 0),
                new Command(RobotControls.RightIris, ButtonActions.ServoMin , 0),
                // Brow Top closed
                new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoMax , 0),
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoMax , 0),
                // Bottom brows closed
                new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoMin , 0),
                new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoMin , 0),
                new Command(ButtonActions.RGBCommand , "Fade,255,0,0,200,vents,lr,40,IN,1,0" , 0),

                new Command(ButtonActions.DisableAllRunningServos, 1500),
                new Command(ButtonActions.PlayNext, 2000),
                new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 2000),

          //   new Command(ButtonActions.DisableAllRunningServos, 500, 0)
            };

            return commandList;
        }

        public static List<Command> NormalEyes()
        {
            var commandList = new List<Command>
            {
            new Command(RobotControls.LeftEyeVent, ButtonActions.ServoHome, 0),
            new Command(RobotControls.RightEyeVent,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftIris,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightIris,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHome , 0),

            new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome , 0),

            new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 0),

              new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftLensVertical,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 0),

            new Command( ButtonActions.RGBCommand , "ClearAll" , 0),

            };
            return commandList;
        }


        public static List<Command> PlayAudioNext()
        {
            var commandList = new List<Command>
            {
                new Command(ButtonActions.PlayNext, 0),
            };
            return commandList;
        }


        public static List<Command> EyePop()
        {
            var commandList = new List<Command>
            {
                new Command(ButtonActions.EyePopNoSafety, 0),  // Eye Pop Open                                 
                             
                new Command(RobotControls.LeftIris,   ButtonActions.ServoMin, 3000),
                new Command(RobotControls.RightIris,  ButtonActions.ServoMin, 3000),

                new Command(ButtonActions.EyePopClosed, 6000),

                new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 7000),
                new Command(RobotControls.RightIris,  ButtonActions.ServoHome, 7000),
            };

            return commandList;
        }


        public static List<Command> Thinking()
        {
            var commandList = new List<Command>
            {  
                // Close Irises a bit
                new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 0),
                new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 0),

                // Tilt neck up left
                new Command(RobotControls.NeckTiltRight,  ButtonActions.ServoValue,  1600 , 0), // Right down half way
                new Command(RobotControls.NeckTiltLeft,  ButtonActions.ServoValue,  1156 , 0), // Left up half way

                // Gaze top left
                new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoMax , 0),
                new Command(RobotControls.LeftLensVertical,ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoMax , 0),
                new Command(RobotControls.RightLensVertical,  ButtonActions.ServoMin , 0),

                // brows normal
                new Command(RobotControls.BrowRightTopOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowLeftBottomOpen,  ButtonActions.ServoHome , 0),
                new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 0),

                // will only disable the servos moved in this Action after the delay in ms
              //  new Command( ButtonActions.DisableAllRunningServos , 500 , 0),

                // low blue
                new Command( ButtonActions.RGBCommand , "Fade,0,0,255,30,eyes,lr,90,IN,1,0" , 0),

                new Command(ButtonActions.PlayNext, 1500),
            };

            return commandList;
        }


        // Hmmm


        public static List<Command> UnThinking()
        {
            var commandList = new List<Command>
            {
            new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default,0),
            new Command(RobotControls.NeckTiltRight,ButtonActions.ServoHome  , 0),
            new Command(RobotControls.NeckTiltLeft,ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftIris, ButtonActions.ServoHome, 0),
            new Command(RobotControls.RightIris, ButtonActions.ServoHome, 0),
            new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 0),
            new Command(RobotControls.LeftLensVertical, ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoHome , 0),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome , 0),         
            // low blue
            new Command(ButtonActions.RGBCommand , "Fade,0,0,255,30,eyes,lr,90,OUT,1,0", 0),
            };

            return commandList;
        }


        public static List<Command> MFRC_Up()
        {
            var commandList = new List<Command>
            {
                  new Command(RobotControls.MFR_UpDown,ButtonActions.ServoMax,0),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 1000),

                  new Command(ButtonActions.PlayNext, 6000),  // I think I will help you
           
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMax, 3000),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 3000),

                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMax, 3000),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 3000),

                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMax, 3000),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 3000),

                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMax, 3000),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 3000),

                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMax, 3000),
                  new Command(RobotControls.MFR_Rotate,ButtonActions.ServoMin, 3000),

                   new Command(RobotControls.MFR_UpDown,ButtonActions.ServoMin,0),
            };

            return commandList;
        }


    }
}
