using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroDeck.Models;
using static MacroDeck.Services.NativeApi;

namespace MacroDeck.Services;

/// <summary>
/// Captures mouse and keyboard events using low-level Windows hooks
/// (SetWindowsHookEx with WH_MOUSE_LL + WH_KEYBOARD_LL).
/// </summary>
public sealed class EventRecorder : IDisposable
{
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private LowLevelHookProc? _mouseProc;
    private LowLevelHookProc? _keyboardProc;
    private Stopwatch _stopwatch = new();

    public bool IsRecording { get; private set; }
    public List<RecordedEvent> Events { get; } = new();

    public event Action? EventCaptured;

    public void Start()
    {
        if (IsRecording) return;

        Events.Clear();
        _stopwatch.Restart();
        IsRecording = true;

        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;

        var moduleHandle = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (!IsRecording) return;

        IsRecording = false;
        _stopwatch.Stop();

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    public void Clear()
    {
        Events.Clear();
        EventCaptured?.Invoke();
    }

    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;
            var type = msg switch
            {
                WM_LBUTTONDOWN => RecordedEventType.LeftDown,
                WM_LBUTTONUP => RecordedEventType.LeftUp,
                WM_RBUTTONDOWN => RecordedEventType.RightDown,
                WM_RBUTTONUP => RecordedEventType.RightUp,
                WM_MBUTTONDOWN => RecordedEventType.MiddleDown,
                WM_MBUTTONUP => RecordedEventType.MiddleUp,
                WM_MOUSEMOVE => RecordedEventType.MouseMove,
                WM_MOUSEWHEEL => RecordedEventType.Scroll,
                _ => (RecordedEventType?)null,
            };

            if (type.HasValue)
            {
                var ev = new RecordedEvent
                {
                    Timestamp = _stopwatch.Elapsed.TotalSeconds,
                    Type = type.Value,
                    X = info.pt.x,
                    Y = info.pt.y,
                };

                if (type.Value == RecordedEventType.Scroll)
                {
                    ev.ScrollDeltaY = (short)((info.mouseData >> 16) & 0xFFFF);
                }

                Events.Add(ev);
                EventCaptured?.Invoke();
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;

            RecordedEventType? type = msg switch
            {
                WM_KEYDOWN or WM_SYSKEYDOWN => RecordedEventType.KeyDown,
                WM_KEYUP or WM_SYSKEYUP => RecordedEventType.KeyUp,
                _ => null,
            };

            if (type.HasValue)
            {
                var ev = new RecordedEvent
                {
                    Timestamp = _stopwatch.Elapsed.TotalSeconds,
                    Type = type.Value,
                    KeyCode = (int)info.vkCode,
                    Flags = info.flags,
                };

                // Capture current cursor position for reference.
                if (GetCursorPos(out var pt))
                {
                    ev.X = pt.x;
                    ev.Y = pt.y;
                }

                Events.Add(ev);
                EventCaptured?.Invoke();
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }
}
