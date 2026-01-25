using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


public enum TriggerSource
{
    Pedals,
    Automatic,
    XBox
}

// Defined list of servos that typically move together
public enum GangedServoNames
{
    NeckNodUp = 0,
    NeckTiltRight = 1,
    FlapsOpen = 2, 
    FlapTiltUp = 3,  
    IrisClose = 4, 
    EyesVerticalUp = 5,
    EyesHorizontalRight = 6,
    VentsOpen = 7,
}

/// <summary>
/// This is the top level class for  Animation Sequences
/// 
/// The automated Background sequences are not complete
/// 
/// </summary>
public class Movie
{
    public bool CommandsExpanded {  get; set; }  // just a flag to verify all repeated or nested commands are expanded.
    public bool Done { get; set; } // The Movie has run to completion
    public string MovieName { get; set; }   
    public string MovieFolder { get; set; }
    public Sequence? BackgroundMotion { get; set; } // Sequence that automatically repeats between scenes
    public int msInactivityTimeout { get; set; }
    public DateTime BackgroundStart { get; set; }
    public TriggerSource Trigger { get; set; } // Manual or Automatic        
    public int SceneIndex { get; set; }
    public List<Scene> Scenes { get; set; } 
    public bool IsRepeating { get; set; }   
   
    public Movie(string movieName, string movieFolder , List<Scene> scenes, TriggerSource triggerSource)
    {
        this.Done=false;
        this.CommandsExpanded = false;
        MovieName = movieName;
        BackgroundMotion = null;
        BackgroundStart = DateTime.MaxValue;
        this.MovieFolder = movieFolder;
        Scenes = scenes;
        Trigger = triggerSource;      
        msInactivityTimeout = 5000; // default 5 seconds
        IsRepeating = false;
        SceneIndex = 0;       
    }
 
}
public class Scene
{
    public string SceneName { get; set; }
  //  public TriggerSource Trigger { get; set; } // Manual or Automatic 

    public List<Sequence> Sequences { get; set; }
    public bool SceneRunning { get; set; }
    public int AudioIndex { get; set; }
    public int SequenceIndex { get; set; }   
    public List<Sequence> RepeatingSequences { get; set; } 
    public List<string> AudioTracks { get; set; }

    public  Scene(string SceneName) {  //}, TriggerSource trigger) {
        this.Sequences = new List<Sequence>();   
        this.RepeatingSequences = new List<Sequence>();
        this.AudioTracks = new List<string>();
        this.SceneName = SceneName;
       // this.Trigger = trigger;
        this.SequenceIndex = 0;
        this.AudioIndex = 0;
        this.SceneRunning = false;
    }
}

public class Sequence
{
    public int[] StartingServoPosition { get; set; }
    public ServoSpeed[] StartingServoSpeed { get; set; }
    public string lastRGBCommand { get; set; }
    public int[] EyePosition { get; set; }
    public List<Command> CommandList { get; set; }
    public int CommandIndex { get; set; }
    public TimeSpan MsDelay { get; set; }
    public TimeSpan BackgroundMsDelay { get; set; }
    public DateTime FireTime { get; set; }
    public bool IsComplete { get; set; }
    public int iterations { get; set; }
    public int iterationCount { get; set; }

    [JsonConstructor]

    public Sequence(List<Command> CommandList, int msDelay)   
    {
        StartingServoPosition = new int[24];
        StartingServoSpeed = new ServoSpeed[24];
        EyePosition = new int[2];
        this.CommandList = CommandList; 
        this.MsDelay = TimeSpan.FromMilliseconds(msDelay);
        this.BackgroundMsDelay = TimeSpan.FromMilliseconds(1000);
        this.CommandIndex = 0;
        this.FireTime = DateTime.MinValue;
        this.IsComplete = false;
        this.iterations = 0;
        this.iterationCount = 0;
         
    }

    public Sequence(List<Command> CommandList, int msDelay, int iterations)
    {
        StartingServoPosition = new int[24];
        StartingServoSpeed = new ServoSpeed[24];
        EyePosition = new int[2];
        this.CommandList = CommandList;
        this.MsDelay = TimeSpan.FromMilliseconds(msDelay);
        this.BackgroundMsDelay = TimeSpan.FromMilliseconds(1000);
        this.CommandIndex = 0;
        this.FireTime = DateTime.MinValue;
        this.IsComplete = false;
        this.iterationCount = 0;
        this.iterations = iterations;
    }

}

public class GangedServo
{
    public RobotControls control;
    public MultiOutput orientation;

    public GangedServo(RobotControls control, MultiOutput orientation)
    {
        this.control = control;
        this.orientation = orientation;
    }
}

