using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

public class SteamDeckController
{
}


[Flags]
public enum DeckButtons : uint
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LB = 1 << 4,
    RB = 1 << 5,
    Back = 1 << 6,   // View
    Start = 1 << 7,   // Menu
    LStick = 1 << 8,   // L3
    RStick = 1 << 9,   // R3
    DUp = 1 << 10,
    DDown = 1 << 11,
    DLeft = 1 << 12,
    DRight = 1 << 13,
    L4 = 1 << 14,  // Rear paddle
    L5 = 1 << 15,  // Rear paddle
    R4 = 1 << 16,  // Rear paddle
    R5 = 1 << 17,  // Rear paddle
    // Add others if you bind them via Steam Input
}

[Flags]
public enum DeckSources : uint
{
    None = 0,
    Sticks = 1 << 0,
    Triggers = 1 << 1,
    Buttons = 1 << 2,
    Touchpads = 1 << 3,
    Gyroscope = 1 << 4,
    Accelerometer = 1 << 5,
    Orientation = 1 << 6
}

public sealed class DeckInputPacket
{
    // ---- Header / metadata ----
    public int Version { get; set; } = 1;
    public int Sequence { get; set; }           // increments each sample
    public string DeviceId { get; set; } = "";     // e.g., "SteamDeck-ABC123"
    public DateTime TimestampUtc { get; set; }     // DateTime.UtcNow on sender
    public DeckSources SourcesPresent { get; set; }

    // ---- Gamepad-style ----
    public Sticks? Sticks { get; set; }
    public Triggers? Triggers { get; set; }
    public DeckButtons Buttons { get; set; }

    // ---- Motion ----
    public Motion? Motion { get; set; }               // gyro/accel
    public Orientation? Orientation { get; set; }     // quaternion

    // ---- Touchpads (left & right) ----
    public Touchpad? LeftPad { get; set; }
    public Touchpad? RightPad { get; set; }

    // Optional: battery %, RSSI, etc.
    public MiscTelemetry? Telemetry { get; set; }
}

public sealed class Sticks
{
    // Normalized -1..+1
    public float LX { get; set; }
    public float LY { get; set; }
    public float RX { get; set; }
    public float RY { get; set; }
}

public sealed class Triggers
{
    // Normalized 0..1
    public float LT { get; set; }
    public float RT { get; set; }
}

public sealed class Motion
{
    // Gyro in deg/s
    public float GyroX { get; set; }
    public float GyroY { get; set; }
    public float GyroZ { get; set; }

    // Accel in m/s^2
    public float AccelX { get; set; }
    public float AccelY { get; set; }
    public float AccelZ { get; set; }
}

public sealed class Orientation
{
    // Unit quaternion (W^2 + X^2 + Y^2 + Z^2 = 1)
    public float W { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class Touchpad
{
    // True when pad is physically clicked (button), if mapped
    public bool Clicked { get; set; }

    // Array of active touches (0..N)
    public TouchPoint[] Touches { get; set; } = Array.Empty<TouchPoint>();
}

public sealed class TouchPoint
{
    public int Id { get; set; }     // finger id from API
    public float X { get; set; }      // 0..1 normalized
    public float Y { get; set; }      // 0..1 normalized
    public float Pressure { get; set; } // 0..1 if available
    public bool Down { get; set; }
}

public sealed class MiscTelemetry
{
    public float? BatteryPercent { get; set; }  // 0..100, if available
    public float? TemperatureC { get; set; }    // if available
}
