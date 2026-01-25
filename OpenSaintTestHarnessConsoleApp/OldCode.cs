using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TicT249Control;

namespace OpenSaintTestHarnessConsoleApp
{
    internal class OldCode
    {
                private static void RgbFeatures(RGBLight lights, int sleepTime)
        {
            lights.ClearAll();
            lights.Command("SetRGBColor, 255,0,0, 200,both,lr,0");

            // ColorWipeEyes, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms
            lights.Command("ColorWipeEyes,0,255,0,200,eyes,lr,40"); // works
            Thread.Sleep(2000);

            // Fade, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, (In,Out), step, lowest brightness
            lights.Command("Fade,0,255,0,200,both,lr,5,IN,1,0"); // work
            Thread.Sleep(2000);
            lights.Command("Fade,0,255,0,200,both,lr,5,OUT,1,0"); // work
            Thread.Sleep(2000);                                                    // Pulse, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, brightness step, lowest brightness
            lights.Command("Pulse,0,255,0,100,both,lr,5,3,1,0");  //work 
            Thread.Sleep(2000);
            // eyes only
            // TheaterChase, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, cycles
            lights.Command("TheaterChase,0,255,0,100,eyes,lr,40,10"); //work

            Thread.Sleep(2000);                                                   // Rainbow, brightness(0-255), (left,right, lr), delayms
            lights.Command("Rainbow,150,lr,20");  // works
            Thread.Sleep(2000);                                      // RainbowCycle, brightness(0-255), (left,right, lr), delayms, cycles
            lights.Command("RainbowCycle,150,lr,20"); //works
            Thread.Sleep(2000);                                      // RainbowChase, brightness(0-255), (left,right, lr), delayms
            lights.Command("RainbowChase,150,lr,20");  // works
            Thread.Sleep(2000);
            lights.Command("ClearAll");
        }



        #region old storage

        /// <summary>
        /// Sets the default per channel Maestro servo settings
        /// </summary>
        /// <param name="headPort">USB Port</param>
        private static void SetMaestroSettings(string headPort)
        {
            //| Servo | Suggested Speed | Suggested Accel |
            //| ------------ | --------------- | --------------- |
            //| **ASME - 04B * * | 80 – 150 | 20 – 40 |
            //| **HS - 85 * *    | 30 – 60 | 10 – 20 |
            //| **HS - 40 * *    | 20 – 40 | 8 – 15 |

            // Neck
            Servo.ConfigureChannel(headPort, (int)RobotControls.NeckTiltLeft, 90, 35);
            Servo.ConfigureChannel(headPort, (int)RobotControls.NeckTiltRight, 90, 35);
            Servo.ConfigureChannel(headPort, (int)RobotControls.NeckTurn, 90, 10);

            // Nose
            Servo.ConfigureChannel(headPort, (int)RobotControls.NoseBody, 100, 30);
            Servo.ConfigureChannel(headPort, (int)RobotControls.NoseBasket, 100, 30);

            // eyes
            Servo.ConfigureChannel(headPort, (int)RobotControls.LeftIris, 30, 12);
            Servo.ConfigureChannel(headPort, (int)RobotControls.RightIris, 30, 12);
            Servo.ConfigureChannel(headPort, (int)RobotControls.LeftLensVertical, 40, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.LeftLensHorizontal, 40, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.RightLensVertical, 40, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.RightLensHorizontal, 40, 15);

            //Vents
            Servo.ConfigureChannel(headPort, (int)RobotControls.LeftEyeVent, 30, 12);
            Servo.ConfigureChannel(headPort, (int)RobotControls.RightEyeVent, 30, 12);

            //brows
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowLeftBottomOpen, 60, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowRightBottomOpen, 60, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowLeftTopOpen, 60, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowRightTopOpen, 60, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowLeftTopTilt, 60, 15);
            Servo.ConfigureChannel(headPort, (int)RobotControls.BrowRightTopTilt, 60, 15);

            // MFRC  need to test
            Servo.ConfigureChannel(headPort, (int)RobotControls.MFR_UpDown, 100, 30);
            Servo.ConfigureChannel(headPort, (int)RobotControls.MFR_Rotate, 5, 30);

