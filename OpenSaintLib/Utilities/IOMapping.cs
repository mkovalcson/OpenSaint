using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


public class IOMapping
{
    public List<IOMap> Default { get; set; }
    public List<IOMap> L_Shoulder { get; set; }
    public List<IOMap> R_Shoulder { get; set; }
    public List<IOMap> LR_Shoulder { get; set; }
    public List<IOMap> TimerActions { get; set; }
    public IOMapping()
    {    
        Default = new List<IOMap>();
        L_Shoulder = new List<IOMap>();
        R_Shoulder = new List<IOMap>();
        LR_Shoulder = new List<IOMap>();    
        TimerActions = new List<IOMap>();
    }             
}

public enum MultiplexInput
{
    Default,
    L_Shoulder,
    R_Shoulder,
    LR_Shoulder
}

public enum MultiOutput
{
    Normal,
    Reversed
}

public class IOMap
{
    public List<MultiplexInput> Multiplex { get; set; }
    public XBoxControlNames Input { get; set; }
    public List<Output> OutputList { get; set; }
    public List<Command>? CommandList { get; set; }
    public int DelayMs { get; set; }

    public DateTime FireTime { get; set; }

    [JsonConstructor]

    public IOMap(List<Output> OutputList)
    {
        this.Multiplex = new List<MultiplexInput>();
       
        this.OutputList = OutputList; 
    }

    public IOMap(List<MultiplexInput> Multiplex, XBoxControlNames Input, List<Command> CommandList)
    {
        this.Multiplex = Multiplex;
        this.Input = Input;
        this.CommandList = CommandList;
        this.OutputList = new List<Output>();
        this.DelayMs = 0;
        this.FireTime = DateTime.MinValue;
    }

    public IOMap(int DelayMs, List<MultiplexInput> Multiplex, XBoxControlNames Input, List<Command> CommandList)
    {
        this.Multiplex = Multiplex;
        this.Input = Input;
        this.CommandList = CommandList;
        this.OutputList = new List<Output>();
        this.DelayMs = DelayMs;
        this.FireTime = DateTime.MinValue;
    }

    public IOMap(List<MultiplexInput> Multiplex, XBoxControlNames Input, List<Output> OutputList)
    {
        this.Multiplex = Multiplex;
        this.Input = Input;
        this.OutputList = OutputList;
        this.DelayMs = 0;
        this.FireTime = DateTime.MinValue;
    }
    public IOMap(int DelayMs, List<MultiplexInput> Multiplex, XBoxControlNames Input, List<Output> OutputList)
    {
        this.Multiplex = Multiplex;
        this.Input = Input;
        this.OutputList = OutputList;
        this.DelayMs = DelayMs;
        this.FireTime = DateTime.MinValue;
    }
}

public enum RunOrder
{
    Before,
    After
}
public class Output
{
    public MultiOutput MultiOutput { get; set; }
    public List<ButtonActions> Actions { get; set; }
    public float Multiplier;
    public List<string> pathNames;
    public int count;
    public TimeSpan Timer;
    public DateTime lastFired;
    public int iterations;
    public int iterationCount;
    public List<int> Values;
    public RobotControls? robotControl;
    public RunOrder RunOrder { get; set; }
    public ServoSpeed Speed { get; set; }
    [JsonConstructor]


    // Controller Outputs  X-Box

    public Output(RobotControls ControlName, MultiOutput MultiOutput)
    {
        this.MultiOutput = MultiOutput;
        this.robotControl = ControlName;
              Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }
    public Output(RobotControls ControlName, MultiOutput MultiOutput, TimeSpan Delay)
    {
        this.MultiOutput = MultiOutput;
        this.robotControl = ControlName;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }
   
    public Output(RobotControls ControlName, MultiOutput MultiOutput, List<ButtonActions> Actions)
    {
        this.MultiOutput = MultiOutput;
        this.robotControl = ControlName;
        this.Actions = Actions;
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }

