using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MacroDeck.Models;
using MacroDeck.Services;
using Microsoft.Win32;

namespace MacroDeck.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly EventRecorder _recorder = new();
    private readonly EventPlayer _player = new();
    private readonly GlobalHotkeys _hotkeys = new();
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _playCts;

    // ─── Observable state ───

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set { SetField(ref _isRecording, value); OnPropertyChanged(nameof(StatusText)); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { SetField(ref _isPlaying, value); OnPropertyChanged(nameof(StatusText)); }
    }

    private string _sessionName = "Untitled Session";
    public string SessionName
    {
        get => _sessionName;
        set => SetField(ref _sessionName, value);
    }

    private string _elapsedDisplay = "00:00.000";
    public string ElapsedDisplay
    {
        get => _elapsedDisplay;
        set => SetField(ref _elapsedDisplay, value);
    }

    private int _eventCount;
    public int EventCount
    {
        get => _eventCount;
        set { SetField(ref _eventCount, value); OnPropertyChanged(nameof(DiskKB)); }
    }

    public int DiskKB => Math.Max(1, EventCount * 80 / 1024);

    public string StatusText =>
        IsRecording ? "RECORDING" :
        IsPlaying ? "PLAYING" :
        "IDLE";

    public ObservableCollection<RecordedEvent> Events { get; } = new();

    // ─── Settings ───

    private int _repeatCount = 1;
    public int RepeatCount
    {
        get => _repeatCount;
        set => SetField(ref _repeatCount, value);
    }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetField(ref _playbackSpeed, value);
    }

    private bool _abortOnMove;
    public bool AbortOnMove
    {
        get => _abortOnMove;
        set => SetField(ref _abortOnMove, value);
    }

    // ─── Selected event (inspector) ───

    private RecordedEvent? _selectedEvent;
    public RecordedEvent? SelectedEvent
    {
        get => _selectedEvent;
        set => SetField(ref _selectedEvent, value);
    }

    // ─── Settings panel state ───

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            SetField(ref _isSettingsOpen, value);
            OnPropertyChanged(nameof(MainPageVisibility));
            OnPropertyChanged(nameof(SettingsPageVisibility));
        }
    }

    public Visibility MainPageVisibility =>
        IsSettingsOpen ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SettingsPageVisibility =>
        IsSettingsOpen ? Visibility.Visible : Visibility.Collapsed;

    // ─── Commands ───

    public RelayCommand ToggleRecordCommand { get; }
    public RelayCommand TogglePlayCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CloseSettingsCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    // ─── Constructor ───

    public MainViewModel()
    {
        ToggleRecordCommand = new RelayCommand(ToggleRecord);
        TogglePlayCommand = new RelayCommand(TogglePlay);
        StopAllCommand = new RelayCommand(StopAll);
        SaveCommand = new RelayCommand(SaveSession);
        OpenCommand = new RelayCommand(OpenSession);
        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        SaveSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        ClearCommand = new RelayCommand(ClearSession);

        _recorder.EventCaptured += OnEventCaptured;
        _player.PlaybackStateChanged += OnPlaybackStateChanged;

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
    }

    // ─── Hotkey setup (called after window handle is available) ───

    public void AttachHotkeys(IntPtr hwnd)
    {
        _hotkeys.Attach(hwnd);

        // Ctrl+Alt+R — toggle record
        _hotkeys.Register(NativeApi.MOD_CONTROL | NativeApi.MOD_ALT, 0x52, () =>
            Application.Current.Dispatcher.Invoke(ToggleRecord));

        // Ctrl+Alt+P — toggle play
        _hotkeys.Register(NativeApi.MOD_CONTROL | NativeApi.MOD_ALT, 0x50, () =>
            Application.Current.Dispatcher.Invoke(TogglePlay));

        // Ctrl+Alt+S — stop recording
        _hotkeys.Register(NativeApi.MOD_CONTROL | NativeApi.MOD_ALT, 0x53, () =>
            Application.Current.Dispatcher.Invoke(() => { if (IsRecording) ToggleRecord(); }));

        // Ctrl+Alt+. — stop playback
        _hotkeys.Register(NativeApi.MOD_CONTROL | NativeApi.MOD_ALT, 0xBE, () =>
            Application.Current.Dispatcher.Invoke(StopAll));
    }

    // ─── Record ───

    private void ToggleRecord()
    {
        if (IsRecording)
        {
            _recorder.Stop();
            _elapsedTimer.Stop();
            IsRecording = false;
        }
        else
        {
            _recorder.Start();
            _elapsedTimer.Start();
            IsRecording = true;
            Events.Clear();
            EventCount = 0;
        }
    }

    // ─── Play ───

    private async void TogglePlay()
    {
        if (IsPlaying)
        {
            StopAll();
            return;
        }

        if (_recorder.Events.Count == 0) return;

        // Stop recording before playing to prevent the mouse hook
        // from capturing our own synthetic SendInput events.
        if (IsRecording) ToggleRecord();

        _playCts = new CancellationTokenSource();
        IsPlaying = true;

        await _player.PlayAsync(
            _recorder.Events,
            PlaybackSpeed,
            RepeatCount,
            AbortOnMove,
            _playCts.Token
        );

        IsPlaying = false;
    }

    private void StopAll()
    {
        _recorder.Stop();
        _player.Stop();
        _playCts?.Cancel();
        _elapsedTimer.Stop();
        IsRecording = false;
        IsPlaying = false;
    }

    // ─── File ops ───

    private void SaveSession()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "MacroDeck Session (*.json)|*.json",
            DefaultExt = ".json",
            FileName = SessionName,
        };

        if (dlg.ShowDialog() == true)
        {
            var session = new MacroSession
            {
                Name = SessionName,
                Events = _recorder.Events,
                RepeatCount = RepeatCount,
                PlaybackSpeed = PlaybackSpeed,
            };
            session.SaveToFile(dlg.FileName);
            SessionName = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void OpenSession()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "MacroDeck Session (*.json)|*.json|All files (*.*)|*.*",
        };

        if (dlg.ShowDialog() == true)
        {
            var session = MacroSession.LoadFromFile(dlg.FileName);
            SessionName = session.Name;
            RepeatCount = session.RepeatCount;
            PlaybackSpeed = session.PlaybackSpeed;
            _recorder.Clear();
            foreach (var ev in session.Events)
                _recorder.Events.Add(ev);
            SyncEvents();
        }
    }

    private void ClearSession()
    {
        _recorder.Clear();
        Events.Clear();
        EventCount = 0;
        SessionName = "Untitled Session";
    }


    // ─── Internal ───

    private void OnEventCaptured()
    {
        Application.Current.Dispatcher.Invoke(SyncEvents);
    }

    private void OnPlaybackStateChanged()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying = _player.IsPlaying;
        });
    }

    private void SyncEvents()
    {
        // Sync the recorder's list to the observable collection.
        // (Full sync for simplicity — optimize with incremental adds later.)
        Events.Clear();
        foreach (var ev in _recorder.Events)
            Events.Add(ev);
        EventCount = Events.Count;
    }

    private void UpdateElapsed()
    {
        if (!IsRecording) return;
        var total = _recorder.ElapsedSeconds;
        var mm = (int)(total / 60);
        var ss = (int)(total % 60);
        var ms = (int)((total * 1000) % 1000);
        ElapsedDisplay = $"{mm:D2}:{ss:D2}.{ms:D3}";
    }

    public void Dispose()
    {
        _recorder.Dispose();
        _hotkeys.Dispose();
        _playCts?.Cancel();
    }
}
