# MacroDeck for Windows

A precision mouse and keyboard macro recorder for Windows. Native C# WPF app using Win32 APIs for capture and playback.

## Features

- **Record** mouse moves, clicks, scroll, and keystrokes via `SetWindowsHookEx` (low-level hooks)
- **Playback** via `SendInput` with configurable speed, delay, and repeat count
- **Global hotkeys** via `RegisterHotKey` — works even when MacroDeck isn't focused
- **Abort-on-move** — playback stops if you take control of the mouse
- **Save / Load** sessions as JSON (same format as the macOS version)
- **Dark theme** — Soft Dark by default, matching the macOS version's aesthetics
- **Compact 500×640 window** — same layout as the Mac version

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build & Run

```powershell
git clone https://github.com/dammyboss/macrodeck-windows.git
cd macrodeck-windows
dotnet build
dotnet run --project MacroDeck
```

For a release build:

```powershell
dotnet publish MacroDeck -c Release -o publish
```

The output at `publish/MacroDeck.exe` is a self-contained executable.

## Global Hotkeys

| Hotkey | Action |
|---|---|
| Ctrl+Alt+R | Toggle recording |
| Ctrl+Alt+P | Toggle playback |
| Ctrl+Alt+S | Stop recording |
| Ctrl+Alt+. | Stop playback |

## Architecture

```
MacroDeck/
├── Models/
│   ├── RecordedEvent.cs       — event data model (JSON-serializable)
│   └── MacroSession.cs        — session with metadata + save/load
├── Services/
│   ├── NativeApi.cs           — all Win32 P/Invoke declarations
│   ├── EventRecorder.cs       — SetWindowsHookEx mouse + keyboard capture
│   ├── EventPlayer.cs         — SendInput playback with timing
│   └── GlobalHotkeys.cs       — RegisterHotKey wrapper
├── ViewModels/
│   ├── ViewModelBase.cs       — INotifyPropertyChanged + RelayCommand
│   └── MainViewModel.cs       — core app logic (MVVM)
├── Themes/
│   └── SoftDark.xaml          — dark theme resource dictionary
├── MainWindow.xaml            — compact dark-themed UI
└── App.xaml                   — application entry point
```

## Session format

Sessions are saved as `.json` files with the same schema as the macOS version. Files are cross-platform compatible — a session recorded on Mac can be played back on Windows and vice versa (coordinates may differ due to screen resolution).

## Contact

Damilola Onadeinde — damilola.onadeinde@gmail.com
