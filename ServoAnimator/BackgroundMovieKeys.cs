using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ServoAnimator;

/// <summary>Only the four plain movie arrows are registered, with key repeat disabled.</summary>
internal sealed class BackgroundMovieKeys : IDisposable
{
    private const int IdBase = 0x4A00, WmHotkey = 0x0312;
    private static readonly Key[] Keys = { Key.Up, Key.Right, Key.Left, Key.Down };
    private readonly nint _handle;
    private readonly HwndSource _source;
    private readonly Action<Key> _pressed;
    private bool _requested;
    private bool _disposed;
    private readonly List<int> _registered = new();
    public bool Enabled => _registered.Count == Keys.Length;
    public string Error { get; private set; }
    public BackgroundMovieKeys(Window owner, Action<Key> pressed)
    {
        _handle = new WindowInteropHelper(owner).Handle;
        _source = HwndSource.FromHwnd(_handle) ?? throw new InvalidOperationException("Editor window is not ready for hotkeys.");
        _pressed = pressed; _source.AddHook(Hook);
    }
    public void SetEnabled(bool enabled)
    {
        if (_requested == enabled) return;
        _requested = enabled; Clear(); Error = null;
        if (!enabled) return;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!RegisterHotKey(_handle, IdBase + i, 0x4000, (uint)KeyInterop.VirtualKeyFromKey(Keys[i])))
            {
                Error = "Movie background arrows are unavailable: " + new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Clear(); return;
            }
            _registered.Add(IdBase + i);
        }
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        int index = (int)wParam - IdBase;
        if (message == WmHotkey && Enabled && index >= 0 && index < Keys.Length)
        { handled = true; _pressed(Keys[index]); }
        return 0;
    }
    private void Clear() { foreach (int id in _registered) UnregisterHotKey(_handle, id); _registered.Clear(); }
    public void Dispose() { if (_disposed) return; SetEnabled(false); _source.RemoveHook(Hook); _disposed = true; }
    public static bool IsEditorForeground()
    { GetWindowThreadProcessId(GetForegroundWindow(), out uint process); return process == Environment.ProcessId; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
