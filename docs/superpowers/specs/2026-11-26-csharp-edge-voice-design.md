# C# Microsoft Edge online voice design

Date: 2026-11-26
Status: proposed

## Intent

The installed C# WPF ReadAloud app should offer the Microsoft Edge neural voices
(Aria, Jenny, Guy and the rest of the Edge catalog) as an internet-backed speech
engine. The user wants to reach those voices from inside the C# app, with no
return to the old Python application and no Python runtime involved in this
feature. Piper stays the default engine; Edge is an explicit opt-in choice.

## Goals

- Add a third `ISpeechEngine`, `EdgeSpeechEngine`, written entirely in C#.
- Let the user select Edge in **Settings → Voice and playback** and set an
  ordered list of Edge voices by short name. The engine tries them in that order
  and uses the first one that succeeds.
- If every Edge voice fails (offline, timeout, service rejection), silently fall
  back to Piper for that read. No error is shown.
- Show the Edge voice catalog in the existing **Voice Library** with the existing
  search and language filter when Edge is the active engine.
- Keep the existing window layout, theme and all existing rows unchanged; only
  add rows inside the existing "Voice and playback" group.
- Keep Piper active by default and SAPI available as an offline fallback.
- Make network failure safe: no crash, no hung UI, no unhandled exception at the
  UI boundary.

## Non-goals

- Reintroducing or depending on the Python ReadAloud application.
- Using a Python helper process for Edge synthesis.
- Changing Piper, SAPI, capture, hotkey, history or theme behaviour.
- Bundling or shipping Microsoft audio or voice models.

## Anti-goals

- Silently sending text online. Edge must be chosen explicitly in settings, and
  the option must say that text is sent online.
- Blocking startup on a network call. The Voice Library is constructed during
  `App.OnStartup`, so voice catalog access must be cached/backgrounded.
- Replacing Piper as the default engine.
- Showing an error dialog or status message when the network is unavailable; the
  fallback to Piper is silent.

## Constraints

- Target project: `src-cs/ReadAloud/ReadAloud.csproj`, .NET 8 WPF,
  `net8.0-windows`, single-file `win-x64` publish, xUnit tests in
  `src-cs/ReadAloud.Tests`.
- `dotnet` on PATH has no SDK; use
  `C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe` for all commands.
- Existing config is `%APPDATA%\Read Aloud\settings.json`; new keys must be
  backward compatible and added without disturbing existing keys.
- Existing `ISpeechEngine` contract: `GetVoices()`, `Speak(text, options)`,
  `Stop()`, `Pause()`, `Resume()`, `IsSpeaking`, `SpeakCompleted`,
  `SpeakProgress`.
- No Python dependency for this feature. The old Python app's `edge_tts`
  installation is not part of the design.

## Approaches

### A. Pure C# Edge engine with a .NET Edge TTS library (chosen)

Use `EdgeTtsSharp` (MIT, .NET Standard 2.0/2.1, compatible with .NET 8) for the
Edge speech WebSocket synthesis, and `NAudio` for MP3 playback. Fetch the voice
catalog in C# with `HttpClient` from Microsoft's public Edge voices endpoint,
cache it locally, and expose it through `EdgeSpeechEngine.GetVoices()`.

Trade-off: adds two managed NuGet dependencies and relies on Microsoft's public
(unofficial) Edge endpoint. It satisfies "all in C#" and keeps the existing
settings/voice-library surface.

### B. Python `edge_tts` helper invoked from C#

Rejected. The `.venv` already has `edge_tts`, and it would be a small change, but
it requires Python at runtime and the user explicitly asked for everything in
C# and no use of the Python app.

### C. `EdgeTTS.DotNet` library

Rejected. It has a clean API (`Voices.ListVoicesAsync()`, `Communicate`) but its
NuGet package targets .NET 9/10 only, while the app targets .NET 8.

### D. Windows-installed online natural voices through SAPI

Rejected. It needs no network code but only exposes locally installed voices,
not the Edge catalog the user asked for.

## Design

### Configuration

Add to `AppConfig` and `ConfigService`:

- `EdgeVoicePriority` (`List<string>`, default
  `["en-US-AriaNeural", "en-US-JennyNeural", "en-US-GuyNeural"]`) persisted as
  `edge_voice_priority`. This is the user-editable hierarchy.