            // Whip Antenna
            Servo.ConfigureChannel(headPort, (int)RobotControls.Whip_Antenna_RaiseLower, 100, 30);
            Servo.ConfigureChannel(headPort, (int)RobotControls.Whip_Antenna_Rotate, 5, 30);

            //microphone
            Servo.ConfigureChannel(headPort, (int)RobotControls.Microphone_RaiseLower, 100, 30);
        }


        //private static void EyeFeatures(string headPort, int sleepTime)
        //{
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 1350); // Open Left Iris  1350 close 2000
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 650); // open Right Iris 650, 1350  closed iris 
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 2000);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 1290);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 1350);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 650);
        //    Thread.Sleep(sleepTime);
        //    // Iris partly open   
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 1525); // Open Left Iris  1350 close 2000
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 925); // open Right Iris 650, 1350  closed iris 
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftIris);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightIris);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal  
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480); // Right Eye Horisontal
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 2280); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 2250);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 550); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 650);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480);
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensHorizontal);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensHorizontal);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500); // Right Eye Vertical
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 2250); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 550);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 550); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 2250);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500);
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensVertical);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensVertical);
        //}

        //private static void NoseFeatures(string headPort, int sleepTime)
        //{
        //    //Nose
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1450);  // Nose down 1600, up 650
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 850);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1440);
        //    Thread.Sleep(sleepTime);


        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 900); //900 down centered, 1250 up     
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 1250);// up 1250 // down 675 // centered 900
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 675);
        //    Thread.Sleep(sleepTime);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1400); // Nose up?
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 900); //900 centered, 1250 up     
        //    Thread.Sleep(sleepTime);

        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBody);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBasket);
        //}

        //private static void BrowFeatures(string headPort, int sleepTime)
        //{
        //    // Brow Tilt      
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 882);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1970);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1090);
        //    Thread.Sleep(sleepTime);

        //    // Close all 4 Brows
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1100);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1800);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 910); //closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 1298);
        //    Thread.Sleep(sleepTime);

        //    // Open Level Brows
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 2100);// bottom brow 2000 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1200);// Brow 1150 open  1950 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550);
        //    Thread.Sleep(sleepTime);

        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopOpen);
        //}

        //private static void TopFeatures(string headPort, int sleepTime)
        //{
        //    //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftEyeVent, 1750); // Closed Left Vent 1750 OPen 2200 closed     
        //    //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1150); // Closed Right Vent 1121 Open 749
        //    //Thread.Sleep(sleepTime);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftEyeVent, 2200);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 600);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftEyeVent, 1750);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1121);
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftEyeVent);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightEyeVent);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_UpDown, 1250); // 655 all the way down  1250 all the way up.
        //    Thread.Sleep(sleepTime);     
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_Rotate, 550); // 550 full left 2240 rull right  1380 centered dish front
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_Rotate, 2240); // 550 full left 2240 rull right  1380 centered dish front
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_Rotate, 1380); // 550 full left 2240 rull right  1380 centered dish front
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.MFR_Rotate);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_UpDown, 670); // 655 all the way down  1250 all the way up.
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.MFR_UpDown);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_RaiseLower, 700);  

        //    //Center
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_Rotate, 1400); // 500 full left 2300 rull right
        //    Thread.Sleep(sleepTime);
        //    // Left
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_Rotate, 500); // 500 full left 2300 rull right
        //    Thread.Sleep(sleepTime);
        //    // Right
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_Rotate, 2300); // 500 full left 2300 rull right
        //    Thread.Sleep(sleepTime);
        //    // Center
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_Rotate, 1400); // 500 full left 2300 rull right
        //    Servo.DisableServo(headPort, (int)RobotControls.Whip_Antenna_Rotate);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_RaiseLower, 800); // 1400 all the way down  680 all the way up.
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_RaiseLower, 1400); // 1400 all the way down  680 all the way up.
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.Whip_Antenna_RaiseLower);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Microphone_RaiseLower, 1700); // 1050 down 1700 up
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Microphone_RaiseLower, 1050); // 1050 down 1700 up
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.Microphone_RaiseLower);

        //}

        //private static void NeckTest(string headPort, int sleepTime)
        //{

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 900);  // 940 Left    1306 center  1740  Right
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1700);  // 940 Left    1306 center  1740  Right
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1295);
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    //Servo.DisableServo(headPort, (int)RobotControls.NeckTurn);

        //    // Neck Tilt Center

        //    // Tilt down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1600);  // 1600 down   1281 up        center  1435 
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1674); // 1694 down  1454 up  Center 1574
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    // Tilt Up
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1250);  // 1600 down   1281 up        center  1435 
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1454); // 1694 down  1454 up  Center 1574
        //    Thread.Sleep(sleepTime);
        //    Thread.Sleep(sleepTime);
        //    // Telt Center
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1435);  // 1600 down   1281 up        center  1435 
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1574); // 1694 down  1454 up  Center 1574
        //    Thread.Sleep(sleepTime);

        //    //Servo.DisableServo(headPort, (int)RobotControls.NeckTiltLeft);
        //    //Servo.DisableServo(headPort, (int)RobotControls.NeckTiltRight);
        //}

        //private static void SetStartPostion(RGBLight lights, string headPort, TicController? leftTic, TicController? rightTic)
        //{
        //    // blue eyes and  Vents
        //    lights.ClearAll();
        //    lights.SetRGBColor(0, 0, 255, 150, RGBLight.Ring.Both, RGBLight.Side.LR);

        //    //Neck Rotate Center
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1306);  // 940 Left    1306 center  1740  Right
        //    // Center Brows
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1435);  // 1600 down   1281 up        center  1435 
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1574); // 1694 down  1454 up  Center 1574

        //    // Nose up Basket Centered
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1400); // Nose up?
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 900); //900 centered, 1250 up     

        //    // Open Level Brows
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 2100);// bottom brow 2000 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1200);// Brow 1150 open  1950 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550);

        //    // Neck tilt up and down and Center
        //    // Neck rotate Left and Right and Center
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750); // left tilt 1750 center, 882 vertical, 2000 tilt down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1100); // tilt 1070 center, 1970 vertical, 760 tilt down              

        //    // Center Eyes
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal  
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480); // Right Eye Horisontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500); // Right Eye Vertical

        //    // Iris partly open   
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 1525); // Open Left Iris  1350 close 2000
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 925); // open Right Iris 650, 1350  closed iris 

        //    // MFRC
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_Rotate, 1380); // 550 full left 2240 rull right  1380 centered dish front
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.MFR_UpDown, 670); // 655 all the way down  1250 all the way up.

        //    // Whip
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_RaiseLower, 1400); // 1400 all the way down  680 all the way up.
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Whip_Antenna_Rotate, 1400); // 500 full left 2300 rull right 1400 Center

        //    //Mic      
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.Microphone_RaiseLower, 1070); // 1050 down 1700 up

        //    // Vents
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftEyeVent, 1750); // Closed Left Vent 1750 OPen 2200 closed     
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1150); // Closed Right Vent 1121 Open 749

        //    // Stop buzzing
        //    Servo.DisableServo(headPort, (int)RobotControls.NeckTurn);
        //    Servo.DisableServo(headPort, (int)RobotControls.NeckTiltLeft);
        //    Servo.DisableServo(headPort, (int)RobotControls.NeckTiltRight);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBody);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBasket);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensHorizontal);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensHorizontal);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensVertical);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensVertical);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftIris);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightIris);
        //    Servo.DisableServo(headPort, (int)RobotControls.MFR_Rotate);
        //    Servo.DisableServo(headPort, (int)RobotControls.MFR_UpDown);
        //    Servo.DisableServo(headPort, (int)RobotControls.Whip_Antenna_Rotate);
        //    Servo.DisableServo(headPort, (int)RobotControls.Whip_Antenna_RaiseLower);
        //    Servo.DisableServo(headPort, (int)RobotControls.Microphone_RaiseLower);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftEyeVent);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightEyeVent);

        //    // Move eye Pop back.
        //    long position = 0;
        //    rightTic.MoveToPosition(position);
        //    leftTic.MoveToPosition(position);
        //}

        //public static IHostBuilder CreateHostBuilder(string[] args) =>
        //    Host.CreateDefaultBuilder(args)
        //        .ConfigureWebHostDefaults(webBuilder =>
        //        {
        //            webBuilder.UseStartup<Startup>();
        //            webBuilder.UseUrls("http://localhost:8080");
        //        });

        static void FunctionCheck(string headPort, string neckPort, RGBLight lights, TicController leftTic, TicController rightTic)
        {
            lights.Command("ClearAll"); // Need to send an initial string of any kind to initialize the loop.      
            lights.SetRGBColor(0, 255, 0, 150, RGBLight.Ring.Both, RGBLight.Side.LR);

            //InitServos(headPort, neckPort, lights);
            //  InitEyePop(leftSN, rightSN);

            lights.SetRGBColor(0, 0, 255, 150, RGBLight.Ring.Both, RGBLight.Side.LR);
            //RangeofMotionServos(headPort, neckPort);

            // RangeofMotionEyePop(leftTic,rightTic);
        }


        static void InitEyePop(TicController leftTic, TicController rightTic)
        {
            var position = 0;
            leftTic.MoveToPosition(position);
            rightTic.MoveToPosition(position);
        }

        static void RangeofMotionEyePop(TicController leftTic, TicController rightTic)
        {
            int position = 0;
            leftTic.MoveToPosition(position);
            rightTic.MoveToPosition(position);
            Thread.Sleep(1000);
            position = 2000;
            leftTic.MoveToPosition(position);
            rightTic.MoveToPosition(position);
            Thread.Sleep(1000);
            position = 0;
            leftTic.MoveToPosition(position);
            rightTic.MoveToPosition(position);
        }


        //    static void InitServos(string headPort, string neckPort, RGBLight lights)
        //{       
        //    // Left Eye
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftEyeVent, 1750); // Closed Vent 1750 OPen 2200
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 2000); // Open Iris  2000, closed iris 1350
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical

        //    // Right Eye
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1121); // Closed Vent 1121 Open 749
        //    //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 1350); // open Iris,  closed iris 650    
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500); // Right Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480); // Right Eye Horisontal

        //    // Nose
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 2250);  // Nose down 2250, up 1600
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 862); // 862 centered, 1250 up, down 790

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1600); // nose 1600 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1300); // basket 1300 open  1950 Closed

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750); // left tilt 1750 center, 882 vertical, 2000 tilt down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1070); // tilt 1070 center, 1970 vertical, 760 tilt down

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740); // left open 1740 center, open 1950, closed 910  may need to move servo arm
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550); // tilt open 550, open 550, closed 1298 ( matches left )

        //    // Neck
        //    Servo.WritePwmMicroseconds(neckPort, (int)RobotControls.RobotControls.NeckTurn 650);
        //    Servo.WritePwmMicroseconds(neckPort, (int)RobotControls.NeckTiltLeft, 1350);
        //    Servo.WritePwmMicroseconds(neckPort, (int)RobotControls.NeckTiltRight, 1310);
        //}

        // static void RangeofMotionServos(string headPort, string neckPort)
        //{
        //    // Left Eye
        //    RangeTest(1750, 1750, 2200, headPort, (int)RobotControls.LeftEyeVent, false);
        //    RangeTest(1121, 1121, 749, headPort, (int)RobotControls.RightEyeVent, true);

        //    RangeTest(2000, 1350, 2000, headPort, (int)RobotControls.LeftIris, true);
        //    RangeTest(1350, 650, 1350, headPort, (int)RobotControls.RightIris, true);


        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical

        //    // Right Eye
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1121); // Closed Vent 1121 Open 749
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 1350); // open Iris,  closed iris 650    
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500); // Right Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480); // Right Eye Horisontal

        //    // Nose
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 2250);  // Nose down 2250, up 1600
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 862); // 862 centered, 1250 up, down 790

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1600); // nose 1600 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1300); // basket 1300 open  1950 Closed

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750); // left tilt 1750 center, 882 vertical, 2000 tilt down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1070); // tilt 1070 center, 1970 vertical, 760 tilt down

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740); // left open 1740 center, open 1950, closed 910  may need to move servo arm
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550); // tilt open 550, open 550, closed 1298 ( matches left )

        //    // Neck
        //    //Servo.WritePwmMicroseconds(neckPort, (int)NeckServos.RobotControls.NeckTurn 650);
        //    //Servo.WritePwmMicroseconds(neckPort, (int)NeckServos.NeckTiltLeft, 1350);
        //    //Servo.WritePwmMicroseconds(neckPort, (int)NeckServos.NeckTiltRight, 1310);
        //}

        // static void RangeTest(int startEnd, int lowValue, int highValue, string headPort, int channel, bool isReverse)
        //{
        //    var sleepMs = 50;

        //    var low = lowValue;
        //    var high = highValue;

        //    if (isReverse) {
        //        high = lowValue;
        //        low = highValue;
        //    }

        //    if (startEnd != low)
        //    {            
        //        var diff = (startEnd - low) / 10;
        //        for (int i = startEnd; i > low; i -= diff)
        //        {
        //            Servo.WritePwmMicroseconds(headPort, channel, i);
        //            Thread.Sleep(sleepMs);
        //        }
        //    }
        //    var steps = (high - low)/50;

        //    for (int i = low; i < high; i += steps)
        //    {
        //        Servo.WritePwmMicroseconds(headPort, channel, i);
        //        Thread.Sleep(sleepMs);
        //    }

        //    for (int i = high; i > startEnd; i -= steps)
        //    {
        //        Servo.WritePwmMicroseconds(headPort, channel, i);
        //        Thread.Sleep(sleepMs);
        //    }      
        //}

        #endregion


        #region tempSave


        //lights.ClearAll();
        //Thread.Sleep(sleepTime);
        //lights.SetRGBColor(0, 0, 255, 150, RGBLight.Ring.Both, RGBLight.Side.LR);
        //Thread.Sleep(sleepTime);
        //lights.Command("ColorWipeEyes,0,255,0,200,eyes,lr,40");

        //// Iris part way closed.
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftIris, 1525); // Open Left Iris  1350 close 2000
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightIris, 925); // open Right Iris 650, 1350  closed iris 
        //Thread.Sleep(sleepTime);

        //// Neck Rotation
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1306);  // 940 Left    1306 center  1740  Right
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 940);  // 940 Left    1306 center  1740  Right
        //Thread.Sleep(sleepTime);
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1306);
        //Thread.Sleep(sleepTime);
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1740);  // 940 Left    1306 center  1740  Right
        //Thread.Sleep(sleepTime);
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RobotControls.NeckTurn 1306);
        //Thread.Sleep(sleepTime);

        //// Neck Tilt Center
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1435);  // 1600 down   1281 up        center  1435 
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1574); // 1694 down  1454 up  Center 1574
        //Thread.Sleep(sleepTime);
        //// Tilt down
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1550);  // 1600 down   1281 up        center  1435 
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1644); // 1694 down  1454 up  Center 1574
        //Thread.Sleep(sleepTime);
        //// Tilt Up
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1331);  // 1600 down   1281 up        center  1435 
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1504); // 1694 down  1454 up  Center 1574
        //Thread.Sleep(sleepTime);
        //// Telt Center
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltLeft, 1435);  // 1600 down   1281 up        center  1435 
        //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NeckTiltRight, 1574); // 1694 down  1454 up  Center 1574
        //Thread.Sleep(sleepTime);

        //Servo.DisableServo(headPort, (int)RobotControls.NeckTurn);
        //Servo.DisableServo(headPort, (int)RobotControls.NeckTiltLeft);
        //Servo.DisableServo(headPort, (int)RobotControls.NeckTiltRight);  


        //    // Center Points      
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 2250);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 910); //closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 1298);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1032);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1900);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1090);

        //    // Set Top brows center          
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480);
        //    // Set bottom brows down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550);
        //    // Set Tilt level
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1090);
        //    // Move Nose Basket up.
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 650);// open 650// closed 1450
        //    Servo.EnableChannel(headPort, (int)RobotControls.NoseBasket, 1150);

        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBasket);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBody);
        //    // Servo.SetChannelOff(headPort, (int)RobotControls.NoseBody);






        #endregion

        #region tempSave2
        //    servos.Add(new Servo(RobotControls.LeftEyeVent, headPort, 8, ServoMode.Stay, StartPosition.Min, false, ServoType.HS85MG, 1750, 1750, 2200));

        //    // Symetrical test.
        //    //for (int i = 0; i < 100; i++)
        //    //{
        //    //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 1150); // Closed Right Vent 1121 Open 749
        //    //    Thread.Sleep(betweenTime);
        //    //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightEyeVent, 560);
        //    //    Thread.Sleep(betweenTime);
        //    //}


        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal  
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480); // Right Eye Horisontal
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 2280); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 2250);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 550); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 650);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensHorizontal, 1350); // Left Eye Horizontal
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensHorizontal, 1480);
        //    Thread.Sleep(betweenTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensHorizontal);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensHorizontal);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500); // Right Eye Vertical
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 2250); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 550);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 550); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 2250);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.LeftLensVertical, 1350); // Left Eye Vertical
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.RightLensVertical, 1500);
        //    Thread.Sleep(betweenTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.RightLensVertical);
        //    Servo.DisableServo(headPort, (int)RobotControls.LeftLensVertical);


        //    //Nose
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1450);  // Nose down 1600, up 650
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 650);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBody, 1440);
        //    Thread.Sleep(sleepTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBody);

        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 900); //900 down centered, 1250 up     
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 1150);// open 1150 // closed 675
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.NoseBasket, 675);
        //    Thread.Sleep(betweenTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.NoseBasket);



        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750); // left tilt 1750 center, 882 vertical, 2000 tilt down
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1090); // tilt 1070 center, 1970 vertical, 760 tilt down
        //                                                                                  //Thread.Sleep(sleepTime);       
        //                                                                                  //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1780);
        //                                                                                  //Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1040);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 882);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1970);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopTilt, 1750);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopTilt, 1090);
        //    Thread.Sleep(betweenTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopTilt);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopTilt);


        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740); // left open 1740 center, open 1950, closed 910  may need to move servo arm
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550); // tilt open 550, open 550, closed 1298 ( matches left )
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1800);// nose 1800 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1150);// basket 1150 open  1950 Closed
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 910); //closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 1298);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1032);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1900);
        //    Thread.Sleep(sleepTime);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftTopOpen, 1740);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightTopOpen, 550);
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowLeftBottomOpen, 1800);// nose 1800 open 1022 Closed
        //    Servo.WritePwmMicroseconds(headPort, (int)RobotControls.BrowRightBottomOpen, 1150);// basket 1150 open  1950 Closed
        //    Thread.Sleep(betweenTime);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightTopOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowRightBottomOpen);
        //    Servo.DisableServo(headPort, (int)RobotControls.BrowLeftBottomOpen);
        //    // Lower Brows


        //    Thread.Sleep(sleepTime);

        // Move them to one position
        //foreach (var device in devices)
        //{
        //    if (device.Type == USBDeviceType.TicT249)
        //    {

        //        if (device.SerialNumber == "00475552")
        //        {
        //            position = -1100;
        //         //   TicController.MoveToPosition(device.SerialNumber, position);
        //        }
        //        else
        //        {
        //            position = -1200;
        //            TicController.MoveToPosition(device.SerialNumber, position);
        //        }
        //    }
        //}
        //foreach (var device in devices)
        //{
        //    if (device.Type == USBDeviceType.TicT249)
        //    {
        //         position = 0;
        //        if (device.SerialNumber == "00475552")
        //        {
        //            position = 1100;
        //           // TicController.MoveToPosition(device.SerialNumber, position);
        //        }
        //        else
        //        {
        //            position = 1200;
        //            TicController.MoveToPosition(device.SerialNumber, position);
        //        }
        //    }
        //}
        //NeoPixel  RGB Lights

        //D7 L Eye
        //D8 L Vent
        //D9 R Eye
        //D10 R Vent


        //Setup Head


        //case ServoType.HS40:
        //           this.minμsec = 615;
        //           this.maxμsec = 2495;
        //           this.degreeRange = 195;
        //           break;

        //       case ServoType.HS85BB:
        //       case ServoType.HS85MG:
        //           this.minμsec = 553;
        //           this.maxμsec = 2300;
        //           this.degreeRange = 182;
        //           break;

        //       case ServoType.HS5496:
        //           this.minμsec = 750;
        //           this.maxμsec = 2250;
        //           this.degreeRange = 117;
        //           break;

        //       case ServoType.HS5645:
        //           this.minμsec = 1500;
        //           this.maxμsec = 1900;
        //           this.degreeRange = 45;
        //           break;

        //       case ServoType.Gobuilda25_502:
        //           this.minμsec = 500;
        //           this.maxμsec = 2500;
        //           this.degreeRange = 1800;
        //           break;

        //       case ServoType.AMSE_04B: // 50Hz signal
        //       case ServoType.AMSE_05B:
        //           this.minμsec = 1000; // Could be 500-2500us will need to test
        //           this.maxμsec = 2000;
        //           this.degreeRange = 180;
        //           break;

        //        Left Joystick(neck )
        //UpDown Tilt forward / backwards neck up and down nodding
        //Rotate left / Right turn head
        //Gang eye articulation by default to look in the direction of rotation, recenter when motion stopped.

        //Right Joystick U / D for eye flaps and integrated nose movements Up / down along with lower brows open close, L / R tilts upper brows.

        //Left Trigger - Iris Control. (0 - 256) Open / close both irises
        //Right Trigger – Eye pop(0 - 256 fully extended)eyes ganged together(model knows to move flaps out of the way, turn off angry mode if on)

        //                Button A(toggle) for angry mode for Red eyes and 45 degree tilt upper brows.
        //                Button X(toggle) for MFRC(will raise fully and rotate until clicked again)
        //                Button B(toggle) for Whip(will raise fully and rotate until clicked again)
        //                Button Y – (toggle)raise / lower microphone
        //                D - Pad Up – open both vents, Down close both Vents, Left Red light, Right Blue light


        //                Multiplex Left rear Button(independent gaze control)
        //                Right Joystick controls Eye gaze U / D / L / R(assume head is still to show off eyes moving)


        //                Multiplex Right rear Button(no rotation, additional tilt control)
        //                Left Joystick L / R tilt neck sideways, U / D set center of tilt.





        //var lowerLimit = 553;
        //var upperLimit = 2300;
        #endregion



        //// move to Stepper Motor section
        //private static void SendTic(string serialNumber, string argument)
        //{
        //    ProcessStartInfo psi = new ProcessStartInfo
        //    {
        //        FileName = "ticcmd.exe",
        //        Arguments = string.Format("-d {0} {1}", serialNumber, argument),
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };

        //    using (var process = Process.Start(psi))
        //    {
        //        string output = process.StandardOutput.ReadToEnd();
        //        Console.WriteLine(output);
        //    }
        //}

        //private static void MoveStepperToPosition(string serialNumber, long position)
        //{
        //    SendTic(serialNumber, "--energize");
        //    SendTic(serialNumber, "--step-mode 1/16");
        //    SendTic(serialNumber, "--max-speed 200000");
        //    SendTic(serialNumber, "--max-accel 100000");
        //    SendTic(serialNumber, string.Format("-p {0}", position));
        //}

    }


}


