using System.IO.Ports;
using System.Text.Json.Serialization;


/// <summary>
/// The Servo Class defines each Servo and provides all the methods to drive them.
/// 
/// Setting Maestro Speed / Acceleration
/// Disabling a Servo so it stops buzzing
/// 
/// Simple Go Min, Max, Home commands
///
/// Mapping Axis to Servo positions with Expo 
/// 
/// Stay will use Axis motion to move in deltas to a given position and hold.
///
/// Self Center will return to Center if the sticks got to zero
/// 
/// 
/// </summary>

public enum ServoMode
{
    SelfCenter,
    Stay
}

public enum StartPosition
{
    Min,
    Max,  
    Home,
    Mode
}

public enum ServoSpeed
{
    Default =0, 
    Slow =1,
    Fast =2,
    Crawl = 3
}


public enum RobotControls
{
    None = 1000,

    // These enumerations directly map to a Servo Array when driving the entire robot.
    //
    // Currently they also map to the channels of a single 24 port Maestro Card
    //
    // To extend this:
    // 1:  This RobotControl enumeration should continue to have a sequential list of ALL the Robot Servos.
    // 2: Create additional enumerations for each Maestro card separately.
    // 3: Add a Servo Channel argument to the Constructor
    // 4: Set the Servo Channel based on that argument.
    //
    // Then continue to map this enumeration to the Servo Name.but specify the Channel number separately.
    //

    // Left
    LeftEyeVent = 0,
    LeftIris = 1,
    LeftLensHorizontal = 2,
    LeftLensVertical = 3,
    NeckTiltLeft = 4,
    BrowLeftBottomOpen = 5,

    // Nose
    BrowLeftTopOpen = 6,
    BrowLeftTopTilt = 7,
    NoseBody = 8,
    NoseBasket = 9,
    BrowRightTopTilt = 10,
    BrowRightTopOpen = 11,

    // Right 
    BrowRightBottomOpen = 12,
    NeckTiltRight = 13,
    RightLensVertical = 14,
    RightLensHorizontal = 15,
    RightIris = 16,
    RightEyeVent = 17,

    // Misc
    Whip_Antenna_Rotate = 19,
    Whip_Antenna_RaiseLower = 18,
    MFR_UpDown = 20,
    MFR_Rotate = 21,
    NeckTurn = 22,
    Microphone_RaiseLower = 23,

    // These are just for identification purposes although they are not servos.

    //eye pop Tic T249
    LeftEyePop = 200,
    RightEyePop = 201,

    // Arduino
    LeftEyeRGBLightFront = 101,
    LeftEyeRGBLightBack = 102,
    RightEyeRGBLightFront = 103,
    RightEyeRGBLightBack = 104
}



/// <summary>
/// Servo Class
/// </summary>
public class Servo
{
    const int DefaultBaudRate = 115200; // 115200

    public RobotControls Name { get; set; }
    public string USBPort { get; set; }    
    public int Channel { get; set; }
    public int degreeRange { get; set; }
    public int HomeValue { get; set; }
    public int ModeValue { get; set; }
    public bool Reverse { get; set; }
    public int CurrentPosition { get; set; }
    public float Expo { get; set; }
    public int stepScale { get; set; }
    public int LimitUpper { get; set; }
    public int ModeUpper { get; set; }
    public int ModeLower { get; set; }
    public int LimitLower { get; set; }

    public int[] Speed { get; set; }
    public int[] Accel { get; set; }
    public bool isDisabled { get; set; }
    public ServoSpeed currentSpeed { get; set; }      
    public ServoMode Mode { get; set; }
    public StartPosition Startposition { get; set; }
       
    public bool EyePopSensitive { get; set; }

    /// <summary>
    /// Initialize Servo
    /// </summary>
    /// <param name="boardAddress"></param>
    /// <param name="channel"></param>
    /// <param name="mode"></param>
    /// <param name="startposition"></param>
    /// <param name="reverse"></param>
    /// <param name="servoType"></param>  