    //public Output(RobotControls ControlName, Servo SlaveServo, MultiOutput MultiOutput, Servo SlaveServo2, MultiOutput MultiOutput2, float Multiplier)
    //{
    //    this.MultiOutput = MultiOutput;
    //    this.Servo = Servo;
    //    this.SlaveServo = SlaveServo;   
    //    this.SlaveServo2 = SlaveServo2;
    //    this.Actions = Actions;
    //    this.Multiplier = Multiplier;
    //    this.count = 0;
    //    Timer = TimeSpan.Zero;
    //    lastFired = DateTime.MinValue;       
    //}

    public Output(RobotControls ControlName, MultiOutput MultiOutput, List<ButtonActions> Actions, List<int> values)
    {
        this.MultiOutput = MultiOutput;
        this.robotControl = ControlName;
        this.Actions = Actions;
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.Values = values;
    }
    public Output(RobotControls ControlName, MultiOutput MultiOutput, List<ButtonActions> Actions, TimeSpan Timer, int iterations)
    {
        this.robotControl = ControlName;
        this.MultiOutput = MultiOutput;     
        this.Actions = Actions;
        this.count = 0;
        this.iterationCount = 0;
        this.Timer = Timer;
        this.iterations = iterations;
        lastFired = DateTime.MinValue;
    }
    public Output(MultiOutput MultiOutput, List<ButtonActions> Actions, List<string> pathNames)
    {
        this.MultiOutput = MultiOutput;       
        this.Actions = Actions;
        this.pathNames = pathNames;
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }


    // ControlName Sequencies
        
    public Output(RobotControls ControlName, ButtonActions Actions, TimeSpan Delay)
    {
        this.robotControl = ControlName;
        this.MultiOutput = MultiOutput;
        this.robotControl = ControlName;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }

    public Output(RobotControls ControlName, ButtonActions Actions, int values)
    {
        this.robotControl = ControlName;
        this.Actions = new List<ButtonActions>() { Actions };
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.Values = new List<int>() { values };
    }

    public Output(RobotControls ControlName, ButtonActions Actions)
    {
        this.robotControl = ControlName;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
    }

    // Stand Alone Actions

    public Output(List<ButtonActions> Actions, ServoSpeed Speed, RunOrder order)
    {
        this.Actions = Actions;
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.Speed = Speed;
        this.RunOrder = order;
    }
    public Output(ButtonActions Actions, ServoSpeed Speed)
    {
        this.Actions = new List<ButtonActions>() { Actions };
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.Speed = Speed;
    }
    public Output(ButtonActions Actions, string pathname)
    {
        this.Actions = new List<ButtonActions>() { Actions };
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.pathNames = new List<string>() { pathname };
    }

    public Output(ButtonActions Actions, int values)
    {

        this.Actions = new List<ButtonActions>() { Actions };
        this.count = 0;
        Timer = TimeSpan.Zero;
        lastFired = DateTime.MinValue;
        this.Values = new List<int>() { values };
    }

    public Output(List<ButtonActions> Actions)
    {
        this.Actions = Actions;
    }

}

// These list all of the Actions and Commands that are run.
//
// Technical debt:  Actions and Commands need to be consolidated.
// 
// XBox Controller drives Actions.
// Movie mode drives Commands
// 
public enum ButtonActions
{
   SubCommands,
   RepeatCommands,
   ServoAllGoHome,

   ServoMin,
   ServoMax,
   ServoHome,
   ServoValue,
   ServoHomeDelta,
   ServoGangedDelta,
   
   ServoModeValue,      
   ServoSetMode,
   ServoSetRandom,
   ServoSetRandomDisable,     
  
   ServoDelta,

   EyePopSetZero,
   EyePopOpen,
   EyePopNoSafety,
   EyePopLeftOpen,
   EyePopHalfOpen,
   EyePopClosed,
   EyePopLeftClosed,
   EyePopValue,

   RGBCommand,

    MaestroSet,
    MaestroSetAll,  

   DisableServo,
   DisableAllServos,
   DisableAllRunningServos,

    PlayFirst,
    PlayCurrent,
    PlayNext,
    PlayPrevious,
    PlayThis,

    TriggerSequence,
    Snapshot,
    SnapshotSeries,
}