//public class Startup
//{
//    public void ConfigureServices(IServiceCollection services)
//    {
//        services.AddCors(options =>
//        {
//            options.AddPolicy("AllowAll", builder =>
//            {
//                builder.AllowAnyOrigin()
//                       .AllowAnyMethod()
//                       .AllowAnyHeader();
//            });
//        });
//    }

//    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
//    {
//        if (env.IsDevelopment())
//        {
//            app.UseDeveloperExceptionPage();
//        }

//        app.UseCors("AllowAll");
//        app.UseWebSockets();
//        app.UseRouting();

//        app.Use(async (context, next) =>
//        {
//            if (context.Request.Path == "/controller")
//            {
//                if (context.WebSockets.IsWebSocketRequest)
//                {
//                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
//                    await HandleWebSocketConnection(webSocket);
//                }
//                else
//                {
//                    context.Response.StatusCode = 400;
//                }
//            }
//            else
//            {
//                await next();
//            }
//        });

//        app.UseEndpoints(endpoints =>
//        {
//            endpoints.MapGet("/", async context =>
//            {
//                await context.Response.WriteAsync("SteamDeck Controller WebSocket Server is running!");
//            });
//        });
//    }

//    private async Task HandleWebSocketConnection(WebSocket webSocket)
//    {
//        var buffer = new byte[1024 * 4];
//        Console.WriteLine("WebSocket connection established!");

