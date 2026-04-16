using System.Runtime.InteropServices;
using MacroDeck.Models;
using static MacroDeck.Services.NativeApi;

namespace MacroDeck.Services;

/// <summary>
/// Replays recorded events by posting synthetic mouse/keyboard input
/// via the Win32 SendInput API.
/// </summary>
public sealed class EventPlayer
{
    private volatile bool _cancelFlag;
    private volatile bool _playbackCancelled;

    public bool IsPlaying { get; private set; }

    public event Action? PlaybackStateChanged;

    public void Stop()
    {
        _cancelFlag = true;
        _playbackCancelled = true;
    }

    public async Task PlayAsync(
        List<RecordedEvent> events,
        double speed = 1.0,
        int repeatCount = 1,
        bool abortOnMove = false,
        CancellationToken ct = default)
    {
        if (IsPlaying || events.Count == 0) return;

        IsPlaying = true;
        _cancelFlag = false;
        _playbackCancelled = false;
        PlaybackStateChanged?.Invoke();

        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);
        var remaining = repeatCount; // -1 = infinite

        try
        {
            while (remaining != 0 && !_playbackCancelled)
            {
                var start = DateTime.UtcNow;
                POINT lastCursor = default;
                var hasLastCursor = false;

                foreach (var ev in events)
                {
                    if (_cancelFlag || ct.IsCancellationRequested) break;

                    // Absolute-time scheduling.
                    var target = start.AddSeconds(ev.Timestamp / Math.Max(0.1, speed));
                    var now = DateTime.UtcNow;
                    var delay = target - now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                    }

                    // Abort-on-move check: if cursor drifted >40px, user
                    // is taking control — bail.
                    if (abortOnMove && hasLastCursor)
                    {
                        if (GetCursorPos(out var cur))
                        {
                            var dx = cur.x - lastCursor.x;
                            var dy = cur.y - lastCursor.y;
                            if (Math.Sqrt(dx * dx + dy * dy) > 40)
                            {
                                break;
                            }
                        }
                    }

                    PostEvent(ev, screenW, screenH);

                    if (ev.Type is RecordedEventType.MouseMove or
                        RecordedEventType.LeftDown or RecordedEventType.LeftUp or
                        RecordedEventType.RightDown or RecordedEventType.RightUp)
                    {
                        lastCursor = new POINT { x = (int)ev.X, y = (int)ev.Y };
                        hasLastCursor = true;
                    }
                }

                if (remaining > 0) remaining--;
            }
        }
        catch (TaskCanceledException) { }
        finally
        {
            IsPlaying = false;
            PlaybackStateChanged?.Invoke();
        }
    }

    private static void PostEvent(RecordedEvent ev, int screenW, int screenH)
    {
        switch (ev.Type)
        {
            case RecordedEventType.MouseMove:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE);
                break;
            case RecordedEventType.LeftDown:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN);
                break;
            case RecordedEventType.LeftUp:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP);
                break;
            case RecordedEventType.RightDown:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTDOWN);
                break;
            case RecordedEventType.RightUp:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_RIGHTUP);
                break;
            case RecordedEventType.MiddleDown:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MIDDLEDOWN);
                break;
            case RecordedEventType.MiddleUp:
                SendMouseInput((int)ev.X, (int)ev.Y, screenW, screenH, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MIDDLEUP);
                break;
            case RecordedEventType.KeyDown:
                if (ev.KeyCode.HasValue)
                    SendKeyInput((ushort)ev.KeyCode.Value, KEYEVENTF_KEYDOWN);
                break;
            case RecordedEventType.KeyUp:
                if (ev.KeyCode.HasValue)
                    SendKeyInput((ushort)ev.KeyCode.Value, KEYEVENTF_KEYUP);
                break;
            case RecordedEventType.Scroll:
                SendScrollInput(ev.ScrollDeltaY ?? 0);
                break;
        }
    }

    private static void SendMouseInput(int x, int y, int screenW, int screenH, uint flags)
    {
        // Convert pixel coords to absolute coords (0..65535).
        var absX = (int)(x * 65535.0 / screenW);
        var absY = (int)(y * 65535.0 / screenH);

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = flags,
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyInput(ushort vk, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = flags,
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendScrollInput(int delta)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    mouseData = delta,
                    dwFlags = MOUSEEVENTF_WHEEL,
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
