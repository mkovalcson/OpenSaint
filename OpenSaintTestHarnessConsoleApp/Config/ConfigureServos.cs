
namespace OpenSaintLib.Utilities
{

    public static class ServoConfig
    {
        public static Servo[] ConfigureServos(string headPort)
        {
            var servos = new Servo[24];

            var Reversed = true;
            var Normal = false;

            //| Servo | Suggested Speed | Suggested Accel |
            //| ------------ | --------------- | --------------- |
            //| **ASME - 04B * * | 80 – 150 | 20 – 40 |
            //| **HS - 85 * *    | 30 – 60 | 10 – 20 |
            //| **HS - 40 * *    | 20 – 40 | 8 – 15 |

            var initialSpeed = ServoSpeed.Default;

            // Fast servos
            // Fast: Iris, Vent, Gaze, MFRC rotate, Whip rotate
            // 
            // Servo Speed/Accel  Default, Slow, Fast, Crawl

            // Nose
            int[] NoseSpeed = { 100, 50, 0, 50 };
            int[] NoseAccel = { 30, 15, 0, 15 };
            servos[(int)RobotControls.NoseBasket] = new Servo(RobotControls.NoseBasket, headPort, ServoMode.Stay, StartPosition.Home, Normal, 850, 850, 1150, NoseSpeed, NoseAccel);  // Home is down.
            servos[(int)RobotControls.NoseBody] = new Servo(RobotControls.NoseBody, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1200, 1000, 1500, NoseSpeed, NoseAccel);

            servos[(int)RobotControls.NoseBody].ModeValue = 1430; // mode is down    // Nose Body down to shut eyes 1430            
         
            servos[(int)RobotControls.NoseBasket].ConfigureSpeed(initialSpeed);
            servos[(int)RobotControls.NoseBody].ConfigureSpeed(initialSpeed);
            servos[(int)RobotControls.NoseBasket].GoHome();
            servos[(int)RobotControls.NoseBody].GoHome();

            // Neck
            int[] NeckSpeed = { 20, 10, 0, 0 };
            int[] NeckAccel = { 10, 5, 0, 0 }; // Left 370  Right 204 range... Left 1596  1336 ( 260) Right 1645 1400  ( 245 )  Left 276, Right 274
            var neckLeftTilt = new Servo(RobotControls.NeckTiltLeft, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1533, 1445, 1594, NeckSpeed, NeckAccel); // 1453 down  1594 up  center  1533 }   
            var neckRightTilt = new Servo(RobotControls.NeckTiltRight, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1480, 1400, 1564, NeckSpeed, NeckAccel);// 1687 down  1440 up  Center 1564      
         
            //neckLeftTilt.GoHome();
            //neckRightTilt.GoHome();
            //while (true)
            //{
            //    neckLeftTilt.GoValue(1594);
            //    neckRightTilt.GoValue(1440);
            //    Thread.Sleep(1500);
            //    neckLeftTilt.GoValue(1445);
            //    neckRightTilt.GoValue(1564);
            //    Thread.Sleep(1500);
            //}
           

            int[] NeckRSpeed = { 90, 3, 0, 45 };
            int[] NeckRAccel = { 10, 1, 0, 15 };

            var neckturn = new Servo(RobotControls.NeckTurn, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1325, 850, 1740, NeckRSpeed, NeckRAccel, 30, (float)0.35);
            servos[(int)RobotControls.NeckTiltRight] = neckRightTilt;
            servos[(int)RobotControls.NeckTiltLeft] = neckLeftTilt;
            servos[(int)RobotControls.NeckTurn] = neckturn;

            neckLeftTilt.ConfigureSpeed(initialSpeed);
            neckRightTilt.ConfigureSpeed(initialSpeed);
            neckturn.ConfigureSpeed(initialSpeed);

            neckLeftTilt.GoHome();
            neckRightTilt.GoHome();
            neckturn.GoHome();

            // Eye Gaze
            //int[] EyeGazeSpeed = { 40, 20, 0, 20 }; // Maestro Speed ms  (Default, Slow, Fast, crawl)
            //int[] EyeGazeAccel = { 15, 5, 0, 5 };  // Maestro Accel ms  (Default, Slow, Fast)
            int[] EyeGazeSpeed = { 0, 60, 0, 20 }; // Maestro Speed ms  (Default, Slow, Fast, crawl)
            int[] EyeGazeAccel = { 0, 25, 0, 5 };  // Maestro Accel ms  (Default, Slow, Fast)

            var leftLensH = new Servo(RobotControls.LeftLensHorizontal, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1450, 650, 2250, EyeGazeSpeed, EyeGazeAccel);
            var rightLensH = new Servo(RobotControls.RightLensHorizontal, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1450, 650, 2250, EyeGazeSpeed, EyeGazeAccel);
           
            leftLensH.GoHome();
            rightLensH.GoHome();

            //leftLensH.GoValue(650);
            //rightLensH.GoValue(650);

            //leftLensH.GoValue(2250);
            //rightLensH.GoValue(2250);

            var leftLensV = new Servo(RobotControls.LeftLensVertical, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1400, 600, 2200, EyeGazeSpeed, EyeGazeAccel);
            var rightLensV = new Servo(RobotControls.RightLensVertical, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1520, 720, 2320, EyeGazeSpeed, EyeGazeAccel);

            rightLensV.GoHome();   
            leftLensV.GoHome();

            //leftLensV.GoValue(600);
            //rightLensV.GoValue(2320);
         

            //leftLensV.GoValue(2200);
            //rightLensV.GoValue(720);


            servos[(int)RobotControls.RightLensHorizontal] = rightLensH;
            servos[(int)RobotControls.LeftLensHorizontal] = leftLensH;
            servos[(int)RobotControls.LeftLensVertical] = leftLensV;
            servos[(int)RobotControls.RightLensVertical] = rightLensV;

            leftLensH.ConfigureSpeed(initialSpeed);
            leftLensV.ConfigureSpeed(initialSpeed);
            rightLensH.ConfigureSpeed(initialSpeed);
            rightLensV.ConfigureSpeed(initialSpeed);

          

          
            // Iris
            //int[] IrisSpeed = { 30, 10, 0, 10 };
            //int[] IrisAccel = { 12, 3, 0, 5 };
            int[] IrisSpeed = { 90, 90, 0, 10 };
            int[] IrisAccel = { 15, 15, 0, 5 };
            var leftIris = new Servo(RobotControls.LeftIris, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1575, 1350, 1950, IrisSpeed, IrisAccel);
            var rightIris = new Servo(RobotControls.RightIris, headPort, ServoMode.Stay, StartPosition.Home, Normal, 975, 750, 1350, IrisSpeed, IrisAccel);
            // 650/2 = 325.   Left 1675, Right 975
            // 
            // Old Iris home  Left=1525, Right = 925

            //leftIris.GoValue(1350);
            //rightIris.GoValue(750);

            //leftIris.GoValue(1950);
            //rightIris.GoValue(1350);

            servos[(int)RobotControls.RightIris] = rightIris;
            servos[(int)RobotControls.LeftIris] = leftIris;

            leftIris.ConfigureSpeed(initialSpeed);
            rightIris.ConfigureSpeed(initialSpeed);

            leftIris.GoHome();
            rightIris.GoHome();

            // Brows
            int[] BrowsSpeed = { 60, 10, 0, 2 };
            int[] BrowsAccel = { 15, 5, 0, 3 };
            var browLeftTop = new Servo(RobotControls.BrowLeftTopOpen, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1523, 920, 1770, BrowsSpeed, BrowsAccel); //1513, 910, 1760,
            var browRightTop = new Servo(RobotControls.BrowRightTopOpen, headPort, ServoMode.Stay, StartPosition.Home, Normal, 827, 600, 1380, BrowsSpeed, BrowsAccel); //777, 550, 1330,827, 600, 1330,

            // Brow Left 1740 level with nose down
            // Brow Right 550 level with nose down

            // Home with Nose up partly 
            // Left 1533
            // Right 757  

            //neckLeftTilt.DisableServo();
            //neckRightTilt.DisableServo();   

            //browLeftTop.GoHome();
            //browRightTop.GoHome();

            //browLeftTop.GoValue(1513);
            //browRightTop.GoValue(777);

            //browLeftTop.GoValue(1634);
            //browRightTop.GoValue(666);

            //browLeftTop.GoValue(1392);
            //browRightTop.GoValue(888);

            //browLeftTop.GoValue(1760);
            //browRightTop.GoValue(550);

            //browLeftTop.GoValue(910);
            //browRightTop.GoValue(1330);

            //browLeftTop.GoValue(1513);
            //browRightTop.GoValue(777);           



            servos[(int)RobotControls.BrowLeftTopOpen] = browLeftTop;
            servos[(int)RobotControls.BrowRightTopOpen] = browRightTop;

            browLeftTop.ConfigureSpeed(initialSpeed);
            browRightTop.ConfigureSpeed(initialSpeed);

            browLeftTop.GoHome();
            browRightTop.GoHome();

            int[] bBrowsSpeed = { 40, 10, 0, 2 };
            int[] bBrowsAccel = { 10, 5, 0, 3 };
            var browLeftTilt = new Servo(RobotControls.BrowLeftTopTilt, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1800, 910, 1900, bBrowsSpeed, bBrowsAccel);  // 1750
            var browRightTilt = new Servo(RobotControls.BrowRightTopTilt, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1100, 1000, 1990, bBrowsSpeed, bBrowsAccel);  // 1970)

          

            //browLeftTilt.GoHome();
            //browRightTilt.GoHome();

            //browLeftTilt.GoValue(1900);
            //browRightTilt.GoValue(1000);

            //browLeftTilt.GoValue(910);
            //browRightTilt.GoValue(1990);


            browLeftTilt.LimitUpper = 1950; // 180
            browRightTilt.LimitLower = 950; // 220
                                            //Different limits for Nose Up  Allow more downward tilt  will be upper limit for one and lower limit for the other
                                            //browLeftTilt.ModeLower =
                                            //browLeftTilt.ModeUpper =
                                            //browRightTilt.ModeLower =
                                            //browRightTilt.ModeUpper =

        
            servos[(int)RobotControls.BrowLeftTopTilt] = browLeftTilt;
            servos[(int)RobotControls.BrowRightTopTilt] = browRightTilt;

            browLeftTilt.ConfigureSpeed(initialSpeed);
            browRightTilt.ConfigureSpeed(initialSpeed);

            browLeftTilt.GoHome();
            browRightTilt.GoHome();

            var browLeftBottom = new Servo(RobotControls.BrowLeftBottomOpen, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1750, 940, 2050, BrowsSpeed, BrowsAccel);           
            var browRightBottom = new Servo(RobotControls.BrowRightBottomOpen, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1100, 700, 1910, BrowsSpeed, BrowsAccel);

            browLeftBottom.GoHome();
            browRightBottom.GoHome();

            //browLeftBottom.GoValue(2050);
            //browRightBottom.GoValue(700);

            //browLeftBottom.GoValue(930);
            //browRightBottom.GoValue(1920);

            // Different limits for Eye Pop don't allow closing all the way will be upper limit for one and lower limit for the other
            // Left 1363
            // Right 1537
            //browLeftBottom.ModeLower =
            //browLeftBottom.ModeUpper =
            //browRightBottom.ModeLower =
            //browRightBottom.ModeUpper =

         

            servos[(int)RobotControls.BrowLeftBottomOpen] = browLeftBottom;
            servos[(int)RobotControls.BrowRightBottomOpen] = browRightBottom;

            browLeftBottom.GoHome();
            browRightBottom.GoHome();

            // Vents
            int[] VentSpeed = { 40, 10, 0, 10 };
            int[] VentAccel = { 20, 5, 0, 5 };
            var leftVent = new Servo(RobotControls.LeftEyeVent, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1765, 1765, 2086, VentSpeed, VentAccel);   // Closed Left Vent 1765 OPen 2086 closed
            var rightVent = new Servo(RobotControls.RightEyeVent, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1125, 835, 1125, VentSpeed, VentAccel);// Closed Right Vent 1125 Open 835
            
            leftVent.GoHome();         
            //leftVent.GoValue(2086);
            //leftVent.GoHome();

            rightVent.GoHome();
            //rightVent.GoValue(835);
            //rightVent.GoHome();

           

            servos[(int)RobotControls.LeftEyeVent] = leftVent;
            servos[(int)RobotControls.RightEyeVent] = rightVent;

            leftVent.ConfigureSpeed(initialSpeed);
            rightVent.ConfigureSpeed(initialSpeed);

            leftVent.DisableServo();
            rightVent.DisableServo();

            // MFRC
            int[] MFRCHSpeed = { 0, 1, 0, 10 };
            int[] MFRCHAccel = { 0, 1, 0, 20 };
            var mfrcRotate = new Servo(RobotControls.MFR_Rotate, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1320, 800, 1865, MFRCHSpeed, MFRCHAccel);// 500 full left 2212 rull right  1320 centered dish front        
            int[] MFRCVSpeed = { 100, 50, 0, 50 };
            int[] MFRCVAccel = { 30, 25, 0, 25 };
            var mfrcUpDown = new Servo(RobotControls.MFR_UpDown, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 550, 550, 1070, MFRCVSpeed, MFRCVAccel);// 550 all the way down  1170 all the way up.
            servos[(int)RobotControls.MFR_UpDown] = mfrcUpDown;
            servos[(int)RobotControls.MFR_Rotate] = mfrcRotate;

            mfrcRotate.ConfigureSpeed(initialSpeed);
            mfrcUpDown.ConfigureSpeed(initialSpeed);

            mfrcRotate.GoHome();
            //mfrcRotate.DisableServo();
            //mfrcUpDown.GoHome();
            //mfrcUpDown.GoValue(1070);
            mfrcUpDown.GoHome();

            // Whip Antenna
            int[] WhipVSpeed = { 100, 20, 0, 50 };
            int[] WhipVAccel = { 30, 5, 0, 25 };
            var whipUpDown = new Servo(RobotControls.Whip_Antenna_RaiseLower, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 2200, 1620, 2200, WhipVSpeed, WhipVAccel);//the way up 1560,  down 2200           

            int[] WhipHSpeed = { 0, 0, 0, 10 };
            int[] WhipHAccel = { 0, 0, 0, 20 };
            var whipRotate = new Servo(RobotControls.Whip_Antenna_Rotate, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1400, 500, 2300, WhipHSpeed, WhipHAccel);// 500 full left 2300 rull right 1400 Center
           
            servos[(int)RobotControls.Whip_Antenna_Rotate] = whipRotate;
            servos[(int)RobotControls.Whip_Antenna_RaiseLower] = whipUpDown;

            whipUpDown.ConfigureSpeed(initialSpeed);
            whipRotate.ConfigureSpeed(initialSpeed);

            whipUpDown.GoHome();
            //whipUpDown.GoValue(1620);
            //whipUpDown.GoHome();
            whipRotate.GoHome();

            // Mic
            int[] micSpeed = { 60, 50, 0, 50 };
            int[] micAccel = { 25, 25, 0, 25 };
            var microphone = new Servo(RobotControls.Microphone_RaiseLower, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1625, 1625, 2298, micSpeed, micAccel);  //Mic          
            servos[(int)RobotControls.Microphone_RaiseLower] = microphone;
            microphone.ConfigureSpeed(initialSpeed);
            microphone.GoHome();
            microphone.GoValue(2298);
            microphone.GoHome();

            Thread.Sleep(2000);// Wait 4 seconds for all servos to find home.

           // servos[0].SetRangeAll(servos);

            foreach (var servo in servos)
            {
                servo.DisableServo();
            }

            return servos;

        }
    }
}