- `EdgeVoice` (string) remains as a legacy key. When `edge_voice_priority` is
  absent but `edge_voice` exists, the legacy value seeds the first priority
  entry. On save, the first priority entry is also written to `edge_voice` so
  older readers remain compatible.

No other new keys. Existing 38+ keys remain untouched. Reads of a missing key
fall back to the default.

### Engine

`src-cs/ReadAloud/Services/EdgeSpeechEngine.cs`:

- Implements `ISpeechEngine` using `Func<AppConfig>` like `PiperSpeechEngine`.
- Constructor takes a fallback `ISpeechEngine` (Piper). It subscribes to the
  fallback's events once and forwards them as its own, so a fallback read looks
  identical to callers.
- `Speak`: a non-empty `options.VoiceId` (from Voice Library preview) is tried
  first; then each `config.EdgeVoicePriority` entry in order. For each candidate
  it calls `EdgeTtsSharp` to save MP3 to a temp file and plays it with `NAudio`.
  The first successful voice wins; later candidates are not read.
- If no candidate succeeds, it calls `fallback.Speak(text, options)` silently.
  This is how offline behavior becomes Piper without any UI error path.
- `Stop()` cancels playback and the in-flight request; it also stops the
  fallback through the forwarded engine path.
- Applies `config.PlaybackSpeed` to Edge rate and `config.Volume` to playback
  volume where the library supports it.
- All failures are caught inside the engine; completion is still raised so
  `PlaybackService` cannot hang.
- `GetVoices`: returns a cached catalog immediately. The cache lives at
  `%LOCALAPPDATA%\Read Aloud\edge_voices.json`. A background `HttpClient` fetch
  refreshes it using Microsoft's public Edge voices endpoint with the standard
  public trusted-client token; a small bundled short list of well-known voices
  is the fallback before the first successful fetch. Voice cards map
  `ShortName` → `Name` and voice id, `Locale` → `Language`, `Gender` → `Gender`,
  and the first configured priority entry → `IsDefault`.

### Routing and composition

- `SelectableSpeechEngine` switches on exact values: `sapi` → SAPI, `edge` →
  Edge, anything else (including default `piper`) → Piper. Events and `Stop`
  forward only from the active engine, as today.
- `App.xaml.cs` constructs one `EdgeSpeechEngine` and passes it into the same
  `SelectableSpeechEngine` instance used by PlaybackService, HomeViewModel and
  VoiceLibraryViewModel.

### UI

- `SettingsWindow.xaml`: inside the existing "Voice and playback" GroupBox, add
  `edge` to the Speech engine ComboBox with label
  `Online voice - Microsoft Edge (text is sent online)`, and add an
  "Edge voices (priority order)" TextBox bound to a comma-separated
  `EdgeVoicePriorityText`. Existing rows keep their order and styling.
- `SettingsViewModel`: add `EdgeVoicePriorityText`, initialize it from config,
  persist it in `ApplyTo` (splitting on commas, trimming, dropping blanks), and
  expose the third engine in the engine list.
- `HomeViewModel.SelectedVoice` writes `OfflineVoice` for Piper/SAPI and moves
  the chosen voice to the front of `EdgeVoicePriority` when the active engine is
  Edge.
- `VoiceLibraryView.xaml`: change only the subtitle text based on active engine —
  existing offline text for Piper/SAPI, "Microsoft Edge neural voices (requires
  internet)." for Edge. No layout, card template or control changes.
- Voice Library preview continues to be audition-only: it passes the tapped
  voice id to `Speak` but does not persist a default.

### Tests

- Unit tests with fakes for: config round-trip of `edge_voice_priority` (including
  legacy `edge_voice` seeding), routing of `edge` to the Edge engine, default
  engine still Piper, voice-list JSON parsing, cache fallback, silent fallback
  from Edge to Piper when all voices fail, and per-engine voice persistence.
- Real network synthesis and catalog fetch are manual smoke checks, not unit
  tests, to avoid flaky CI.

## Parts left to the builder

- Exact NuGet package versions and API calls for `EdgeTtsSharp` and `NAudio`
  that compile under .NET 8.
- Exact Microsoft voices endpoint URL and token as used by current Edge clients;
  verify it responds before relying on it.
- Cache expiry policy (a day is a reasonable default) and bundled fallback list
  contents.
- Whether Edge rate/volume are passed to the library or applied in NAudio.
