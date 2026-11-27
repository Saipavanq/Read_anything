using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ReadAloud.Services;
using ReadAloud.ViewModels;
using ReadAloud.Views;
using WinFormsApplication = System.Windows.Forms.Application;

namespace ReadAloud;

public partial class App : System.Windows.Application
{
    private ConfigService? _config;
    private IThemeService? _themes;
    private ActivityLog? _log;
    private HistoryService? _history;
    private HomeViewModel? _home;
    private SelectionReadFlow? _selectionReadFlow;
    private PlaybackService? _playback;
    private MiniPlayerWindow? _miniWindow;
    private MiniPlayerService? _miniPlayer;
    private MiniPlayerViewModel? _miniViewModel;
    private MainViewModel? _main;
    private MainWindow? _window;
    private HotkeyService? _hotkeys;
    private Win32HotkeyRegistrar? _registrar;
    private StartupService? _startup;
    private AutoReadService? _autoRead;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        WinFormsApplication.EnableVisualStyles();
        WinFormsApplication.SetCompatibleTextRenderingDefault(false);

        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _config = new ConfigService(
            Path.Combine(roamingAppData, "Read Aloud", "settings.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "settings.json"));

        var config = _config.Load();
        _startup = new StartupService(new RegistryAccessor());
        _startup.SetStartWithWindows(config.StartWithWindows);
        _themes = new ThemeService(Resources, new WindowsSystemThemeProvider());
        _themes.Apply(config.ColorMode, config.Theme);

        _log = new ActivityLog();
        _history = new HistoryService(Path.Combine(roamingAppData, "Read Aloud", "history.json"));
        _history.SetEnabled(config.HistoryEnabled);
        _history.ApplyRetention(config.HistoryRetentionDays);
        var selection = new SelectionService(
            new ClipboardService(),
            new KeyboardSender(),
            new UiaSelectionReader(),
            new SystemClock(),
            () => _config!.Load().ClipboardRestore);
        var sapi = new SapiSpeechEngine();
        var piper = new PiperSpeechEngine(() => _config!.Load());
        var catalog = new EdgeVoiceCatalog();
        var edge = new EdgeSpeechEngine(() => _config!.Load(), new EdgeSynthesizer(), new NAudioPlayer(), piper, catalog);
        ISpeechEngine speech = new SelectableSpeechEngine(sapi, piper, edge, () => _config!.Load());
        _playback = new PlaybackService(speech, () => _config!.Load(), _config.Save, _history);
        _miniWindow = new MiniPlayerWindow();
        _miniPlayer = new MiniPlayerService(_miniWindow, () => _config!.Load(), _config.Save);
        _miniViewModel = new MiniPlayerViewModel(
            _playback,
            _miniPlayer,
            action => Dispatcher.Invoke(action));
        _miniWindow.DataContext = _miniViewModel;
        _miniPlayer.ExpandRequested += ShowMainWindow;
        _playback.StateChanged += state => Dispatcher.Invoke(() => _miniPlayer?.UpdateState(state));

        var coordinator = new ReadCoordinator(selection, _playback, _log, () => _config!.Load());

        _registrar = new Win32HotkeyRegistrar();
        _hotkeys = new HotkeyService(_registrar);
        var conflicts = _hotkeys.RegisterAll(config, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foreach (var conflict in conflicts)
        {
            _log.Add(conflict);
        }

        _registrar.HotkeyPressed += OnHotkeyPressed;

        _home = new HomeViewModel(
            coordinator,
            _log,
            _hotkeys,
            () => _config!.Load(),
            _config.Save,
            speech);
        _selectionReadFlow = new SelectionReadFlow(
            () => _home!.RequestReadSelectionAsync(),
            () => _miniPlayer?.ShowForSelectionRead());
        _main = new MainViewModel(_themes, _registrar)
        {
            Home = _home,
            MiniPlayer = _miniViewModel,
            VoiceLibrary = new VoiceLibraryViewModel(speech, _config),
            History = new HistoryViewModel(_history, coordinator)
        };

        var tray = new TrayService(
            ShowMainWindow,
            () => _ = ReadSelectionAsync(),
            StopSpeech,
            OpenSettings,
            Quit);
        _trayIcon = new TrayIcon(tray);

        _window = new MainWindow(
            _main,
            OpenSettings,
            () => _config!.Load().MinimizeToTray);
        MainWindow = _window;
        _window.Show();

        var foregroundPolicy = new OwnWindowForegroundPolicy(
            NativeMethods.GetForegroundWindow,
            () => Current!.Windows
                .Cast<Window>()
                .Where(window => window.IsVisible)
                .Select(window => new WindowInteropHelper(window).Handle)
                .Where(handle => handle != IntPtr.Zero)
                .ToArray());
        _autoRead = new AutoReadService(
            new MouseUpAutoReadTrigger(
                foregroundPolicy.IsOwnWindowForeground,
                hook: new Win32MouseHook(diagnostic: message => _log!.Add(message)),
                diagnostic: message => _log!.Add(message)),
            coordinator,
            () => _config!.Load(),
            action => Dispatcher.Invoke(action));
        _autoRead.ApplyConfig();

        _log.Add(conflicts.Count == 0 ? "Read Aloud is ready." : "Started with hotkey conflicts.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _autoRead?.Stop();
        _registrar?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void OnHotkeyPressed(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "read":
                _ = ReadSelectionAsync();
                break;
            case "stop":
                StopSpeech();
                break;
            case "voice":
                _log?.Add("Voice commands are available in a later release.");
                break;
        }
    }

    private async Task ReadSelectionAsync()
    {
        var flow = _selectionReadFlow;
        if (flow is null || _log is null)
        {
            return;
        }

        try
        {
            await await Dispatcher.InvokeAsync(() => flow.RunAsync());
        }
        catch (Exception ex)
        {
            _log.Add($"Read failed: {ex.Message}");
        }
    }

    private void StopSpeech()
    {
        Dispatcher.Invoke(() =>
        {
            if (_home is null || _log is null)
            {
                return;
            }

            _log.Add("Stop requested from tray or hotkey.");
            _home.Stop();
        });
    }

    private void OpenSettings()
    {
        if (_config is null || _hotkeys is null || _main is null || _themes is null)
        {
            return;
        }

        var viewModel = new SettingsViewModel(
            _config,
            _hotkeys,
            _themes,
            _main,
            _startup,
            action => Dispatcher.Invoke(action));
        var window = new SettingsWindow(viewModel) { Owner = _window };
        if (window.ShowDialog() == true)
        {
            ApplyToggledPreferences();
            _home?.Refresh();
            _log?.Add("Settings updated.");
        }
        else
        {
            ApplyToggledPreferences();
            _home?.Refresh();
        }
    }

    private void ApplyToggledPreferences()
    {
        if (_config is null)
        {
            return;
        }

        var config = _config.Load();
        _history?.SetEnabled(config.HistoryEnabled);
        _history?.ApplyRetention(config.HistoryRetentionDays);
        _autoRead?.ApplyConfig();

        if (_miniPlayer is null)
        {
            return;
        }

        _miniPlayer.SetAlwaysOnTop(config.MiniPlayerAlwaysOnTop);
        if (!config.MiniPlayerEnabled)
        {
            _miniPlayer.Hide();
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Show();
        _window.Activate();
    }

    private void Quit()
        => Dispatcher.Invoke(Shutdown);
}
