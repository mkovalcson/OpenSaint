using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Included Arduino Sketch file is compatible with these commands.
/// 
/// /ArduinoCode/ArduinoOpenSaintRGB.ino
/// 
/// Assumes dedicated Arduino out pin per RGB Ring Light.
/// 
/// sketch file can be adjusted
///
/// #define LEFTEYE_PIN 7 // Arduino pin out may vary
/// #define LEFTVENT_PIN 8
/// #define RIGHTEYE_PIN 9
/// #define RIGHTVENT_PIN 10
///
///
/// </summary>
public class RGBLight
{
    public SerialPort arduino; 

    public RGBLight(string serialPort)
    {
        arduino = new SerialPort(serialPort, 115200);
    }
    public void Command(string command)
    {
        arduino.Open();
        arduino.WriteLine(command);    
        arduino.Close();
    }

    // Arguments are 
    // ring = EYES, VENTS, BOTH
    // lr =  LEFT, RIGTH, LR
    // red, green, blue  (0-255)
    // brightness ( 0-255 )
    // lowest brightness (0-255)
    // delay = ms
    // pulses = int
    // step = (0-255)
    // fadeIn = IN, OUT

    // Single Action
    //CLEARALL
    //CLEAR 
    //SETRGBCOLOR
        
    //CYLON
    //COLORWIPEEYES
    //FADE
    //PULSE
    //THEATERCHASE
    //RAINBOWCHASE
    //RAINBOWCYCLE


    // The Commands below are unused, but show how to assemble various commands.
    public enum Ring
    {
        Eyes = 0,
        Vents = 1,
        Both = 2
    }

    public enum Side
    {
        Left = 0,
        Right = 1,
        LR = 2
    }

    public enum FadeDirection
    {
        In = 0,
        Out = 1
    }

    public static string ClearAll()
    {
        return "ClearAll";
    }
    /// <summary>
    /// Clear - clears specified ring(s)
    /// </summary>
    /// <param name="ring"></param>
    /// <param name="side"></param>
    public static string Clear(Ring ring, Side side) {

        var command = string.Format("Clear,{0},{1}",ring.ToString(),side.ToString());
        return command;
    }

    /// <summary>
    /// SetRGBColor sets color of specified ring(s)
    /// </summary>
    /// <param name="red">0-255</param>
    /// <param name="green">0-255</param>
    /// <param name="blue">0-255</param>
    /// <param name="brightness">0-255</param>
    /// <param name="ring"></param>
    /// <param name="side"></param>   
    public static string SetRGBColor(byte red, byte green, byte blue, byte brightness, Ring ring, Side side) {
        var command = string.Format("SetRGBColor,{0},{1},{2},{3},{4},{5}",red,green,blue,brightness,ring.ToString(),side.ToString());
        return (command);
    }

    /// <summary>
    /// ColorWipeEyes Circular color wipe
    /// </summary>
    /// <param name="red">0-255</param>
    /// <param name="green">0-255</param>
    /// <param name="blue">0-255</param>
    /// <param name="brightness">0-255</param>
    /// <param name="ring"></param>
    /// <param name="side"></param>
    /// <param name="delayms">mSec</param>
    public static string ColorWipeEyes(byte red, byte green, byte blue, byte brightness, Ring ring, Side side, uint delayms)
    {
        var command = string.Format("ColorWipeEyes,{0},{1},{2},{3},{4},{5},{6}", red, green, blue, brightness, ring.ToString(), side.ToString(), delayms);
        return (command);
    }

    /// <summary>
    /// Fade - fades color up or down for specified ring(s)
    /// </summary>
    /// <param name="red">0-255</param>
    /// <param name="green">0-255</param>
    /// <param name="blue">0-255</param>
    /// <param name="brightness">0-255</param>
    /// <param name="ring"></param>
    /// <param name="side"></param>
    /// <param name="delayms">mSec</param>
    /// <param name="fadeDirection">In/Out</param>
    /// <param name="step">0-255</param>
    /// <param name="lowestBrightness">0-255</param>
    public static string Fade(byte red, byte green, byte blue, byte brightness, Ring ring, Side side, uint delayms, FadeDirection fadeDirection, byte step, byte lowestBrightness )
    {
        var command = string.Format("Fade,{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", red, green, blue, brightness, ring.ToString(), side.ToString(), delayms, fadeDirection.ToString(),step,lowestBrightness);
        return (command);
    }

