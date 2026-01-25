using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenSaintTestHarnessConsoleApp
{
    public static class ControllerMapping
    {
        public static IOMapping SetXBoxMapping()
        {
            // Generate Multiplexed Mapping
            var maps = new List<IOMap>();        

            maps = Add_Default_MUX(maps);

            maps = Add_L_MUX(maps);

            maps = Add_R_MUX(maps);

            maps = Add_LR_MUX(maps);

            // These Mappings are across all MUX combinations.
                           
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default, MultiplexInput.L_Shoulder, MultiplexInput.R_Shoulder }, XBoxControlNames.End,
                new List<Output> { new Output( new List<ButtonActions>{ButtonActions.DisableAllServos })
            }));
                  
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default, MultiplexInput.L_Shoulder, MultiplexInput.R_Shoulder }, XBoxControlNames.Start,
                new List<Output> { new Output( new List<ButtonActions>{ButtonActions.Snapshot })
            }));
                           
            maps.Add(new IOMap(new List<MultiplexInput> {  MultiplexInput.LR_Shoulder }, XBoxControlNames.Start,
                new List<Output> { new Output( new List<ButtonActions>{ButtonActions.SnapshotSeries })
            }));

            var mappings = new IOMapping();

            // Add mappings to each mux group so only the mappings for each are processed at any given time.
            foreach (IOMap map in maps)
            {
                if (map.Multiplex.Contains(MultiplexInput.Default))
                {
                    mappings.Default.Add(map);
                }
                if (map.Multiplex.Contains(MultiplexInput.L_Shoulder))
                {
                    mappings.L_Shoulder.Add(map);
                }
                if (map.Multiplex.Contains(MultiplexInput.R_Shoulder))
                {
                    mappings.R_Shoulder.Add(map);
                }
                if (map.Multiplex.Contains(MultiplexInput.LR_Shoulder))
                {
                    mappings.LR_Shoulder.Add(map);
                }
            }

            return mappings;
        }


        /// <summary>
        /// These are the beginnings of new Ganged servo logic to simply the Controller Mapping.
        ///  
        /// Currently Unused
        /// </summary>
        /// <returns></returns>
        public static new List<GangedServo> EyesHorizontalNeckRotate()
        {
            var outputs = new List<GangedServo> {            
            // Left Joystick X moves eyes first
            new GangedServo(RobotControls.LeftLensHorizontal, MultiOutput.Reversed),
            new GangedServo(RobotControls.RightLensHorizontal, MultiOutput.Reversed),
            new GangedServo(RobotControls.NeckTurn, MultiOutput.Normal)
            };
            return outputs;
        }

        public static new List<GangedServo> FocusChange()
        {
            var outputs = new List<GangedServo> {            
            // Left Joystick X moves eyes first
            new GangedServo(RobotControls.LeftIris, MultiOutput.Normal),
            new GangedServo(RobotControls.RightIris, MultiOutput.Normal),            
            };
            return outputs;
        }

        public static new List<GangedServo> EyesVerticalNeckTilt()
        {
            var outputs = new List<GangedServo> {
          // Left Joystick Y moves Eyes first.
          new GangedServo(RobotControls.LeftLensVertical, MultiOutput.Normal),
          new GangedServo(RobotControls.RightLensVertical, MultiOutput.Reversed),

          // Neck tilt is delayed by 100ms because the hyraulics take longer to move.
          new GangedServo(RobotControls.NeckTiltRight, MultiOutput.Reversed), //, tiltDelay),
          new GangedServo(RobotControls.NeckTiltLeft, MultiOutput.Normal), //, tiltDelay),       
            };
            return outputs;
        }

        public static new List<GangedServo> BrowsAll()
        {
            // Default operation
            var outputs = new List<GangedServo> {
            new GangedServo(RobotControls.BrowLeftTopOpen, MultiOutput.Normal),
            new GangedServo(RobotControls.BrowLeftBottomOpen, MultiOutput.Normal),
            new GangedServo(RobotControls.BrowRightTopOpen, MultiOutput.Reversed),
            new GangedServo(RobotControls.BrowRightBottomOpen, MultiOutput.Reversed)
            };
            return outputs;
        }

        public static new List<GangedServo> BrowTilt()
        {
            var outputs = new List<GangedServo> {
          new GangedServo(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed),
          new GangedServo(RobotControls.BrowRightTopTilt, MultiOutput.Normal),

            };

            return outputs;
        }

        public static List<IOMap> Add_Default_MUX(List<IOMap> maps)
        {
            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.LTrigger, new List<Output> {
            //new Output(RobotControls.Microphone_RaiseLower, MultiOutput.Normal),   }));

            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RTrigger, new List<Output> {
            //new Output(RobotControls.LeftEyeVent, MultiOutput.Normal),
            //new Output(RobotControls.RightEyeVent, MultiOutput.Reversed), }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.LTrigger, new List<Output> {
            new Output(RobotControls.LeftEyePop, MultiOutput.Normal), }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RTrigger, new List<Output> {
            new Output(RobotControls.RightEyePop, MultiOutput.Normal), }));


            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.DPadUp, new List<Output> {
            new Output(RobotControls.NoseBody, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }) }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.DpadDown, new List<Output> {
            new Output(RobotControls.NoseBasket , MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }) }));

            // Reset Face to Start
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.DPadLeft, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.EyePopClosed }),
            new Output(RobotControls.NeckTiltRight, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.NeckTiltLeft, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),

            new Output(RobotControls.LeftLensHorizontal, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.LeftLensVertical, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightLensHorizontal, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightLensVertical, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),

            new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.LeftIris, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightIris, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"ClearAll"}),

            new Output(ButtonActions.DisableAllRunningServos,1000),
            //new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.DisableServo })
        }));

            // Dpad Right BSOD
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.DPadRight, new List<Output>
        {
             new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"TheaterChase,0,0,255,100,Eyes,LR,40,60"}),
             new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax}),
             new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
             new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax }),
           //  new Output(null, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.EyePopLeftOpen}),
        }));

            // End button taken at top.

        //    // Eye Pop Toggle
        //    maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.Start, new List<Output> {

        //    new Output(new List<ButtonActions>{ButtonActions.Snapshot })
        //}));

            // RGB Clear ALL
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RThumb, new List<Output> {
             new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"Clear,both,lr"}) }));

            // Eye Pop Toggle
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.Y, new List<Output> {
             new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Default, RunOrder.Before ),
            new Output( new List<ButtonActions>{ButtonActions.EyePopOpen, ButtonActions.EyePopClosed }) }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.X, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Default, RunOrder.Before ),
            new Output(new List<ButtonActions>{ButtonActions.EyePopHalfOpen, ButtonActions.EyePopClosed }) }));

            // Close eyes
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.A, new List<Output> {
            // Nose down
            new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            // Brows tilted
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),           
            // Brow Top closed
            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            // Bottom brows closed
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin })

        }));

            // Open eyes
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.B, new List<Command> {
            // Nose down
            new Command(RobotControls.NoseBody, ButtonActions.ServoMin),
            new Command(RobotControls.NoseBasket,  ButtonActions.ServoMin ),
            // Brows tilted
            new Command(RobotControls.BrowLeftTopTilt,ButtonActions.ServoHome),
            new Command(RobotControls.BrowRightTopTilt,ButtonActions.ServoHome),           
            // Brow Top closed
            new Command(RobotControls.BrowRightTopOpen, ButtonActions.ServoHome ),
            new Command(RobotControls.BrowLeftTopOpen,ButtonActions.ServoHome ),
            // Bottom brows closed
            new Command(RobotControls.BrowLeftBottomOpen,ButtonActions.ServoHome),
            new Command(RobotControls.BrowRightBottomOpen,ButtonActions.ServoHome)
        }));



            // Play Sounds

            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.DPadLeft, new List<Output> {
            //    new Output( MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.PreviousSound }, new List<string>{@"X:\Johnny5\Soundbites\J5\excelent.mp3", @"X:\Johnny5\Soundbites\J5\excelent.mp3" } ) }));

            //////////////////////////////////////////////////////////////////           
            // Map Left Joystick X access, with no shoulder buttons pressed

            var neckDelay = TimeSpan.FromSeconds(0.15);

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.LJoyX, new List<Output> {
            
            // Left Joystick X moves eyes first
            new Output(RobotControls.LeftLensHorizontal, MultiOutput.Reversed),
            new Output(RobotControls.RightLensHorizontal, MultiOutput.Reversed),

            // Neck waits 150ms before responding
            new Output(RobotControls.NeckTurn, MultiOutput.Normal) //, neckDelay),
            
            // A multiplier of the neck turn input is added to the neck tilt without delay so the neck tilts in anticipation of rotating either way      
           // new Output(RobotControls.NeckTurn RobotControls.NeckTiltLeft,  MultiOutput.Normal, RobotControls.NeckTiltRight,  MultiOutput.Reversed, (float)0.25),          
        }));

            ////////////////////////////////////////////////////////////////////
            // Map Left Joystick Y access, with no shoulder buttons pressed

            // Eyes move vertically 100ms before neck tilts up or down
            var tiltDelay = TimeSpan.FromSeconds(0.10);

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.LJoyY, new List<Output> {

          // Left Joystick Y moves Eyes first.
          new Output(RobotControls.LeftLensVertical, MultiOutput.Normal),
          new Output(RobotControls.RightLensVertical, MultiOutput.Reversed),

          // Neck tilt is delayed by 100ms because the hyraulics take longer to move.
          new Output(RobotControls.NeckTiltRight, MultiOutput.Reversed), //, tiltDelay),
          new Output(RobotControls.NeckTiltLeft, MultiOutput.Normal), //, tiltDelay),
       
        }));


            // Default operation
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RJoyY, new List<Output> {
          new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal),
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Normal),
          new Output(RobotControls.BrowRightTopOpen, MultiOutput.Reversed),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Reversed)
        }));



            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RJoyX, new List<Output> {
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal),

        }));



            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.RTrigger, new List<Output> {
            //  new Output(RobotControls.LeftIris, MultiOutput.Normal),
            //    new Output(RobotControls.RightIris, MultiOutput.Normal)
            //}));
            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.Default }, XBoxControlNames.LTrigger, new List<Output> {
            //  new Output(RobotControls.LeftIris, MultiOutput.Reversed),
            //    new Output(RobotControls.RightIris, MultiOutput.Reversed)
            //}));

          

            return maps;
        }
            
        public static List<IOMap> Add_L_MUX(List<IOMap> maps)
        {
         
            // Left Shoulder Mux            "RainbowCycle,150,lr,20",,"Cylon,100,4"

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DPadLeft, new List<Output> {
         new Output(MultiOutput.Normal, new List<ButtonActions>{ ButtonActions.RGBCommand, ButtonActions.RGBCommand, ButtonActions.RGBCommand, ButtonActions.RGBCommand,ButtonActions.RGBCommand },
                                            new List<string>{ "Cylon,100,2", "ColorWipeEyes,0,0,255,200,eyes,lr,40", "RainbowCycle,150,lr,3","Fade,255,0,0,100,eyes,lr,5,IN,1,0","Pulse,0,255,0,100,both,lr,5,3,1,0"})
              }));
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DPadRight, new List<Output> {
               new Output(MultiOutput.Normal, new List<ButtonActions>{ ButtonActions.RGBCommand, ButtonActions.RGBCommand,ButtonActions.RGBCommand },
                                            new List<string>{ "SetRGBColor, 255,0,0,50,eyes,lr", "ClearAll","SetRGBColor, 255,255,255,200,eyes,lr"})
              }));

            // Thinking
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DPadUp, Sequences.Thinking()));

            // Un Thinking
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DpadDown, Sequences.UnThinking()));

         //   // Left eye Wink
         //   maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder, }, XBoxControlNames.Start, new List<Output> {
         //     // Nose down
         //   new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
         //   new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
         //   // Brows tilted
         //   new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
         //   new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),           
         //   // Brow Top closed          
         //   new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax, ButtonActions.ServoHome } ),
         //   // Bottom brows closed
         //   new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoHome } ),
         //}));

         //   maps.Add(new IOMap(1000, new List<MultiplexInput> { MultiplexInput.L_Shoulder, }, XBoxControlNames.Start, new List<Output> {                     
         //   // Brow Top closed          
         //   new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoHome } ),
         //   // Bottom brows closed
         //   new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMax, ButtonActions.ServoHome } ),
         //}));

            // Normal Eyes
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.Y, new List<Output> {
          //  new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Default, RunOrder.Before ),
            new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome}),
            new Output(RobotControls.LeftIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),

            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome } ),

            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),

              new Output(RobotControls.LeftLensHorizontal, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.LeftLensVertical, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightLensHorizontal, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
            new Output(RobotControls.RightLensVertical, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),

            new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"ClearAll"}),

          
        }));

            // Partly Angry Eyes  
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.X,

                new List<Output> {

         //   new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Slow, RunOrder.Before ),
            // Nose down
            new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            // Brows tilted
            // TODO: need to pull these back some...
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1527 }),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1295 }),
            // Irises half closed
            new Output(RobotControls.LeftIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.RightIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            // Brow Top closed
            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            // Bottom brows closed
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin }),
            // Fadein to red over a couple seconds
            // Fade, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, (In,Out), step, lowest brightness
            new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{ "Fade,255,0,0,100,eyes,lr,40,IN,1,0" }),
            //"SetRGBColor, 255,0,0,50,eyes,lr,0" }), //"Fade,255,0,0,100,eyes,lr,40,IN,1,0"})

            
            }));

            // Angry Eyes  
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.A, new List<Output> {
            
            // Nose down
            new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            // Brows tilted
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1327 }),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1495 }),
            // Irises half closed
            new Output(RobotControls.LeftIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.RightIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            // Brow Top closed
            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            // Bottom brows closed
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),

               new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin }),
            // Fadein to red over a couple seconds
            // Fade, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, (In,Out), step, lowest brightness
            new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"SetRGBColor, 255,0,0,200,eyes,lr,0"}),
          //new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.DisableServo}),
          //new Output(RobotControls.RightEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.DisableServo}),
           new Output(ButtonActions.DisableAllRunningServos, 500),
        }));

            // Very Angry Eyes        
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.B, new List<Output> {
            
           // Closed Left Vent 1765 OPen 2086 closed
           // Closed Right Vent 1125 Open 835
            // Open Vents
            new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoValue },  new List<int>{2086 }),
            new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{835 }),

            // Nose down
            new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            // Brows tilted
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1327 }),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1495 }),
            // Irises half closed
            new Output(RobotControls.LeftIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            new Output(RobotControls.RightIris, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
            // Brow Top closed
            new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            new Output(RobotControls.BrowLeftTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),
            // Bottom brows closed
            new Output(RobotControls.BrowLeftBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
            new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin }),
                   

            // Fadein to red over a couple seconds
            // Fade, red(0-255), green(0-255), blue(0-255), brightness(0-255), (eyes,vents,both), (left,right, lr), delayms, (In,Out), step, lowest brightness
          
            new Output(MultiOutput.Normal, new List<ButtonActions>{ButtonActions.RGBCommand }, new List<string>{"Fade,255,0,0,200,vents,lr,40,IN,1,0"}), //"Fade,255,0,0,200,vents,lr,40,IN,1,0"}),
         new Output(ButtonActions.DisableAllRunningServos, 500),
        }));

            // Axis

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.LJoyX, new List<Output> {
            new Output(RobotControls.LeftIris, MultiOutput.Normal),
            new Output(RobotControls.RightIris, MultiOutput.Normal),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.LJoyY, new List<Output> {
        new Output(RobotControls.LeftEyeVent, MultiOutput.Normal, new List<ButtonActions> { ButtonActions.ServoHome }),
        new Output(RobotControls.RightEyeVent, MultiOutput.Reversed, new List<ButtonActions> { ButtonActions.ServoHome }),
         }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.RJoyX, new List<Output> {
            new Output(RobotControls.LeftLensHorizontal, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMax }),
            new Output(RobotControls.RightLensHorizontal, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMax }),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.RJoyY, new List<Output> {
            new Output(RobotControls.LeftLensVertical, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax }),
            new Output(RobotControls.RightLensVertical, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin }),
        }));





            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.RJoyY, new List<Output> {
            //  new Output(RobotControls.LeftEyeVent, MultiOutput.Normal),
            //    new Output(RobotControls.RightEyeVent, MultiOutput.Normal),

            //}));

            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DPadRight, new List<Output> {
            //    new Output(mfrcRotate, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }, TimeSpan.FromSeconds(2), 3) }));


            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.L_Shoulder }, XBoxControlNames.DPadUp, new List<Output> {
            //    new Output(mfrcUpDown, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }) }));

          
            return maps;
        }
           
        public static List<IOMap> Add_R_MUX(List<IOMap> maps)
        {

         

            ////////////////////////////////////////////////////////////////////////////////////////
            // Right Shoulder MUX    

            // Right eye Wink
            // Part 1
         //   maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder, }, XBoxControlNames.Start, new List<Output> {
         //     // Nose down
         //   new Output(RobotControls.NoseBody, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
         //   new Output(RobotControls.NoseBasket, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoMin } ),
         //   // Brows tilted
         //   new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome }),
         //   new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),           
         //   // Brow Right Top closed          
         //   new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMax } ),         
         //   // Brow Right Bottom  closed           
         //   new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin })
         //}));

         //   // Right eye Wink
         //   // Part 2
         //   var msWinkDelay = 1000;
         //   maps.Add(new IOMap(msWinkDelay, new List<MultiplexInput> { MultiplexInput.R_Shoulder, }, XBoxControlNames.Start, new List<Output> {                     
         //   // Brow Top opened          
         //   new Output(RobotControls.BrowRightTopOpen, MultiOutput.Normal, new List<ButtonActions>{ ButtonActions.ServoHome } ),
         //   // Bottom brows opened
         //   new Output(RobotControls.BrowRightBottomOpen, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
         //}));

            // Slow all Servos
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.DPadLeft, new List<Output> {
        new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Slow,  RunOrder.Before),
        }));

            // Speed up all Servos
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.DPadRight, new List<Output> {
        new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Fast,  RunOrder.Before),
        }));
            // Set Servos to Default
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.DPadUp, new List<Output> {
        new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Default,  RunOrder.Before),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.DpadDown, new List<Output> {
          new Output(new List<ButtonActions>{ButtonActions.MaestroSetAll }, ServoSpeed.Crawl,  RunOrder.Before),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.Y, new List<Output> {
            new Output(RobotControls.Microphone_RaiseLower, MultiOutput.Normal, new List<ButtonActions>{ ButtonActions.ServoMax, ButtonActions.ServoMin }) }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.B, new List<Output> {
            new Output(RobotControls.MFR_UpDown, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }) }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.X, new List<Output> {
                new Output(RobotControls.Whip_Antenna_RaiseLower, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoMin, ButtonActions.ServoMax }) }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.A, new List<Output> {
            new Output(RobotControls.RightEyeVent, MultiOutput.Normal, new List<ButtonActions>{ ButtonActions.ServoMax,ButtonActions.ServoMin, }),
             new Output(RobotControls.LeftEyeVent, MultiOutput.Reversed, new List<ButtonActions>{ ButtonActions.ServoMax,ButtonActions.ServoMin, }),
             //  new Output(null, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.DisableAllRunningServos}),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.LJoyX, new List<Output> {
           new Output(RobotControls.Whip_Antenna_Rotate, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
        }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.LJoyY, new List<Output>
            {
            }));

            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.RJoyX, new List<Output> {
          new Output(RobotControls.MFR_Rotate, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
        }));

            //maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.R_Shoulder }, XBoxControlNames.RJoyY, new List<Output> {
            //}));    


          


            return maps;
        }
      
        public static List<IOMap> Add_LR_MUX(List<IOMap> maps)
        {        
            // reset Brow
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.End, new List<Output> {
            new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed, new List<ButtonActions>{ButtonActions.ServoHome } ),
            new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
        }));

            // Neck presets 0 degrees  1300 - 1740
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.Y, new List<Output> {
            new Output(RobotControls.NeckTurn, MultiOutput.Normal, new List<ButtonActions>{ButtonActions.ServoHome }),
        }));

            // Neck 30 degrees right
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.X, new List<Output> {
            new Output(RobotControls.NeckTurn, MultiOutput.Normal,new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1400 }),
        }));

            // Neck 45 degrees right
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.A, new List<Output> {
            new Output(RobotControls.NeckTurn, MultiOutput.Normal,new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1500 }),
        }));

            // Neck 60 degrees right
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.B, new List<Output> {
            new Output(RobotControls.NeckTurn, MultiOutput.Normal,new List<ButtonActions>{ButtonActions.ServoValue }, new List<int>{1600 }),
        }));


            // Sound controls
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.DPadUp, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.PlayFirst } )
        }));
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.DpadDown, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.PlayCurrent } )
        }));
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.DPadLeft, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.PlayPrevious } )
        }));
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.DPadRight, new List<Output> {
            new Output(new List<ButtonActions>{ButtonActions.PlayNext } )
        }));


            // Tilt head 
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.LJoyX, new List<Output> {
               new Output(RobotControls.NeckTiltLeft, MultiOutput.Reversed),
               new Output(RobotControls.NeckTiltRight, MultiOutput.Reversed),
        }));

            // Tilt brows 
            maps.Add(new IOMap(new List<MultiplexInput> { MultiplexInput.LR_Shoulder }, XBoxControlNames.RJoyX, new List<Output> {
               new Output(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed),
               new Output(RobotControls.BrowRightTopTilt, MultiOutput.Normal),
        }));

            return maps;

        }


    }
}
