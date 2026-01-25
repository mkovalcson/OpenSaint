using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TicT249Control;


/// <summary>
/// This code reads all the USB devices and decides what they are.
/// -Arduino 
/// -TicController
/// -Maestro
/// 
/// Limitations:
///     It currently has the Left Tic's SN hardcoded to differentiate between left and right.
///     There is currently only one Maestro Port being set.
/// 
/// </summary>

public enum USBDeviceType
{
    TicT249,
    Maestro, 
    Arduino
}

public class OutputDevices
{
    public string headPort {  get; set; }
    public RGBLight lights {  get; set; }
    public TicController LeftTic {  get; set; }
    public TicController RightTic {  get; set; }

    public OutputDevices(string headPort, RGBLight lights, TicController leftTic, TicController rightTic)
    {
        this.headPort = headPort;
        this.lights = lights;
        this.LeftTic = leftTic;
        this.RightTic = rightTic;
    }
}
public class USBDevices
{
    public static OutputDevices WindowsFindUSBDevices(string leftTicSN, string exeFolder)
    {

        // Get All the USB devices by Serial COM port
        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");

        var USBPorts = new List<UsbDevice>();

        foreach (ManagementObject port in searcher.Get())
        {
            string name = port["Name"]?.ToString(); // e.g., "Pololu Micro Maestro (COM4)"
            string deviceId = port["PNPDeviceID"].ToString(); // e.g., USB\VID_1FFB&PID_0089\00258715

            if (deviceId.Contains("_00"))
            {
               
                var newPort = new UsbDevice();
                newPort.PortName = port["DeviceID"].ToString();
                newPort.MaxBaudRate = Convert.ToInt32(port["MaxBaudRate"].ToString());
                newPort.SerialNumber = port["PNPDeviceID"].ToString().Split('\\')[2];
                USBPorts.Add(newPort);

                Console.WriteLine(port["DeviceID"]?.ToString() + " Serial: " + newPort.SerialNumber + " maxbaud:" + port["MaxBaudRate"]?.ToString() + " status " + port["Status"]?.ToString());
            }
         
        }      

        // Get USB devices by manufacturer Pololu
        var devices = USBDevices.GetUsbDevices();

        var headPort = "";
        RGBLight? lights = null;
        TicController leftTic = null;
        TicController rightTic = null;

        // Merge the Com port and baud rate data with the device
        foreach (var device in devices)
        {
            var sn = device.SerialNumber.Replace("_04","_00").Replace("0004","0000");
                        
            foreach (UsbDevice usbdev in USBPorts)
            {             
                if (usbdev.SerialNumber == sn)
                {
                    device.PortName = usbdev.PortName;
                    device.MaxBaudRate = usbdev.MaxBaudRate;
                    headPort = device.PortName;

                    var channeltemp = device.Name.Split('-')[0].Split(' ');
                    var numberofChannels = Convert.ToInt32(channeltemp[channeltemp.Length - 1]);
                    device.Channels = numberofChannels;

                    if (device.Name.Contains("Maestro"))
                    {
                        device.Type = USBDeviceType.Maestro;
                    }                    
                }
            }

            if (device.Name.Contains("T249"))
            {
                device.Type = USBDeviceType.TicT249;

                if (device.SerialNumber == leftTicSN)
                {
                    leftTic = new TicController(device.SerialNumber, exeFolder, RobotControls.LeftEyePop, 0, 2100);
                    device.isLeft = true;
                }
                else
                {
                    rightTic = new TicController(device.SerialNumber, exeFolder, RobotControls.RightEyePop, 0, 2100);
                    device.isLeft = false;
                }
            }

            else if (device.Name.Contains("CH340"))
            {
                device.Type = USBDeviceType.Arduino;
                device.RGBLight = new RGBLight(device.PortName);
                lights = new RGBLight(device.PortName);
                lights.Command("ClearAll");
            }

            Console.WriteLine("---------------------------------------------------");          
            Console.WriteLine($"Name         : {device.Name}");           
            Console.WriteLine($"SerialNumber : {device.SerialNumber}");
            Console.WriteLine($"Type : {device.Type.ToString()}");
            if(device.Type == USBDeviceType.TicT249)
            {
                Console.WriteLine(device.isLeft?"Left ":"Right "+"Eye");
            }

            if (device.Type == USBDeviceType.Maestro)
            {
                Console.WriteLine($"Port         : {device.PortName}");
                Console.WriteLine($"Baud         : {device.MaxBaudRate}");
                Console.WriteLine($"Channels     : {device.Channels}");
            }
        }
             

        Console.WriteLine();     

        if (lights == null)
        {
            Console.WriteLine("Unable to find Arduino");
            return null;
        }

        var outputDevices = new OutputDevices(headPort, lights, leftTic, rightTic);

        return outputDevices;

    }
    public static List<UsbDevice> GetUsbDevices()
    {
        var devices = new List<UsbDevice>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%' AND (Name LIKE '%Pololu%' OR Name LIKE '%CH340%') ");

        foreach (ManagementObject device in searcher.Get())
        {
            var uniqueId = device["DeviceID"].ToString().Split('\\')[2];
            var name = device["Name"].ToString();

            string port = ExtractComPort(name);

            devices.Add(new UsbDevice
            {
                Name = device["Name"]?.ToString(),
                DeviceID = device["DeviceID"].ToString(),
                SerialNumber = uniqueId,
                PortName = port
            });
        }

        return devices;
    }


    private static string ExtractComPort(string name)
    {
        var match = Regex.Match(name, @"\(COM\d+\)");
        return match.Success ? match.Value.Trim('(', ')') : null;
    }

}

public class UsbDevice
{
    public USBDeviceType Type { get; set; }
    public string Name { get; set; }
    public string DeviceID { get; set; }
    public string SerialNumber { get; set; }
    public string PortName { get; set; }
    public int MaxBaudRate { get; set; }
    public int Channels { get; set; }
    public RGBLight RGBLight { get; set; }
    public TicController Tic { get; set; }
    public bool isLeft {  get; set; }
}