//        try
//        {
//            while (webSocket.State == WebSocketState.Open)
//            {
//                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

//                if (result.MessageType == WebSocketMessageType.Text)
//                {
//                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
//                    ProcessControllerInput(message);
//                }
//                else if (result.MessageType == WebSocketMessageType.Close)
//                {
//                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
//                    Console.WriteLine("WebSocket connection closed.");
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"WebSocket error: {ex.Message}");
//        }
//    }

//    private void ProcessControllerInput(string jsonMessage)
//    {
//        try
//        {
//            var input = JsonConvert.DeserializeObject<ControllerInput>(jsonMessage);

//            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Controller Input:");
//            Console.WriteLine($"  Type: {input.Type}");
//            Console.WriteLine($"  Timestamp: {input.Timestamp}");
//            Console.WriteLine($"  Data: {JsonConvert.SerializeObject(input.Data, Newtonsoft.Json.Formatting.Indented)}");
//            Console.WriteLine();

//            // Process different input types
//            switch (input.Type)
//            {
//                case "button":
//                    ProcessButtonInput(input.Data);
//                    break;
//                case "axis":
//                    ProcessAxisInput(input.Data);
//                    break;
//                case "touchpad":
//                    ProcessTouchpadInput(input.Data);
//                    break;
//                case "gyro":
//                    ProcessGyroInput(input.Data);
//                    break;
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Error processing input: {ex.Message}");
//        }
//    }