    [JsonConstructor]
    public Servo(RobotControls Name,  string USBPort, ServoMode Mode, StartPosition Startposition, bool Reverse, int HomeValue, int LimitLower, int LimitUpper, int[] Speed, int[] Accel, int stepScale = 75, float Expo = (float)0.25)//, float expo)
    {
        this.Name = Name;
        this.USBPort = USBPort;
        this.Channel = (int)Name;
        this.HomeValue = HomeValue;
        this.ModeValue = HomeValue;       
        this.Mode = Mode;
        this.Startposition = Startposition;
        this.Reverse = Reverse;      

        this.LimitLower = LimitLower;
        this.LimitUpper = LimitUpper;
        this.ModeLower = LimitLower;
        this.ModeUpper = LimitUpper;
        this.stepScale = stepScale;
        this.Expo = Expo;

        this.Speed = Speed;
        this.Accel = Accel;

        CurrentPosition = this.HomeValue;

        switch (Startposition)
        {
            case StartPosition.Min:
                CurrentPosition = this.LimitLower;
                break;
            case StartPosition.Max:
                CurrentPosition = this.LimitUpper;
                break;
          
            case StartPosition.Home:
                CurrentPosition = this.HomeValue;
                break;

            case StartPosition.Mode:
                CurrentPosition = this.ModeValue;
                break;
        }

    }


