using SharpDX.XInput;

/// <summary>
/// Describe the XBox controller
/// 
/// Inputs by name and type
/// 
/// Left and Right shoulders special case for Mulitplex control ( 00, 01, 10, 11 )
/// 
/// Holds Control Value and if it has changed.
/// 
/// When processing XBox inputs it aggregates 3 lists of only the inputs that have changed.
/// 
/// CAxis, CButtons, CTriggers
/// 
/// </summary>

public enum XBoxControlNames
{
    None,
    LJoyY,
    LJoyX, 
    RJoyX,    
    RJoyY,   
    DpadDown, 
    DPadUp,
    DPadLeft,   
    DPadRight,
    A,
    B,
    X,
    Y,  
    Start,  
    End,  
    LeftShoulder,   
    RightShoulder,  
    LTrigger,
    RTrigger,
    LThumb,
    RThumb
}

public class XBoxController
{   
    public bool diagnosticVerbose { get; set; }   
    public Control LMUX { get; set; }
    public Control RMUX { get; set; }
    public List<Control> Axis { get; set; }
    public List<Control> CAxis { get; set; }
    public List<Control> Buttons { get; set; }
    public List<Control> CButtons { get; set; }
    public List<Control> Triggers { get; set; }
    public List<Control> CTriggers { get; set; }

    public XBoxController(int joyStickDeadband, bool verbose)
    {
        diagnosticVerbose = verbose;
      
        LeftShoulder = new Control(ControlType.Button, 0, "LeftShoulder");
        LMUX = LeftShoulder;
        if (LMUX.Value == null) LMUX.Value = 0;
        RightShoulder = new Control(ControlType.Button, 0, "RightShoulder");
        RMUX = RightShoulder;
        if (RMUX.Value == null) RMUX.Value = 0;

        Axis = new List<Control>();
        LeftJoyY = new Control(ControlType.Axis, joyStickDeadband, "LJoyY");
        Axis.Add(LeftJoyY); 
        LeftJoyX = new Control(ControlType.Axis, joyStickDeadband, "LJoyX");
        Axis.Add(LeftJoyX);

        RightJoyX = new Control(ControlType.Axis, joyStickDeadband, "RJoyX");
        Axis.Add(RightJoyX);
        RightJoyY = new Control(ControlType.Axis, joyStickDeadband, "RJoyY");
        Axis.Add(RightJoyY);

        Buttons = new List<Control>();
        DPadDown = new Control(ControlType.Button, 0, "DpadDown");
        Buttons.Add(DPadDown);
        DPadUp = new Control(ControlType.Button,0, "DPadUp");
        Buttons.Add(DPadUp);
        DPadLeft = new Control(ControlType.Button,0, "DPadLeft");
        Buttons.Add(DPadLeft);
        DPadRight = new Control(ControlType.Button, 0, "DPadRight");
        Buttons.Add(DPadRight);

        AButton = new Control(ControlType.Button, 0,  "A");
        Buttons.Add(AButton);
        BButton = new Control(ControlType.Button, 0, "B"  );
        Buttons.Add(BButton);
        XButton = new Control(ControlType.Button, 0, "X");
        Buttons.Add(XButton);
        YButton = new Control(ControlType.Button, 0, "Y");
        Buttons.Add(YButton);
     
        StartButton = new Control(ControlType.Button, 0, "Start");
        Buttons.Add(StartButton);
        BackButton = new Control(ControlType.Button, 0, "End");
        Buttons.Add(BackButton);

        LeftThumbButton = new Control(ControlType.Button, 0, "LeftThumb");
        Buttons.Add(LeftThumbButton);
        RightThumbButton = new Control(ControlType.Button, 0, "RightThumb");
        Buttons.Add(RightThumbButton);

        Triggers = new List<Control>();
        LeftTrigger = new Control(ControlType.Trigger,0, "LTrigger");
        Triggers.Add(LeftTrigger);
        RightTrigger = new Control(ControlType.Trigger,0, "RTrigger");
        Triggers.Add(RightTrigger);
    }

    //public Control None { get; set; }
    public Control BackButton{ get; set; }
    public Control StartButton{ get; set; }

    public Control LeftThumbButton{ get; set; } 
    public Control RightThumbButton{ get; set; }

    public Control LeftShoulder{ get; set; }
    public Control RightShoulder{ get; set; }

    public Control LeftTrigger{ get; set; }
    public Control RightTrigger{ get; set; }

    public Control LeftJoyY{ get; set; }
    public Control LeftJoyX{ get; set; }

    public Control RightJoyY{ get; set; }
    public Control RightJoyX{ get; set; }

    public Control DPadRight{ get; set; }
    public Control DPadLeft{ get; set; }
    public Control DPadUp{ get; set; }
    public Control DPadDown{ get; set; }

    public Control AButton{ get; set; }
    public Control BButton{ get; set; }
    public Control XButton{ get; set; }
    public Control YButton{ get; set; }