    /// <summary>
    /// Pulse - fades color brightness up and down number of pulses for specified ring(s)
    /// </summary>
    /// <param name="red">0-255</param>
    /// <param name="green">0-255</param>
    /// <param name="blue">0-255</param>
    /// <param name="brightness">0-255</param>
    /// <param name="ring"></param>
    /// <param name="side"></param>
    /// <param name="delayms">mSec</param>
    /// <param name="numberPulses">How many pulse cycles</param>
    /// <param name="step">0-255</param>
    /// <param name="lowestBrightness">0-255</param>
    public static string Pulse(byte red, byte green, byte blue, byte brightness, Ring ring, Side side, uint delayms, uint numberPulses, byte brightnessStep, byte lowestBrightness)
    {
        var command = string.Format("Pulse,{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", red, green, blue, brightness, ring.ToString(), side.ToString(), delayms, numberPulses, brightnessStep, lowestBrightness);
        return command;
    }

    // eyes only below this

    /// <summary>
    /// TheaterChase - fades color brightness up and down number of pulses for specified ring(s)
    /// </summary>
    /// <param name="red">0-255</param>
    /// <param name="green">0-255</param>
    /// <param name="blue">0-255</param>
    /// <param name="brightness">0-255</param>
    /// <param name="ring"></param>
    /// <param name="side"></param>
    /// <param name="delayms">mSec ex(40)</param>
    /// <param name="cycles">How many times the chase circles ex(10)</param>
    public static string TheaterChase(byte red, byte green, byte blue, byte brightness, Ring ring, Side side, uint delayms, uint cycles)
    {
        var command = string.Format("TheaterChase,{0},{1},{2},{3},{4},{5},{6},{7}", red, green, blue, brightness, ring.ToString(), side.ToString(), delayms, cycles);
        return command;
    }

    /// <summary>
    /// Rainbow - fades color brightness up and down number of pulses for specified ring(s)
    /// </summary>  
    /// <param name="brightness">0-255</param> 
    /// <param name="side"></param>
    /// <param name="delayms">mSec ex(20)</param>   
    public static string Rainbow(byte brightness, Side side, uint delayms)
    {
        var command = string.Format("Rainbow,{0},{1},{2}", brightness, side.ToString(), delayms);
        return command;
    }

    /// <summary>
    /// RainbowWipe - color wipe for specified sides(s) and cycles
    /// </summary>  
    /// <param name="brightness">0-255</param> 
    /// <param name="side"></param>
    /// <param name="delayms">mSec ex(20)</param> 
    /// <param name="cycles">number of cycles</param>
    public static string RainbowWipe(byte brightness, Side side, uint delayms, uint cycles)
    {
        var command = string.Format("RainbowWipe,{0},{1},{2}", brightness, side.ToString(), delayms, cycles);
        return command;
    }

    /// <summary>
    /// Rainbow - rainbow colored chase lights for specified ring(s)
    /// </summary>  
    /// <param name="brightness">0-255</param> 
    /// <param name="side"></param>
    /// <param name="delayms">mSec ex(20)</param>   
    //public void RainbowChase(byte brightness, Side side, uint delayms)
    //{
    //    var command = string.Format("RainbowChase,{0},{1},{2}", brightness, side.ToString(), delayms);
    //    Command(command);
    //}
   
    public static string RainbowCycle(byte brightness, Side side, uint delayms)
    {
        var command = string.Format("RAINBOWCYCLE,{0},{1},{2}", brightness, side.ToString(), delayms);
        return command;
    }

    public static string RainbowChase(byte brightness, Side side, uint delayms)
    {
        var command = string.Format("RainbowChase,{0},{1},{2}", brightness, side.ToString(), delayms);
        return command;
    }

}