    /// <summary>
    /// Map Trigger to Servo
    /// </summary>
    /// <param name="c"></param>
    /// <param name="servo"></param>
    /// <param name="reversed"></param>
    /// <returns></returns>
    public static int MapTriggertoServo(Control c, Servo servo, bool reversed)
    {

        double outMin = servo.LimitLower;
        double outMax = servo.LimitUpper;
        double inMax = 255; // c.High;
        double inMin = 0; // c.Low;
        double value = (Int32)c.Value;

        if (reversed)
        {
            value = 255 - value;
        }

        var adjustedValue = ((value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin);

        //float curve = 0;

        //if (servo.Mode == ServoMode.Stay)
        //    curve = UpdateServoIncrementalTrigger(servo.CurrentPosition, value, outMin, outMax, (float)0.5);
        //else
        //    curve = ApplyExpo((float)adjustedValue, servo.Expo);

        return Convert.ToInt32(adjustedValue);
    }

    public static int MapDeltatoServo(int deltaValue, Servo servo, bool isGangReversed, bool isCentered)
    {

        double outMin = servo.LimitLower;
        double outMax = servo.LimitUpper;
        double outHome = servo.HomeValue;
        double inMax = 100; 
        double inMin = -100; 
        double value = deltaValue;               

        double adjustedValue = 0;            

        if (isCentered)  // -100 to 100
        {
            // if 50  not reversed  
            if (isGangReversed) value = -value;

            if (!servo.Reverse)
            {
                if (value < 0)
                {
                    // if outHome = 1200  Value = 50/100 = .5 * (1200 - 800 )
                    adjustedValue = outHome + (value / 100) * (outHome - outMin); 
                }
                else
                {
                    // if outHome = 1200  Value = 50/100 = .5 * (1800 - 1200 )
                    adjustedValue = outHome + (value / 100) * (outMax - outHome);
                }
            }
            else
            {
                if (value > 0)
                {
                    // if outHome = 1200  Value = 50/100 = .5 * (1800 - 1200 )
                    adjustedValue = outHome + (value / 100) * (outMax - outHome);
                   
                }
                else
                {
                    // if outHome = 1200  Value = 50/100 = .5 * (1200 - 800 )
                    adjustedValue = outHome + (value / 100) * (outHome - outMin);                 }
            }
        }
        else // not centered only 0-100
        {
            
            if (servo.Reverse)
            {              
                adjustedValue = outMax - (value / 100) * (outMax - outMin);
            }
            else
            {
                adjustedValue = outHome + (value / 100) * (outMax - outMin);
            }          
          
        } 
      
        return Convert.ToInt32(adjustedValue);
    }

    /// <summary>
    /// For Eye Pop
    /// </summary>
    /// <param name="c"></param>
    /// <param name="servo"></param>
    /// <param name="reversed"></param>
    /// <returns></returns>
    public static int MapTriggerEyePop(Control c)
    {
        double outMax = 2100;
        double inMax = 255;             
      
        var adjustedValue = Convert.ToDouble(c.Value) * outMax/inMax ;

        return Convert.ToInt32(adjustedValue);
    }


    public static int UpdateServoIncrementalTrigger(
   int currentPos,     // current servo microseconds
   int rawAxis,        // joystick raw value (-32768..+32767)
   int minPos,         // servo min µs
   int maxPos,         // servo max µs
   float expo = 0.3f,  // exponential curve strength (0..1)
   int stepScale = 150,  // base step per frame at full deflection
   float deadzone = 0.01f // fraction of axis range
)
    {
        //if (rawAxis > 30000) return maxPos;
        //if (rawAxis < -30000) return minPos;

        // Normalize axis to -1..1
        float norm = (float)rawAxis / (float)255;
        //if (norm < -1f) norm = -1f;

        // Deadzone
        if (norm < deadzone)
            return currentPos;

        // Remove deadzone & re-scale
        //float s = MathF.Sign(norm);
        //norm = s * ((MathF.Abs(norm) - deadzone) / (1 - deadzone));

        // Apply exponential curve
        norm = ApplyExpo(norm, expo);

        // Compute delta
        float delta = norm * stepScale;

        int newPos = (int)MathF.Round(currentPos + delta);


        // Clamp to servo range
        if (minPos < maxPos)
        {
            if (newPos < minPos) newPos = minPos;
            if (newPos > maxPos) newPos = maxPos;
        }
        else
        {
            if (newPos > minPos) newPos = minPos;
            if (newPos < maxPos) newPos = maxPos;
        }


        return newPos;
    }
    public static int MapAxis(Control c, Servo servo, bool reversed, bool isRaw)
    {

        var outMin = servo.LimitLower;
        var outMax = servo.LimitUpper;
        var inMax = c.High;
        var inMin = c.Low;
        var value = (Int32)c.Value;

        if (reversed)
        {
            value = -value;
        }

        int us = 0;


        if (servo.Mode == ServoMode.Stay && !isRaw)
            us = UpdateServoIncremental(servo.CurrentPosition, value, outMin, outMax, servo.Expo, servo.stepScale);
        else
            us = ServoFromRawJoystick(value, outMin, servo.HomeValue, outMax, (float)0.05, servo.Expo);

        return us;
    }

    public static int UpdateServoIncremental(
    int currentPos,     // current servo microseconds
    int rawAxis,        // joystick raw value (-32768..+32767)
    int minPos,         // servo min µs
    int maxPos,         // servo max µs
    float expo = 0.3f,  // exponential curve strength (0..1)
    int stepScale = 5,  // base step per frame at full deflection
    float deadzone = 0.03f // fraction of axis range
)
    {
        //if (rawAxis > 30000) return maxPos;
        //if (rawAxis < -30000) return minPos;

        // Normalize axis to -1..1
        float norm = rawAxis / 32767f;


        if (norm < -1f) norm = -1f;

        // Deadzone
        if (MathF.Abs(norm) < deadzone)
        { 
        return currentPos;
        }

        // Remove deadzone & re-scale
        float s = MathF.Sign(norm);
        norm = s * ((MathF.Abs(norm) - deadzone) / (1 - deadzone));

        // Apply exponential curve
        norm = ApplyExpo(norm, expo);

        // Compute delta
        float delta = norm * stepScale;

        int newPos = (int)MathF.Round(currentPos + delta);

        // Clamp to servo range
        if (minPos < maxPos)
        {
            if (newPos < minPos) newPos = minPos;
            if (newPos > maxPos) newPos = maxPos;
        }
        else
        {
            if (newPos > minPos) newPos = minPos;
            if (newPos < maxPos) newPos = maxPos;
        }


        return newPos;
    }
    public static int ServoFromRawJoystick(
    int raw,           // -32768..+32767
    int uMin, int uHome, int uMax,
    float deadzone = 0f,
    float expo = 0f)
    {
        // Normalize
        float j = (float)raw / 32767f;
        if (j < -1f) j = -1f;

        // Deadzone
        float a = MathF.Abs(j);
        if (a < deadzone) j = 0f;
        else
        {
            float s = MathF.Sign(j);
            j = s * (a - deadzone) / (1f - deadzone);
        }



        // Exponential curve
        if (expo != 0f)
            j = j * (1 - expo) + j * j * j * expo;

        // Asymmetric map
        float posSpan = uMax - uHome;
        float negSpan = uHome - uMin;
        float up = MathF.Max(0, j) * posSpan;
        float un = MathF.Min(0, j) * negSpan;
        float u = uHome + up + un;

        return (int)MathF.Round(u);
    }

    /// <summary>
    /// TODO need to verify this works.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="expo"></param>
    /// <returns></returns>
    public static float ApplyExpo(float input, float expo)
    {
        return input * (1 - expo) + input * input * input * expo;
    }

    static int Map(int value, int inMin, int inMax, int outMin, int outMax)
    {
        return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    }
     

    public void GoHome()
    {
        WritePwmMicroseconds(USBPort, Channel, HomeValue);

    }

    public void GoMode()
    {
        WritePwmMicroseconds(USBPort, Channel, ModeValue);

    }

    public void GoValue(int us)
    {       
        WritePwmMicroseconds(USBPort, Channel, us);
    }

    //public void GoMin()
    //{
    //    WritePwmMicroseconds(USBPort, Channel, Reverse ? LimitUpper : LimitLower);

    //}

    //public void GoMax()
    //{
    //    WritePwmMicroseconds(USBPort, Channel, Reverse ? LimitLower : LimitUpper);

    //}
    public static void SetTargetsLast(List<Servo> servos)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One); //  115200
        port.Open();     

