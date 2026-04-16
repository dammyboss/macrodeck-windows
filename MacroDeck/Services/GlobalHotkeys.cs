using System.Windows.Interop;
using static MacroDeck.Services.NativeApi;

namespace MacroDeck.Services;

/// <summary>
/// Registers system-wide hotkeys via RegisterHotKey. The hotkeys fire
/// even when MacroDeck isn't the foreground window — ideal for
/// start/stop record and play controls.
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>
    /// Registers a hotkey. Returns the id that can be used to unregister.
    /// </summary>
    /// <param name="modifiers">MOD_CONTROL | MOD_ALT | etc.</param>
    /// <param name="vk">Virtual key code (e.g. 0x52 for 'R').</param>
    /// <param name="handler">Callback when hotkey fires.</param>
    public int Register(uint modifiers, uint vk, Action handler)
    {
        var id = _nextId++;
        if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
        {
            _handlers[id] = handler;
        }
        return id;
    }

    public void Unregister(int id)
    {
        UnregisterHotKey(_hwnd, id);
        _handlers.Remove(id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }
        _handlers.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var handler))
            {
                handler.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
