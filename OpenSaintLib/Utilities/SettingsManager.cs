using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TicT249Control;

// This is the top level Setting object that holds the entire configuration.
// 
// Eventually this and everything under it, except Movies should be saved to a JSON file.
//

public class SettingsObject
{
    public bool EyesPopped { get; set; }
    public bool NoseUp { get; set; }
    public string SnapshotBaseName { get; set; }
    public DateTime EyesUnpopped { get; set; }
    public Movie? SelectedMovie { get; set; }
    public Servo[] Servos { get; set; }
    public IOMapping IO_Mapping { get; set; }
    public List<IOMap> IO_Delay { get; set; }
    public int SoundBiteIndex { get; set; }
    public string[] SoundBites { get; set; }
    public TicController LTicController { get; set; }
    public TicController RTicController { get; set; }
    public RGBLight Lights { get; set; }
    public GangedServoList[] GangedServoList { get; set; }
    
    // This is only to allow serialization which currently isn't possible.
    public SettingsObject()
    {
        this.Servos = new Servo[0];
    }
    //List<UsbDevice> OutPutDevices,
    public SettingsObject(IOMapping Mapping, Servo[] Servos, TicController LTicController, TicController RTicController,
        RGBLight Lights)
    {
        this.NoseUp = false;
        this.EyesPopped = false;
        this.IO_Mapping = Mapping;
        this.Servos = Servos;
        this.LTicController = LTicController;
        this.RTicController = RTicController;
        this.Lights = Lights;
        this.IO_Delay = new List<IOMap>();
        this.SoundBites = System.Array.Empty<string>();
        this.GangedServoList = CreateGangedList();        
    }

    /// <summary>
    /// This is a hard coded lists of all the ganged servos so they can be called with a single command.
    /// </summary>
    /// <returns></returns>
    public GangedServoList[] CreateGangedList()
    {
        var numberGanged = 8;

        var list = new GangedServoList[numberGanged];

        var neckNod = new List<GangedServo>
        {
            new GangedServo(RobotControls.NeckTiltLeft, MultiOutput.Normal),
            new GangedServo(RobotControls.NeckTiltRight, MultiOutput.Reversed)
        };
        list[(int)GangedServoNames.NeckNodUp] = new GangedServoList(neckNod, true);

        var neckTilt = new List<GangedServo>
        {
            new GangedServo(RobotControls.NeckTiltLeft, MultiOutput.Reversed),
            new GangedServo(RobotControls.NeckTiltRight, MultiOutput.Reversed)
        };
        list[(int)GangedServoNames.NeckTiltRight] = new GangedServoList(neckTilt, true);

        var flapsOpen = new List<GangedServo>
        {
            new GangedServo(RobotControls.BrowLeftTopOpen, MultiOutput.Normal),
            new GangedServo(RobotControls.BrowLeftBottomOpen, MultiOutput.Normal),
            new GangedServo(RobotControls.BrowRightTopOpen, MultiOutput.Reversed),
            new GangedServo(RobotControls.BrowRightBottomOpen, MultiOutput.Reversed),
        };
        list[(int)GangedServoNames.FlapsOpen] = new GangedServoList(flapsOpen, true);

        var flapsTilt = new List<GangedServo>
        {
            new GangedServo(RobotControls.BrowLeftTopTilt, MultiOutput.Reversed),
            new GangedServo(RobotControls.BrowRightTopTilt, MultiOutput.Normal)
        };
        list[(int)GangedServoNames.FlapTiltUp] = new GangedServoList(flapsTilt, true);

        var irisClose = new List<GangedServo>
        {
            new GangedServo(RobotControls.LeftIris, MultiOutput.Normal),
            new GangedServo(RobotControls.RightIris, MultiOutput.Normal)
        };
        list[(int)GangedServoNames.IrisClose] = new GangedServoList(irisClose, false);

        var eyesVertical = new List<GangedServo>
        {
            new GangedServo(RobotControls.LeftLensVertical, MultiOutput.Reversed),
            new GangedServo(RobotControls.RightLensVertical, MultiOutput.Normal)
        };
        list[(int)GangedServoNames.EyesVerticalUp] = new GangedServoList(eyesVertical, true);

        var eyesHorizontal = new List<GangedServo>
        {
            new GangedServo(RobotControls.LeftLensHorizontal, MultiOutput.Reversed),
            new GangedServo(RobotControls.RightLensHorizontal, MultiOutput.Reversed)
        };
        list[(int)GangedServoNames.EyesHorizontalRight] = new GangedServoList(eyesHorizontal, true);

        var ventsOpen = new List<GangedServo>
        {
            new GangedServo(RobotControls.LeftEyeVent, MultiOutput.Normal),
            new GangedServo(RobotControls.RightEyeVent, MultiOutput.Reversed)
        };
        list[(int)GangedServoNames.VentsOpen] = new GangedServoList(ventsOpen, false);

        return list;
    }
}

public static class OpenSaintSettings
{
    private const string ConfigFile = "OpenSaintSettings.json";

    public static void Save(SettingsObject settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(ConfigFile, json);
    }

    public static SettingsObject Load()
    {
        if (!File.Exists(ConfigFile))
            return new SettingsObject();

        string json = File.ReadAllText(ConfigFile);
        return JsonSerializer.Deserialize<SettingsObject>(json);
    }
}