        // Each command is 4 bytes; buffer holds all 10 at once
        byte[] buffer = new byte[servos.Count * 4];
        int index = 0;
        var i = 0;
        foreach (Servo s in servos)
        {
            int target = s.CurrentPosition * 4; // Maestro uses quarter-microseconds

            buffer[index++] = 0x84; // Set Target command
            buffer[index++] = (byte)s.Channel;
            buffer[index++] = (byte)(target & 0x7F);
            buffer[index++] = (byte)((target >> 7) & 0x7F);
            i++;
            s.isDisabled = false;
        }

        // Single write call for all channels
        port.Write(buffer, 0, buffer.Length);
        port.Close();
    }

    /// <summary>
    /// Write all Servo positions in a single write.
    /// </summary>
    /// <param name="servos"></param>
    /// <param name="microseconds"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void SetTargetsBatch(List<Servo>servos, int[] microseconds)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One); //  115200
        port.Open();

        if (servos.Count != microseconds.Length)
            throw new ArgumentException("Channels and microseconds arrays must have same length");

        // Each command is 4 bytes; buffer holds all 10 at once
        byte[] buffer = new byte[servos.Count * 4];
        int index = 0;
        var i = 0;
        foreach(Servo s in servos)
        {
            int target = microseconds[i] * 4; // Maestro uses quarter-microseconds

            buffer[index++] = 0x84; // Set Target command
            buffer[index++] = (byte)s.Channel;
            buffer[index++] = (byte)(target & 0x7F);
            buffer[index++] = (byte)((target >> 7) & 0x7F);
            i++;
            s.isDisabled = false;
        }

        // Single write call for all channels
        port.Write(buffer, 0, buffer.Length);
        port.Close();
    }

    public static void SetTargetsBatch(byte[] advanceBuffer, List<Servo> servos, int[] microseconds)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One);
        port.Open();

        if (servos.Count != microseconds.Length)
            throw new ArgumentException("Channels and microseconds arrays must have same length");

        // Each command is 4 bytes; buffer holds all 10 at once
        byte[] buffer = new byte[servos.Count * 4];
        int index = 0;
        var i = 0;
        foreach (Servo s in servos)
        {
            int target = microseconds[i] * 4; // Maestro uses quarter-microseconds

            buffer[index++] = 0x84; // Set Target command
            buffer[index++] = (byte)s.Channel;
            buffer[index++] = (byte)(target & 0x7F);
            buffer[index++] = (byte)((target >> 7) & 0x7F);
            i++;
        }

        var allCommands  = advanceBuffer.Concat(buffer).ToArray();
        // Single write call for all channels
        port.Write(allCommands, 0, allCommands.Length);
        port.Close();
    }


    public static void SetTargetsLastPositionBatch(List<Servo> servos)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One);
        port.Open();      

        // Each command is 4 bytes; buffer holds all 10 at once
        byte[] buffer = new byte[servos.Count * 4];
        int index = 0;
        var i = 0;
        foreach (Servo s in servos)
        {
            int target = s.CurrentPosition * 4; // Maestro uses quarter-microseconds

            buffer[index++] = 0x84; // Set Target command
            buffer[index++] = (byte)s.Channel;
            buffer[index++] = (byte)(target & 0x7F);
            buffer[index++] = (byte)((target >> 7) & 0x7F);
            i++;
            s.isDisabled = false;
        }      
        // Single write call for all channels
        port.Write(buffer, 0, buffer.Length);
        port.Close();
    }

    /// <summary>
    /// Set Servo Speed in a single write.
    /// </summary>
    /// <param name="servos"></param>
    /// <param name="pickValues"></param>
    /// <returns></returns>
    public static byte[] ConfigureSpeedBuffer(List<Servo> servos, ServoSpeed pickValues)
    {
        //var port = new SerialPort(servos[0].USBPort, 115200, Parity.None, 8, StopBits.One);
        //port.Open();

        // Each command is 2 bytes; buffer holds all at once
        byte[] buffer = new byte[servos.Count * 8];       
        int index = 0;
        int offset = servos.Count * 4;

        foreach (Servo s in servos)
        {
            buffer[index] = 0x87;
            buffer[offset + index++] = 0x89;
            buffer[index] = (byte)s.Channel;
            buffer[offset + index++] = (byte)s.Channel;
            buffer[index] = (byte)(s.Speed[(int)pickValues] & 0x7F);
            buffer[offset + index++] = (byte)(s.Accel[(int)pickValues] & 0x7F);
            buffer[index] = (byte)(s.Speed[(int)pickValues] >> 7 & 0x7F);
            buffer[offset + index] = (byte)(s.Accel[(int)pickValues] >> 7 & 0x7F);
        }      

        return buffer;
        //// Single write call for all channels
        //port.Write(buffer, 0, buffer.Length);
        //port.Close();
    }


    public void SetRangeAll(Servo[] servos)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One);
        port.Open();

        int minUs = 544;
        int maxUs = 2496;

        int minQ = minUs * 4;
        int maxQ = maxUs * 4;

        var device = 12;

        foreach (Servo s in servos)
        {
            byte[] cmd =
                {
                0xAA, (byte)device,
                0x0E,
                (byte)s.Channel,
                (byte)(minQ & 0x7F),
                (byte)((minQ >> 7) & 0x7F),
                (byte)(maxQ & 0x7F),
                (byte)((maxQ >> 7) & 0x7F)
                };

            port.Write(cmd, 0, cmd.Length);
        }
        port.Close();
    }

    public static void ConfigureSpeedAll( Servo[] servos, ServoSpeed pickValues)
    {
        var port = new SerialPort(servos[0].USBPort, 9600, Parity.None, 8, StopBits.One);
        port.Open();              

        // Each command is 2 bytes; buffer holds all at once
        byte[] buffer = new byte[servos.Length * 8];
        int index = 0;
        int offset = servos.Length * 4;

        foreach (Servo s in servos)
        {
            buffer[index] = 0x87;
            buffer[offset + index++] = 0x89;
            buffer[index] = (byte)s.Channel;
            buffer[offset + index++] = (byte)s.Channel;
            buffer[index] = (byte)(s.Speed[(int)pickValues] & 0x7F);
            buffer[offset + index++] = (byte)(s.Accel[(int)pickValues] & 0x7F);
            buffer[index] = (byte)(s.Speed[(int)pickValues] >> 7 & 0x7F);
            buffer[offset + index] = (byte)(s.Accel[(int)pickValues] >> 7 & 0x7F);

            s.currentSpeed = pickValues;   
           
        }
        // Single write call for all channels
        port.Write(buffer, 0, buffer.Length);
        port.Close();
    }

    /// <summary>
    /// Sets all servos to their current speed and acceleration values.
    /// </summary>
    /// <param name="servos"></param>
    public static void ConfigureSpeedLast(List<Servo> servos)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One);
        port.Open();

        // Each command is 2 bytes; buffer holds all at once
        byte[] buffer = new byte[servos.Count * 8];
        int index = 0;
        int offset = servos.Count * 4;

        foreach (Servo s in servos)
        {
            buffer[index] = 0x87;
            buffer[offset + index++] = 0x89;
            buffer[index] = (byte)s.Channel;
            buffer[offset + index++] = (byte)s.Channel;
            buffer[index] = (byte)(s.Speed[(int)s.currentSpeed] & 0x7F);
            buffer[offset + index++] = (byte)(s.Accel[(int)s.currentSpeed] & 0x7F);
            buffer[index] = (byte)(s.Speed[(int)s.currentSpeed] >> 7 & 0x7F);
            buffer[offset + index] = (byte)(s.Accel[(int)s.currentSpeed] >> 7 & 0x7F);
        }
        // Single write call for all channels
        port.Write(buffer, 0, buffer.Length);
        port.Close();
    }

    public void GetPositionCompareDisable(List<Servo> servos)
    {
        var port = new SerialPort(servos[0].USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One);
        port.Open();

        foreach (Servo s in servos)
        {
            if (!s.isDisabled)
            {
                byte[] cmd = { 0xAA, 0x0C, 0x10, (byte)s.Channel };
                port.Write(cmd, 0, cmd.Length);

                int lsb = port.ReadByte();
                int msb = port.ReadByte();
                int q = lsb + (msb << 8);

                if (q <= s.CurrentPosition +1 && q >= s.CurrentPosition - 1)
                {
                    s.DisableServo();
                }
            }
        }      
    }

    public void ChangeMode(ServoMode servoMode)
    {
        this.Mode = servoMode;
    }



    public void ResetServo(int setSpeed)
    {       
        GoValue(CurrentPosition);
        ConfigureSpeed(Speed[(int)setSpeed], Accel[(int)setSpeed]);
        isDisabled = false;
    }

    public void ConfigureSpeed(ServoSpeed pickValues)
    {
        byte[] cmd = new byte[]
        {
        0x87, (byte)Channel, (byte)(Speed[(int)pickValues] & 0x7F), (byte)(Speed[(int)pickValues] >> 7 & 0x7F),
        0x89, (byte)Channel, (byte)(Accel[(int)pickValues] & 0x7F), (byte)(Accel[(int)pickValues] >> 7 & 0x7F)
        };

        using (SerialPort port = new SerialPort(USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(cmd, 0, cmd.Length);
        }
    }
    public void ConfigureSpeed(int speed, int accel)
    {
        byte[] cmd = new byte[]
        {
        0x87, (byte)Channel, (byte)(speed & 0x7F), (byte)(speed >> 7 & 0x7F),
        0x89, (byte)Channel, (byte)(accel & 0x7F), (byte)(accel >> 7 & 0x7F)
        };

        using (SerialPort port = new SerialPort(USBPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(cmd, 0, cmd.Length);
        }
    }

    public static void ConfigureChannel(string comPort, int channel, int speed, int accel)
    {
        byte[] cmd = new byte[]
        {
        0x87, (byte)channel, (byte)(speed & 0x7F), (byte)(speed >> 7 & 0x7F),
        0x89, (byte)channel, (byte)(accel & 0x7F), (byte)(accel >> 7 & 0x7F)
        };

        using (SerialPort port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(cmd, 0, cmd.Length);
        }
    }


    public void WritePwmMicroseconds(string comPort, int channel, int microseconds)
    {
        CurrentPosition = microseconds;
        if (microseconds < 64 || microseconds > 16383)
            throw new ArgumentOutOfRangeException(nameof(microseconds), "Value must be between 64 and 16383 microseconds.");
        
       

        // Maestro expects target in quarter-microseconds (1 µs = 4 units)
        int target = microseconds * 4;
        byte targetLow = (byte)(target & 0x7F);
        byte targetHigh = (byte)((target >> 7) & 0x7F);

        byte[] command = new byte[]
        {
            0x84,             // Command: Set Target (Compact Protocol)
            (byte)channel,
            targetLow,
            targetHigh
        };

        using (SerialPort port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(command, 0, command.Length);
          //  Console.WriteLine($"Sent {microseconds}µs (target={target}) to channel {channel} on {comPort}");

            //byte[] command2 = new byte[]
            //    {
            //    // Disable Servo 
            //    0xAA, // Start byte
            //    0x0C, // Device number (12 default)
            //    0x0F, // Command: Disable PWM
            //    (byte)channel
            //    };
            //port.Write(command2, 0, command2.Length);

        }

    }
    public static void EnableChannel(string comPort, byte channel, ushort microseconds)
    {
        byte command = 0x84; // Set Target
        ushort target = (ushort)(microseconds * 4); // Convert microseconds to Maestro units
        byte[] buffer = new byte[4];
        buffer[0] = command;
        buffer[1] = channel;
        buffer[2] = (byte)(target & 0x7F);
        buffer[3] = (byte)((target >> 7) & 0x7F);

        using (SerialPort port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(buffer, 0, 4);
        }


    }

    public void DisableServo()
    {
        DisableServo(USBPort, Channel);
        this.isDisabled = true;
    }

    public static void DisableServo(string comPort, int channel)
    {
        
        byte[] command = new byte[]
        {
        0xAA, // Start byte
        0x0C, // Device number (12 default)
        0x0F, // Command: Disable PWM
        (byte)channel
        };

        using (SerialPort port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(command, 0, command.Length);
        }
    }

    public static void SetChannelOff(string comPort, byte channel)
    {
        // Send "Set Target" with target=0
        byte[] buffer = new byte[] { 0x84, channel, 0, 0 };
       

        using (SerialPort port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One))
        {
            port.Open();
            port.Write(buffer, 0, buffer.Length);
        }
    }
}

