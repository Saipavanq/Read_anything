using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private readonly ReadCoordinator _coordinator;
    private readonly ActivityLog _log;
    private readonly HotkeyService _hotkeys;
    private readonly Func<AppConfig> _configProvider;
    private readonly Action<AppConfig>? _saveConfig;
    private readonly ISpeechEngine? _speech;

    private IReadOnlyList<string> _voices = Array.Empty<string>();
    private string _selectedVoice = string.Empty;
    private string _textInput = string.Empty;
    private string _status = "Ready";
    private bool _isListening;
    private CaptureMethod _captureMethod = CaptureMethod.ClipboardFirst;
    private bool _showActivity = true;
    private IReadOnlyList<string> _hotkeySummary = Array.Empty<string>();

    public HomeViewModel(
        ReadCoordinator coordinator,
        ActivityLog log,
        HotkeyService hotkeys,
        Func<AppConfig> configProvider,
        Action<AppConfig>? saveConfig = null,
        ISpeechEngine? speech = null)
    {
        _coordinator = coordinator;
        _log = log;
        _hotkeys = hotkeys;
        _configProvider = configProvider;
        _saveConfig = saveConfig;
        _speech = speech;

        // Reads are newest-wins: a newer request may supersede an in-flight read.
        ListenCommand = new RelayCommand(_ => _ = RequestListenAsync());
        ClearCommand = new RelayCommand(_ => TextInput = string.Empty);
        CaptureMethods = Enum.GetValues<CaptureMethod>();

        Refresh();
    }

    public string Greeting { get; } = "Ready to listen?";

    public string PrimaryButtonText { get; } = "Listen";

    public string PrimaryButtonAccessibleName { get; } = "Read selection";

    public string TextInput
    {
        get => _textInput;
        set
        {
            if (SetProperty(ref _textInput, value))
            {
                RaisePropertyChanged(nameof(CharacterCount));
            }
        }
    }

    public int CharacterCount => _textInput.Length;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (SetProperty(ref _isListening, value))
            {
                ((RelayCommand)ListenCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand ListenCommand { get; }

    public ICommand ClearCommand { get; }

    public IReadOnlyList<CaptureMethod> CaptureMethods { get; }

    public IReadOnlyList<string> Voices
    {
        get => _voices;
        private set => SetProperty(ref _voices, value);
    }

    public string SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (!SetProperty(ref _selectedVoice, value))
            {
                return;
            }

            var config = _configProvider();
            config.OfflineVoice = value;
            _saveConfig?.Invoke(config);
            _log.Add($"Voice changed to {value}.");
        }
    }

    public CaptureMethod SelectedCaptureMethod
    {
        get => _captureMethod;
        set
        {
            if (!SetProperty(ref _captureMethod, value))
            {
                return;
            }

            var config = _configProvider();
            config.CaptureMethod = value;
            _saveConfig?.Invoke(config);
            _log.Add($"Capture mode changed to {value}.");
        }
    }

    public IReadOnlyList<string> HotkeySummary
    {
        get => _hotkeySummary;
        private set => SetProperty(ref _hotkeySummary, value);
    }

    public ObservableCollection<string> Activity => _log.Entries;

    public bool ShowActivity
    {
        get => _showActivity;
        private set => SetProperty(ref _showActivity, value);
    }

    public void Refresh()
    {
        var config = _configProvider();
        Voices = _speech is null
            ? Array.Empty<string>()
            : _speech.GetVoices().Select(voice => voice.Name).ToArray();
        SetProperty(ref _selectedVoice, config.OfflineVoice, nameof(SelectedVoice));
        _captureMethod = config.CaptureMethod;
        RaisePropertyChanged(nameof(SelectedCaptureMethod));
        ShowActivity = config.ShowActivity;
        HotkeySummary = new[]
        {
            $"Read: {config.HotkeyRead}",
            $"Voice: {config.HotkeyVoice}",
            $"Stop: {config.HotkeyStop}"
        };
    }

    public Task<bool> RequestListenAsync()
        => RunReadAsync(string.IsNullOrWhiteSpace(_textInput) ? null : _textInput);

    public Task<bool> RequestReadSelectionAsync() => RunReadAsync(null);

    public void Stop()
    {
        _coordinator.Stop();
        IsListening = false;
        Status = "Ready";
    }

    private async Task<bool> RunReadAsync(string? manualText)
    {
        IsListening = true;
        Status = "Reading…";
        var started = false;

        try
        {
            started = await _coordinator.RequestReadAsync(manualText);
        }
        catch (Exception ex)
        {
            _log.Add($"Read failed: {ex.Message}");
        }
        finally
        {
            IsListening = false;
            Status = "Ready";
        }

        return started;
    }
}