public class GangedServoList
{
    public bool isCentered;
    public List<GangedServo> list;
    public GangedServoList(List<GangedServo> list, bool isCentered)
    {
        this.list = list;
        this.isCentered = isCentered;
    }
}

public class Command
{
    public RobotControls robotControl;
    public GangedServoNames? GangedServosName {  get; set; }
    public List<Command> SubCommands { get; set; }
    public int RepeatLoops { get; set; }
    public TimeSpan RepeatDelay { get; set; }
    public ButtonActions Action { get; set; }
    public string? pathName{ get; set; }
    public int count{ get; set; }
    public TimeSpan Delay{ get; set; }
    public DateTime TimeToFire { get; set; }
    public DateTime? lastFired{ get; set; }   
    public int Value { get; set; }
    public double Percent {  get; set; }
    public ServoSpeed Speed { get; set; }
    public ServoMode Mode { get; set; }     
    public int TopValue { get; set; }
    public int BottomValue {  get; set; }
    public int minDelay { get; set; }
    public int maxDelay  { get; set; }
    public bool OffsetDelay { get; set; }  
    public int DisableDelay { get; set; }
       
    public Command()
    {
        SubCommands = new List<Command>();
    }
    public Command Clone()
    {
        return new Command
        {
            robotControl = this.robotControl,
            GangedServosName = this.GangedServosName,
            RepeatLoops = this.RepeatLoops,
            RepeatDelay = this.RepeatDelay,
            Action = this.Action,
            pathName = this.pathName,
            count = this.count,
            Delay = this.Delay,
            TimeToFire = this.TimeToFire,
            lastFired = this.lastFired,
            Value = this.Value,
            Percent = this.Percent,
            Speed = this.Speed,
            Mode = this.Mode,
            TopValue = this.TopValue,
            BottomValue = this.BottomValue,
            minDelay = this.minDelay,
            maxDelay = this.maxDelay,
            OffsetDelay = this.OffsetDelay,
            DisableDelay = this.DisableDelay,

            SubCommands = this.SubCommands != null
                ? this.SubCommands.Select(c => c.Clone()).ToList()
                : null
        };
    }

    public Command(ButtonActions Action, int msDelay, List<Command> subCommands)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;
        this.SubCommands = subCommands;
        this.RepeatLoops = 0;
        this.RepeatDelay = TimeSpan.FromMilliseconds(0);
        Init();
    }
    public Command(ButtonActions Action, int msDelay, List<Command> subCommands, int repeatLoops, int repeatdelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;
        this.SubCommands = subCommands;
        this.RepeatLoops = repeatLoops;
        this.RepeatDelay = TimeSpan.FromMilliseconds(repeatdelay);
        Init();
    }
    public Command(ButtonActions Action, ServoSpeed Speed, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;
        this.Speed = Speed;

        Init();
    }
    public Command(RobotControls ControlName, ButtonActions Action, ServoSpeed Speed, int msDelay)
    {
        this.robotControl = ControlName;
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action =  Action;      
        this.Speed = Speed;

        Init();
    }
    public Command(ButtonActions Action, ServoMode mode, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;     
        this.Mode = mode;
       Init();
    }
    public Command(ButtonActions Action, string pathname, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;      
        this.pathName = pathname;
       
        Init();
    }
    public Command(RobotControls ControlName, ButtonActions Action, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.robotControl = ControlName;
        this.Action = Action;

        Init();
    }

    public Command(RobotControls ControlName, ButtonActions Action)
    {
        this.Delay = TimeSpan.FromMilliseconds(0);
        this.robotControl = ControlName;
        this.Action = Action;

        Init();
    }

    public Command(RobotControls ControlName, ButtonActions Action, double Percent, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.robotControl = ControlName;
        this.Action = Action;
        this.Percent = Percent;

        Init();
    }
    public Command(RobotControls ControlName, ButtonActions Action, int Value, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.robotControl = ControlName;
        this.Action = Action;
        this.Value = Value;

        Init();
    }

    public Command(RobotControls ControlName, ButtonActions Action, int Value, int msDelay, bool offsetDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.robotControl = ControlName;
        this.Action = Action;
        this.Value = Value;
        this.OffsetDelay = offsetDelay;
        Init();
    }
    public Command(ButtonActions Action, GangedServoNames gangedServoNames, int Value, int msDelay)
    {
        this.GangedServosName = gangedServoNames;
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;
        this.Value = Value;

        Init();
    }
    public Command(ButtonActions Action, int Value, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;     
        this.Value =  Value;

      Init();
    }   

    public Command(ButtonActions Action, int msDelay)
    {
        this.Delay = TimeSpan.FromMilliseconds(msDelay);
        this.Action = Action;

        Init();
    }  

    internal void Init()
    {
        this.count = 0;       
        this.lastFired = null;
        this.OffsetDelay = false;
    }

}

