
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
            servos[(int)RobotControls.NoseBody].EyePopSensitive = true;
            servos[(int)RobotControls.NoseBasket].ConfigureSpeed(initialSpeed);
            servos[(int)RobotControls.NoseBody].ConfigureSpeed(initialSpeed);
            servos[(int)RobotControls.NoseBasket].GoHome();
            servos[(int)RobotControls.NoseBody].GoHome();

            // Neck
            int[] NeckSpeed = { 20, 10, 0, 0 };
            int[] NeckAccel = { 10, 5, 0, 0 }; // Left 370  Right 204 range... Left 1596  1336 ( 260) Right 1645 1400  ( 245 )  Left 276, Right 274
            var neckLeftTilt = new Servo(RobotControls.NeckTiltLeft, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1464, 1320, 1596, NeckSpeed, NeckAccel); // 1626 down  1156 up  center  1391 }   
            var neckRightTilt = new Servo(RobotControls.NeckTiltRight, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1550, 1409, 1683, NeckSpeed, NeckAccel);// 1687 down  1440 up  Center 1564      

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
            int[] EyeGazeSpeed = { 0, 0, 0, 20 }; // Maestro Speed ms  (Default, Slow, Fast, crawl)
            int[] EyeGazeAccel = { 0, 0, 0, 5 };  // Maestro Accel ms  (Default, Slow, Fast)
            var leftLensH = new Servo(RobotControls.LeftLensHorizontal, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1350, 550, 2280, EyeGazeSpeed, EyeGazeAccel);
            var rightLensH = new Servo(RobotControls.RightLensHorizontal, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1480, 650, 2250, EyeGazeSpeed, EyeGazeAccel);
            var leftLensV = new Servo(RobotControls.LeftLensVertical, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1350, 550, 2250, EyeGazeSpeed, EyeGazeAccel);
            var rightLensV = new Servo(RobotControls.RightLensVertical, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1500, 550, 2250, EyeGazeSpeed, EyeGazeAccel);

            servos[(int)RobotControls.RightLensHorizontal] = rightLensH;
            servos[(int)RobotControls.LeftLensHorizontal] = leftLensH;
            servos[(int)RobotControls.LeftLensVertical] = leftLensV;
            servos[(int)RobotControls.RightLensVertical] = rightLensV;

            leftLensH.ConfigureSpeed(initialSpeed);
            leftLensV.ConfigureSpeed(initialSpeed);
            rightLensH.ConfigureSpeed(initialSpeed);
            rightLensV.ConfigureSpeed(initialSpeed);

            leftLensH.GoHome();
            leftLensV.GoHome();
            rightLensH.GoHome();
            rightLensV.GoHome();

            // Iris
            //int[] IrisSpeed = { 30, 10, 0, 10 };
            //int[] IrisAccel = { 12, 3, 0, 5 };
            int[] IrisSpeed = { 0, 0, 0, 10 };
            int[] IrisAccel = { 0, 0, 0, 5 };
            var leftIris = new Servo(RobotControls.LeftIris, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1575, 1350, 2000, IrisSpeed, IrisAccel);
            var rightIris = new Servo(RobotControls.RightIris, headPort, ServoMode.Stay, StartPosition.Home, Normal, 975, 650, 1350, IrisSpeed, IrisAccel);
            // 650/2 = 325.   Left 1675, Right 975
            // 
            // Old Iris home  Left=1525, Right = 925

            servos[(int)RobotControls.RightIris] = rightIris;
            servos[(int)RobotControls.LeftIris] = leftIris;

            leftIris.ConfigureSpeed(initialSpeed);
            rightIris.ConfigureSpeed(initialSpeed);

            leftIris.GoHome();
            rightIris.GoHome();

            // Brows
            int[] BrowsSpeed = { 60, 10, 0, 2 };
            int[] BrowsAccel = { 15, 5, 0, 3 };
            var browLeftTop = new Servo(RobotControls.BrowLeftTopOpen, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1513, 870, 1740, BrowsSpeed, BrowsAccel);//850
            var browRightTop = new Servo(RobotControls.BrowRightTopOpen, headPort, ServoMode.Stay, StartPosition.Home, Normal, 777, 550, 1330, BrowsSpeed, BrowsAccel);

            // Brow Left 1740 level with nose down
            // Brow Right 550 level with nose down

            // Home with Nose up partly 
            // Left 1533
            // Right 757  


            browLeftTop.EyePopSensitive = true;
            browRightTop.EyePopSensitive = true;

            servos[(int)RobotControls.BrowLeftTopOpen] = browLeftTop;
            servos[(int)RobotControls.BrowRightTopOpen] = browRightTop;

            browLeftTop.ConfigureSpeed(initialSpeed);
            browRightTop.ConfigureSpeed(initialSpeed);

            browLeftTop.GoHome();
            browRightTop.GoHome();

            int[] bBrowsSpeed = { 40, 10, 0, 2 };
            int[] bBrowsAccel = { 10, 5, 0, 3 };
            var browLeftTilt = new Servo(RobotControls.BrowLeftTopTilt, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1780, 900, 1820, bBrowsSpeed, bBrowsAccel);  // 1750
            var browRightTilt = new Servo(RobotControls.BrowRightTopTilt, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1100, 1020, 1970, bBrowsSpeed, bBrowsAccel);  // 1970)

            browLeftTilt.LimitUpper = 1950; // 180
            browRightTilt.LimitLower = 950; // 220
                                            //Different limits for Nose Up  Allow more downward tilt  will be upper limit for one and lower limit for the other
                                            //browLeftTilt.ModeLower =
                                            //browLeftTilt.ModeUpper =
                                            //browRightTilt.ModeLower =
                                            //browRightTilt.ModeUpper =

            browLeftTilt.EyePopSensitive = true;
            browRightTilt.EyePopSensitive = true;
            servos[(int)RobotControls.BrowLeftTopTilt] = browLeftTilt;
            servos[(int)RobotControls.BrowRightTopTilt] = browRightTilt;

            browLeftTilt.ConfigureSpeed(initialSpeed);
            browRightTilt.ConfigureSpeed(initialSpeed);

            browLeftTilt.GoHome();
            browRightTilt.GoHome();

            var browLeftBottom = new Servo(RobotControls.BrowLeftBottomOpen, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1800, 930, 2200, BrowsSpeed, BrowsAccel);
            //1236, 782, 2083,  -16
            var browRightBottom = new Servo(RobotControls.BrowRightBottomOpen, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1100, 662, 1963, BrowsSpeed, BrowsAccel);

            // Different limits for Eye Pop don't allow closing all the way will be upper limit for one and lower limit for the other
            // Left 1363
            // Right 1537
            //browLeftBottom.ModeLower =
            //browLeftBottom.ModeUpper =
            //browRightBottom.ModeLower =
            //browRightBottom.ModeUpper =

            browLeftBottom.EyePopSensitive = true;
            browRightBottom.EyePopSensitive = true;

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
            rightVent.GoHome();
            Thread.Sleep(500);

            servos[(int)RobotControls.LeftEyeVent] = leftVent;
            servos[(int)RobotControls.RightEyeVent] = rightVent;

            leftVent.ConfigureSpeed(initialSpeed);
            rightVent.ConfigureSpeed(initialSpeed);

            leftVent.DisableServo();
            rightVent.DisableServo();

            // MFRC
            int[] MFRCHSpeed = { 20, 40, 0, 10 };
            int[] MFRCHAccel = { 30, 20, 0, 20 };
            var mfrcRotate = new Servo(RobotControls.MFR_Rotate, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 1320, 800, 1865, MFRCHSpeed, MFRCHAccel);// 500 full left 2212 rull right  1320 centered dish front        
            int[] MFRCVSpeed = { 100, 50, 0, 50 };
            int[] MFRCVAccel = { 30, 25, 0, 25 };
            var mfrcUpDown = new Servo(RobotControls.MFR_UpDown, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 530, 530, 1100, MFRCVSpeed, MFRCVAccel);// 550 all the way down  1170 all the way up.
            servos[(int)RobotControls.MFR_UpDown] = mfrcUpDown;
            servos[(int)RobotControls.MFR_Rotate] = mfrcRotate;

            mfrcRotate.ConfigureSpeed(initialSpeed);
            mfrcUpDown.ConfigureSpeed(initialSpeed);

            mfrcRotate.GoHome();
            mfrcUpDown.GoHome();

            // Whip Antenna
            int[] WhipVSpeed = { 100, 50, 0, 50 };
            int[] WhipVAccel = { 30, 25, 0, 25 };
            var whipUpDown = new Servo(RobotControls.Whip_Antenna_RaiseLower, headPort, ServoMode.Stay, StartPosition.Home, Reversed, 2200, 1620, 2200, WhipVSpeed, WhipVAccel);//the way up 1560,  down 2200           

            int[] WhipHSpeed = { 0, 0, 0, 10 };
            int[] WhipHAccel = { 0, 0, 0, 20 };
            var whipRotate = new Servo(RobotControls.Whip_Antenna_Rotate, headPort, ServoMode.Stay, StartPosition.Home, Normal, 500, 500, 2300, WhipHSpeed, WhipHAccel);// 500 full left 2300 rull right 1400 Center
            whipUpDown.EyePopSensitive = true;
            whipRotate.EyePopSensitive = true;
            servos[(int)RobotControls.Whip_Antenna_Rotate] = whipRotate;
            servos[(int)RobotControls.Whip_Antenna_RaiseLower] = whipUpDown;

            whipUpDown.ConfigureSpeed(initialSpeed);
            whipRotate.ConfigureSpeed(initialSpeed);

            whipUpDown.GoHome();
            whipRotate.GoHome();

            // Mic
            int[] micSpeed = { 60, 50, 0, 50 };
            int[] micAccel = { 25, 25, 0, 25 };
            var microphone = new Servo(RobotControls.Microphone_RaiseLower, headPort, ServoMode.Stay, StartPosition.Home, Normal, 1681, 1681, 2298, micSpeed, micAccel);  //Mic          
            servos[(int)RobotControls.Microphone_RaiseLower] = microphone;
            microphone.ConfigureSpeed(initialSpeed);
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
