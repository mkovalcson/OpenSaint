using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSaintTestHarnessConsoleApp
{
    public partial class Sequences
    {
        public static Movie CreateVRDilemaMovie()
        {
            var scene1 = new Scene("Scene1");

            scene1.AudioTracks = new List<string> {
                "1_HiAsAlways.mp3","2_DeepQuestion.mp3","3_Great.mp3", "4a_Causes.mp3","4b_Frustrations.mp3", "5_DontUnderstand.mp3",
                "6_OK.mp3", "7_LotsofInput.mp3", "8_NoPeopleDifferent.mp3","9_DoesntSeemLikely.mp3",
                "10_WatchedAllYourVideos.mp3","11_OhBlame.mp3","12_SoYourSaying.mp3","13_SoPeopleNeedToTry.mp3",
                "14_AwwwUnhappyPeople.mp3","15_VRMarketExciting.mp3", "16_ValveShare.mp3",
                "17_ValveInsideOutTracking.mp3","18_oooDFwireless.mp3","19_WirelessWithoutComplaints.mp3",
                  "20_WhatAboutValveFrame.mp3","21_Reddit.mp3","22_Dreams.mp3","23_WorthIt.mp3"
            };

            scene1.Sequences.Add(new Sequence(Sequences.Init1(), 0));   // Set Servos to slow  speed for effect     
            scene1.Sequences.Add(new Sequence(Sequences.Hi(), 0));           
            scene1.Sequences.Add(new Sequence(Sequences.Deep(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.Great(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.Causes(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.Frustrations(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.DontUnderstand(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.OK(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.LotsInput(), 0)); 
            scene1.Sequences.Add(new Sequence(Sequences.NoShapesSizes(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.NotLikely(), 0)); 
            scene1.Sequences.Add(new Sequence(Sequences.WatchedYourVideos(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.OhBlameYou(), 0));        
            
            scene1.Sequences.Add(new Sequence(Sequences.SoYouAreSaying(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.PeopleDifferentExperiences(), 0)); 
            scene1.Sequences.Add(new Sequence(Sequences.AwwwUnhappyPeople(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.VRMarket(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.MarkValve(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.ValveInsideoutTracking(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.ooooDFWireless(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.WirelessWithoutComplaints(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.WouldYouBuyaFrame(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.RedditExpectations(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.Dreams(), 0));
            scene1.Sequences.Add(new Sequence(Sequences.WorthIt(), 0));                   

            string exeFolder = AppContext.BaseDirectory;
            var movieName = "VRDilema";
            var newMovie = new Movie(movieName, exeFolder + "MOVIES\\" + movieName, new List<Scene> { scene1 }, TriggerSource.Pedals);
            //newMovie.BackgroundMotion = new Sequence(Sequences.BackgroundLoop1(), 0);
            //newMovie.msInactivityTimeout = 2000; // 5 seconds of inactivity before background motion kicks in.
            return newMovie;
        }

        public static List<Command> Init1()
        {
            var commandList = (new List<Command>
            {
              new Command(ButtonActions.ServoAllGoHome, 0),
              new Command(ButtonActions.MaestroSetAll, ServoSpeed.Slow, 500),
              new Command(RobotControls.NeckTurn,ButtonActions.MaestroSet, ServoSpeed.Default, 750),
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600,1.0 ),

              new Command(ButtonActions.DisableAllServos, 1800),
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

              new Command(RobotControls.LeftLensHorizontal,  ButtonActions.ServoHome,  1.0),
              new Command(RobotControls.RightLensHorizontal,  ButtonActions.ServoHome , 1.0),

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

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 5.5 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 5.5 ),

                 new Command(ButtonActions.DisableAllServos, 7000),
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
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 1.0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 1.0 ),

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
            new Command(ButtonActions.MaestroSetAll, ServoSpeed.Default,4.5),
            new Command(RobotControls.NeckTiltRight,ButtonActions.ServoHome  ,4.5),
            new Command(RobotControls.NeckTiltLeft,ButtonActions.ServoHome , 4.5),
            new Command(RobotControls.LeftIris, ButtonActions.ServoHome, 4.5),
            new Command(RobotControls.RightIris, ButtonActions.ServoHome, 4.5),
            new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome ,4.5),
            new Command(RobotControls.LeftLensVertical, ButtonActions.ServoHome ,4.5),
            new Command(RobotControls.RightLensHorizontal, ButtonActions.ServoHome ,4.5),
            new Command(RobotControls.RightLensVertical,  ButtonActions.ServoHome ,4.5),

            new Command(RobotControls.RightLensHorizontal,ButtonActions.DisableServo, 5.000),
            new Command(RobotControls.RightIris,ButtonActions.DisableServo, 5.000),

            // low blue
            new Command(ButtonActions.RGBCommand , "Clear,eyes,lr", 4.500),

             new Command(ButtonActions.DisableAllServos, 5.250),

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
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome , 1.0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1.0),
           
            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 1.0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 1.0),


              // tilt left First frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 2100 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 2100 ),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, 200 , 3500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, -200 , 3500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 4.5),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 4.5),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300,5.0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,5.0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 5.0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 5.0),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , 100, 6500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,-100, 6500),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, -200 , 7500),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, 200 , 7500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 8500),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 8500),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500),

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 1.00),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 1.00),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11.0),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11.0),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11.0),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11.0),

              // tilt right 2nd frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 12000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 12000 ),


              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, 200 , 3500+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, -200 , 3500+13000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -200,3500+13000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 200,3500+13000),

              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 200, 3500+13000),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -200, 3500+13000),


              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5+13000),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHomeDelta, -200 , 7500+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHomeDelta, 200 , 7500+13000),


              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 8000+13000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 8000+13000 ),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 9500+13000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11.0+13000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11.0+13000),
              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11.0+13000),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11.0+13000),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 11.0+13000),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome ,11.0+13000),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ,  11.0+13000 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome , 11.0+13000),

              // 4th frustration
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 21.0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 21.0 ),

            new Command(RobotControls.BrowLeftTopTilt, ButtonActions.MaestroSet, ServoSpeed.Slow, 25.0),
            new Command(RobotControls.BrowRightTopTilt, ButtonActions.MaestroSet, ServoSpeed.Slow, 25.0),

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

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 1.0 ),

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
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome , 1.0),
            new Command(RobotControls.BrowLeftTopOpen, ButtonActions.ServoHome ,1.0),

            new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome , 1.0),
            new Command(RobotControls.BrowRightBottomOpen,  ButtonActions.ServoHome , 1.0),          
              

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
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 1.0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 1.0 ),
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

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 1.0 ),

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
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1475, 5.5 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1561, 5.5 ),

                    // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 7000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 7000),

                 // tilt up
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 8500 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 8500 ),          

                  // tilt down (home)
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 1.00 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 1.00),

               new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 1.00 ),
               new Command(RobotControls.RightIris,  ButtonActions.ServoHome, 1.00 ),

               new Command(ButtonActions.DisableAllServos, 11.0),
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

               new Command(ButtonActions.PlayNext, 1.0),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1600, 2500 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 8000),

               new Command(ButtonActions.DisableAllServos, 1.0),
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

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoHomeDelta , -100, 4.5),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoHomeDelta ,100, 4.5),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300,5.0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,5.0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 5.0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 5.0),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 5.5),


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

              new Command(RobotControls.BrowLeftTopTilt,  ButtonActions.ServoValue ,1880, 1.00),
              new Command(RobotControls.BrowRightTopTilt,  ButtonActions.ServoValue ,1020, 1.00),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHome , 11.0),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHome , 11.0),

              new Command(RobotControls.LeftLensVertical,ButtonActions.ServoHome , 11.0),
              new Command(RobotControls.RightLensVertical,ButtonActions.ServoHome ,11.0),

                new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 12000 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 12000 ),

                new Command(ButtonActions.DisableAllServos, 14.5),
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
              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1423, 1.0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1509, 1.0 ),

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
               new Command(ButtonActions.PlayNext, 1.0),

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300, 1.0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,1.0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 1.0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 1.0),


             new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1451, 6000 ),


              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 6500),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,6500),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 6500 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 6500),

               // tilt up
               new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1481, 7000),
               new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1533, 7000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1500, 8000 ),

               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1400, 1.00 ),

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

              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHomeDelta , -300, 1.0),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHomeDelta , 300,1.0),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHomeDelta , 300, 1.0 ),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHomeDelta , -300, 1.0),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 2000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, -200 , 2000),


               new Command(RobotControls.LeftIris,   ButtonActions.ServoValue, 1775 , 3000),
               new Command(RobotControls.RightIris,  ButtonActions.ServoValue,  1100 , 3000),

              new Command(RobotControls.LeftLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 4000),
              new Command(RobotControls.RightLensHorizontal,ButtonActions.ServoHomeDelta, 200 , 4000),

              new Command(RobotControls.LeftIris,   ButtonActions.ServoHome, 5.0 ),
              new Command(RobotControls.RightIris,  ButtonActions.ServoHome,5.0 ),

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

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoValue, 1417, 1.0 ),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoValue, 1597, 1.0),

                //Whip antenna pop up wiggle back and forth and drop
               new Command(RobotControls.Whip_Antenna_RaiseLower,  ButtonActions.ServoMax , 1500),
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1000, 1500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1800, 2500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1000, 3500), //1400 Center
               new Command(RobotControls.Whip_Antenna_Rotate,  ButtonActions.ServoValue , 1800, 4.5), //1400 Center
               new Command(RobotControls.Whip_Antenna_RaiseLower,  ButtonActions.ServoMin ,  5.0),


              new Command(RobotControls.BrowLeftBottomOpen, ButtonActions.ServoHome, 4.5),
              new Command(RobotControls.BrowRightBottomOpen, ButtonActions.ServoHome,4.5),
              new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome, 4.5),
              new Command(RobotControls.BrowLeftTopOpen,  ButtonActions.ServoHome, 4.5),

              new Command(RobotControls.NeckTiltLeft, ButtonActions.ServoHome, 4.5),
              new Command(RobotControls.NeckTiltRight, ButtonActions.ServoHome, 4.5),
              new Command(RobotControls.NoseBody, ButtonActions.ServoHome, 4.5),

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
               new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 4.5),
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
              new Command(RobotControls.NeckTurn, ButtonActions.ServoValue, 1450, 4.5),
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

              new Command(ButtonActions.PlayNext, 1.0),
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