//    private void ProcessButtonInput(object data)
//    {
//        // Handle button press/release events
//        // Implement your button handling logic here
//    }

//    private void ProcessAxisInput(object data)
//    {
//        // Handle analog stick and trigger movements
//        // Implement your axis handling logic here
//    }

//    private void ProcessTouchpadInput(object data)
//    {
//        // Handle touchpad interactions
//        // Implement your touchpad handling logic here
//    }

//    private void ProcessGyroInput(object data)
//    {
//        // Handle gyroscope/accelerometer data
//        // Implement your gyro handling logic here
//    }
//}



//    static int ServoFromJoystick(
//                                    int joyValue,            // -1..+1
//                                    int uMin,           // microseconds
//                                    int uHome,          // microseconds
//                                    int uMax,           // microseconds
//                                    float deadzone = 0, // 0..0.3 typical
//                                    float expo = 0      // -1..+1 (0=linear, >0 softer center)
//)
//    {
//        float j = (float)joyValue / (float)32767;

//        // 1) deadzone
//        float a = MathF.Abs(j);
//        if (a < deadzone) j = 0f;
//        else
//        {
//            float s = MathF.Sign(j);
//            j = s * (a - deadzone) / (1f - deadzone); // re-scale to 0..1
//        }

//        // 2) optional exponential curve (cubic blend)
//        if (expo != 0f)
//            j = j * (1 - expo) + j * j * j * expo;

//        // 3) asymmetric map around home
//        float posSpan = uMax - uHome;  // >= 0
//        float negSpan = uHome - uMin;  // >= 0

//        float up = MathF.Max(0, j) * posSpan;
//        float un = MathF.Min(0, j) * negSpan; // j is negative here

//        float u = uHome + up + un;

//        // 4) clamp to safety
//        if (u < uMin) u = uMin;
//        if (u > uMax) u = uMax;

//        return (int)MathF.Round(u);
//    }

