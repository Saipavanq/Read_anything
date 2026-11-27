using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class VoiceLibraryViewModel : ObservableObject
{
    private const string PreviewText = "This is a preview of the selected voice.";

    private readonly ISpeechEngine _speech;
    private readonly ConfigService _config;
    private IReadOnlyList<VoiceCard> _voices = Array.Empty<VoiceCard>();
    private IReadOnlyList<VoiceCard> _filteredVoices = Array.Empty<VoiceCard>();
    private IReadOnlyList<string> _languageOptions = Array.Empty<string>();
    private VoiceCard? _selectedVoice;
    private string _searchText = string.Empty;
    private string? _languageFilter;
    private bool _isOnlineEngine;

    public VoiceLibraryViewModel(ISpeechEngine speech, ConfigService config)
    {
        _speech = speech;
        _config = config;
        PreviewCommand = new RelayCommand(card => Preview(card as VoiceCard ?? SelectedVoice!));
        RefreshCommand = new RelayCommand(_ => Refresh());
        Refresh();
    }

    public IReadOnlyList<VoiceCard> Voices
    {
        get => _voices;
        private set => SetProperty(ref _voices, value);
    }

    public IReadOnlyList<VoiceCard> FilteredVoices
    {
        get => _filteredVoices;
        private set => SetProperty(ref _filteredVoices, value);
    }

    public IReadOnlyList<string> LanguageOptions
    {
        get => _languageOptions;
        private set => SetProperty(ref _languageOptions, value);
    }

    public bool IsOnlineEngine
    {
        get => _isOnlineEngine;
        private set => SetProperty(ref _isOnlineEngine, value);
    }

    public VoiceCard? SelectedVoice
    {
        get => _selectedVoice;
        private set => SetProperty(ref _selectedVoice, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string? LanguageFilter
    {
        get => _languageFilter;
        set
        {
            if (SetProperty(ref _languageFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand PreviewCommand { get; }

    public ICommand RefreshCommand { get; }

    public void Refresh()
    {
        var config = _config.Load();
        IsOnlineEngine = string.Equals(config.TtsEngine, "edge", StringComparison.OrdinalIgnoreCase);

        var installed = _speech.GetVoices();
        Voices = installed.Select(Map).ToList();
        LanguageOptions = Voices
            .Select(card => card.Language)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var configured = IsOnlineEngine
            ? config.EdgeVoicePriority?.FirstOrDefault() ?? string.Empty
            : config.OfflineVoice;
        var resolved = VoiceIdentity.Resolve(configured, installed);
        SelectedVoice = resolved is null
            ? null
            : Voices.FirstOrDefault(card =>
                string.Equals(card.Description, resolved.Description, StringComparison.OrdinalIgnoreCase)
                || string.Equals(card.Name, resolved.Name, StringComparison.OrdinalIgnoreCase));

        ApplyFilter();
    }

    public void Preview(VoiceCard card)
    {
        if (card is null)
        {
            return;
        }

        // Preview is audition-only: it must not silently change the configured
        // voice. The explicit voice selector owns that persisted decision.
        var config = _config.Load();
        SelectedVoice = card;

        _speech.Speak(
            PreviewText,
            new VoiceOptions(
                card.Description,
                PlaybackService.SpeedToRate(config.PlaybackSpeed),
                config.Volume,
                card.Name));
    }

    private VoiceCard Map(VoiceInfo voice)
        => new(
            voice.Name,
            voice.Description,
            voice.Language,
            string.IsNullOrWhiteSpace(voice.Gender) ? "Not specified" : voice.Gender,
            IsOnlineEngine && !EdgeVoiceCatalog.IsCuratedVoice(voice.Name) ? "Piper (offline)" : "System",
            voice.IsDefault);

    private void ApplyFilter()
    {
        var search = SearchText?.Trim() ?? string.Empty;
        FilteredVoices = Voices
            .Where(card => string.IsNullOrEmpty(search)
                || card.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(card => string.IsNullOrWhiteSpace(LanguageFilter)
                || string.Equals(card.Language, LanguageFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