    /// <summary>
    /// Start with an empty list of inputs and add only the active inputs 
    /// for buttons, axis, triggers to the controls lists to be parsed
    /// </summary>
    /// <param name="gamepad"></param>
    public void ProcessInputs(Gamepad gamepad)
    {
        ProcessMUX(gamepad, LeftShoulder, GamepadButtonFlags.LeftShoulder);
        ProcessMUX(gamepad, RightShoulder, GamepadButtonFlags.RightShoulder);
       
        CButtons = new List<Control>();          

        ProcessButton(gamepad, LeftThumbButton, GamepadButtonFlags.LeftThumb);
        ProcessButton(gamepad, RightThumbButton, GamepadButtonFlags.RightThumb);

        ProcessButton(gamepad, StartButton, GamepadButtonFlags.Start);
        ProcessButton(gamepad, BackButton, GamepadButtonFlags.Back);

        ProcessButton(gamepad, AButton, GamepadButtonFlags.A);
        ProcessButton(gamepad, BButton, GamepadButtonFlags.B);
        ProcessButton(gamepad, XButton, GamepadButtonFlags.X);
        ProcessButton(gamepad, YButton, GamepadButtonFlags.Y);

        ProcessButton(gamepad, DPadUp, GamepadButtonFlags.DPadUp);
        ProcessButton(gamepad, DPadDown, GamepadButtonFlags.DPadDown);
        ProcessButton(gamepad, DPadLeft, GamepadButtonFlags.DPadLeft);
        ProcessButton(gamepad, DPadRight, GamepadButtonFlags.DPadRight);

        CAxis = new List<Control>();

        ProcessAxis(gamepad.LeftThumbX, LeftJoyX);
        ProcessAxis(gamepad.LeftThumbY, LeftJoyY);
        ProcessAxis(gamepad.RightThumbX, RightJoyX);
        ProcessAxis(gamepad.RightThumbY, RightJoyY);

        CTriggers = new List<Control>();

        ProcessTrigger(gamepad.LeftTrigger, LeftTrigger);
        ProcessTrigger(gamepad.RightTrigger, RightTrigger);
    }

    /// <summary>
    /// If Axis is past deadband limit set the positive or negative value
    /// to the Control Axis list to be processed.
    /// </summary>
    /// <param name="axis"></param>
    /// <param name="control"></param>
    private void ProcessAxis(short axis, Control control)
    {
        // Joysticks
        if (axis > control.Deadband || axis < -control.Deadband)
        {  

            if (control.Value != axis)
            {
                if (diagnosticVerbose) Console.WriteLine(control.Name + " - " + axis);
                control.Value = axis;
                CAxis.Add(control);
            }
        }
        else if (control.Value != 0)
        {
            if(diagnosticVerbose)Console.WriteLine(control.Name + " - Released");
            control.Value = 0;
            CAxis.Add(control);
        }
    }

    /// <summary>
    /// If Trigger value is greater than 0 add it
    /// to the Control Trigger list to be processed.
    /// </summary>
    /// <param name="inputValue"></param>
    /// <param name="control"></param>
    private void ProcessTrigger(byte inputValue, Control control)
    {      

        if (inputValue > 0)
        {            
            if (control.Value != inputValue)
            {
                if (diagnosticVerbose) Console.WriteLine(control.Name + " - " + inputValue);
                control.Value = inputValue;
                CTriggers.Add(control); 
            }
        }
        else if (control.Value != 0)
        {
            if(diagnosticVerbose)Console.WriteLine(control.Name + " Released");
            control.Value = 0;
            CTriggers.Add(control);
        }
    }

    /// <summary>
    /// If the Button specified by the flag has changed
    /// Add it to the Control Button List
    /// </summary>
    /// <param name="gamepad"></param>
    /// <param name="control"></param>
    /// <param name="flag"></param>
    private void ProcessButton(Gamepad gamepad, Control control, GamepadButtonFlags flag)
    {       
        if ((gamepad.Buttons & flag) == flag)
        {
            if (control.Value ==  null || control.Value == 0)
            {
                if(diagnosticVerbose)Console.WriteLine(flag.ToString() + " Pressed");
                control.Value = 1;
                CButtons.Add(control);
            }            
        }
        else if (control.Value == 1)
        {
            if(diagnosticVerbose)Console.WriteLine(flag.ToString() + " UnPressed");
            control.Value = 0;
            CButtons.Add(control);
        }
    }

    /// <summary>
    /// The Shoulder buttons are a special case and are not processed
    /// They are read directly for every loop to decide how each of the other controls should be used
    /// </summary>
    /// <param name="gamepad"></param>
    /// <param name="control"></param>
    /// <param name="flag"></param>
    private void ProcessMUX(Gamepad gamepad, Control control, GamepadButtonFlags flag)
    {
        if ((gamepad.Buttons & flag) == flag)
        {
            if (control.Value == null || control.Value == 0)
            {
                if (diagnosticVerbose) Console.WriteLine(flag.ToString() + " Pressed");
                control.Value = 1;              
            }
        }
        else if (control != null && control.Value == 1)
        {
            if (diagnosticVerbose) Console.WriteLine(flag.ToString() + " UnPressed");
            control.Value = 0;          
        }
    }


  
}

    public enum ControlType
    {
        Button,
        Axis, 
        Trigger
    }

public class Control
{
    public string Name { get; set; }
    public int? Value { get; set; }
    public bool? ValueChanged { get; set; }
    public int Deadband { get; set; }
    public int Low { get; set; }
    public int High { get; set; }
    public ControlType ControlType { get; set; }

    public Control(ControlType controlType, int deadband, string name)
    {
        this.Deadband = deadband;
        this.Name = name;
        ControlType = controlType;

        switch (ControlType)
        {
            case ControlType.Button:
                this.Low = 0;
                this.High = 1;
                break;
            case ControlType.Axis:
                this.Low = -32767;
                this.High = 32767;
                break;
            case ControlType.Trigger:
                this.Low = 0;
                this.High = 255;
                break;
        }

    }  
}


