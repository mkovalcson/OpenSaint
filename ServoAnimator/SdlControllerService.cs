using System.IO;
using System.Runtime.InteropServices;

namespace ServoAnimator;

/// <summary>SDL 3.4.16 native gamepad access; all calls run on the WPF UI thread.
/// No key/mouse emulation and no device identification by generic XInput slot.</summary>
internal sealed class SdlControllerService : IDisposable
{
    internal sealed record Device(uint Id, ControllerKind Kind, string Name, nint Handle);
    private readonly Dictionary<uint, Device> _devices = new();
    public IReadOnlyCollection<Device> Devices => _devices.Values;
    public string Error { get; private set; }
    private bool _initialized;
    private long _lastScan;
    private const uint Subsystems = 0x00002000 | 0x00008000; // GAMEPAD and SENSOR

    public SdlControllerService()
    {
        try
        {
            Native.EnsureLoaded();
            Native.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
            Native.SDL_SetHint("SDL_JOYSTICK_HIDAPI_STEAM", "1");
            _initialized = Native.SDL_InitSubSystem(Subsystems);
            if (!_initialized) Error = Native.Error();
        }
        catch (Exception ex) { Error = "Controller driver unavailable: " + ex.Message; }
    }

    public void Update(bool scanNow = false)
    {
        if (!_initialized) return;
        Native.SDL_PumpEvents();
        // SDL events are not consumed by WPF. Discard them after the state update.
        Native.SDL_FlushEvents(0, 0xFFFF);
        Native.SDL_UpdateGamepads();
        foreach (var d in _devices.Values.Where(d => !Native.SDL_GamepadConnected(d.Handle)).ToArray())
        { Native.SDL_CloseGamepad(d.Handle); _devices.Remove(d.Id); }
        long now = Environment.TickCount64;
        if (!scanNow && now - _lastScan < 1000) return;
        _lastScan = now;
        nint ids = Native.SDL_GetGamepads(out int count);
        try
        {
            if (ids == 0) return;
            for (int i = 0; i < count; i++)
            {
                uint id = unchecked((uint)Marshal.ReadInt32(ids, i * 4));
                if (_devices.ContainsKey(id)) continue;
                int type = Native.SDL_GetGamepadTypeForID(id);
                // Stable SDL 3.4 identifies Steam through Valve VID, not the newer 3.6 enum.
                ControllerKind? kind = Native.SDL_GetGamepadVendorForID(id) == 0x28de ? ControllerKind.Steam : type is 2 or 3 ? ControllerKind.Xbox : null;
                if (kind == null) continue;
                nint handle = Native.SDL_OpenGamepad(id);
                if (handle == 0) { Error = Native.Error(); continue; }
                string name = Marshal.PtrToStringUTF8(Native.SDL_GetGamepadName(handle)) ?? kind.ToString();
                // The 2015 controller and Steam Deck are not the requested 2026 device.
                if (kind == ControllerKind.Steam && (!name.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) || !Native.SDL_GamepadHasButton(handle, 18) || Native.SDL_GetNumGamepadTouchpads(handle) < 2))
                { Native.SDL_CloseGamepad(handle); continue; }
                foreach (int sensor in new[] { 1, 2 })
                    if (Native.SDL_GamepadHasSensor(handle, sensor)) Native.SDL_SetGamepadSensorEnabled(handle, sensor, true);
                _devices[id] = new(id, kind.Value, name, handle);
            }
        }
        finally { if (ids != 0) Native.SDL_free(ids); }
    }

    public ControllerSample Read(ControllerKind kind)
    {
        var d = _devices.Values.Where(d => d.Kind == kind).OrderBy(d => d.Id).FirstOrDefault();
        if (d == null || !Native.SDL_GamepadConnected(d.Handle)) return new();
        var result = new ControllerSample { Connected = true, DeviceId = d.Id,
            LeftShoulder = Native.SDL_GetGamepadButton(d.Handle, 9), RightShoulder = Native.SDL_GetGamepadButton(d.Handle, 10) };
        string[] axes = { "LeftX", "LeftY", "RightX", "RightY", "LeftTrigger", "RightTrigger" };
        for (int axis = 0; axis < axes.Length; axis++)
        {
            if (!Native.SDL_GamepadHasAxis(d.Handle, axis)) continue;
            short raw = Native.SDL_GetGamepadAxis(d.Handle, axis);
            double value = raw / (raw < 0 ? 32768.0 : 32767.0);
            if (axis is 1 or 3) value = -value; // up is positive
            Add(axes[axis], axis >= 4 ? Math.Max(0, value) : value);
        }
        var buttons = new Dictionary<string, int> { ["A"] = 0, ["B"] = 1, ["X"] = 2, ["Y"] = 3,
            ["Back"] = 4, ["Guide"] = 5, ["Start"] = 6, ["LeftClick"] = 7, ["RightClick"] = 8,
            ["DpadUp"] = 11, ["DpadDown"] = 12, ["DpadLeft"] = 13, ["DpadRight"] = 14, ["Share"] = 15 };
        if (kind == ControllerKind.Steam)
        {
            buttons["LeftShoulder"] = 9; buttons["RightShoulder"] = 10;
            buttons["R4"] = 16; buttons["L4"] = 17; buttons["R5"] = 18; buttons["L5"] = 19;
            buttons["LeftPadClick"] = 20; buttons["RightPadClick"] = 21;
            buttons["LeftStickTouch"] = 22; buttons["RightStickTouch"] = 23;
            buttons["LeftGripTouch"] = 24; buttons["RightGripTouch"] = 25;
        }
        foreach (var pair in buttons)
            if (Native.SDL_GamepadHasButton(d.Handle, pair.Value)) Add(pair.Key, Native.SDL_GetGamepadButton(d.Handle, pair.Value) ? 1 : 0);
        if (kind == ControllerKind.Steam)
        {
            for (int pad = 0; pad < Math.Min(2, Native.SDL_GetNumGamepadTouchpads(d.Handle)); pad++)
            {
                string side = pad == 0 ? "Left" : "Right";
                if (!Native.SDL_GetGamepadTouchpadFinger(d.Handle, pad, 0, out bool down, out float x, out float y, out float pressure)) continue;
                Add(side + "PadTouch", down ? 1 : 0);
                foreach (string suffix in new[] { "X", "Y", "Pressure" }) result.Supported.Add(side + "Pad" + suffix);
                if (down) { Add(side + "PadX", x * 2 - 1); Add(side + "PadY", 1 - y * 2); Add(side + "PadPressure", Math.Clamp(pressure, 0, 1)); }
            }
            foreach (int sensor in new[] { 1, 2 })
            {
                var data = new float[3];
                if (!Native.SDL_GamepadHasSensor(d.Handle, sensor) || !Native.SDL_GetGamepadSensorData(d.Handle, sensor, data, 3)) continue;
                string[] names = sensor == 1 ? new[] { "AccelX", "AccelY", "AccelZ" } : new[] { "GyroPitch", "GyroYaw", "GyroRoll" };
                for (int i = 0; i < 3; i++) Add(names[i], data[i]);
            }
        }
        return result;
        void Add(string input, double value) { result.Values[input] = value; result.Supported.Add(input); }
    }

    public void Dispose()
    {
        if (!_initialized) return;
        foreach (var device in _devices.Values) Native.SDL_CloseGamepad(device.Handle);
        _devices.Clear(); Native.SDL_QuitSubSystem(Subsystems); _initialized = false;
    }

    private static class Native
    {
        private static bool _loaded;
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, assembly, path) => name == "J5_SDL3"
                ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Native", "SDL3", RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), "SDL3.dll")) : 0);
            _loaded = true;
        }
        public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "SDL error";
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern nint SDL_GetError();
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_InitSubSystem(uint flags);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_QuitSubSystem(uint flags);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_PumpEvents();
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_FlushEvents(uint min, uint max);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_UpdateGamepads();
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern nint SDL_GetGamepads(out int count);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GetGamepadTypeForID(uint id);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern ushort SDL_GetGamepadVendorForID(uint id);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern nint SDL_OpenGamepad(uint id);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_CloseGamepad(nint gamepad);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern nint SDL_GetGamepadName(nint gamepad);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GamepadConnected(nint gamepad);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GetGamepadButton(nint gamepad, int button);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GamepadHasButton(nint gamepad, int button);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GamepadHasAxis(nint gamepad, int axis);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern short SDL_GetGamepadAxis(nint gamepad, int axis);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GetNumGamepadTouchpads(nint gamepad);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GetGamepadTouchpadFinger(nint gamepad, int pad, int finger, [MarshalAs(UnmanagedType.I1)] out bool down, out float x, out float y, out float pressure);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GamepadHasSensor(nint gamepad, int type);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_SetGamepadSensorEnabled(nint gamepad, int type, [MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GetGamepadSensorData(nint gamepad, int type, [Out] float[] data, int count);
        [DllImport("J5_SDL3", CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_free(nint memory);
    }
}
