# C# Microsoft Edge Online Voice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a pure-C# Microsoft Edge online speech engine with a user-editable ordered voice priority list and silent Piper fallback, while keeping Piper the default engine and the existing UI layout.

**Architecture:** A new `EdgeSpeechEngine` sits beside SAPI and Piper behind `SelectableSpeechEngine`. It uses `edge-tts-net` 2.0.0 for online synthesis, NAudio for MP3 playback, and a cached Microsoft voice catalog for the Voice Library. Configuration and Settings gain an ordered `edge_voice_priority` list; when every online attempt fails, the engine silently delegates to Piper.

**Tech Stack:** C# 12 / .NET 8 WPF (`net8.0-windows`), xUnit, `edge-tts-net` 2.0.0, `NAudio` 2.2.1, `System.Net.Http`, single-file `win-x64` publish.

**Implementation update (2026-11-26):** The originally planned `EdgeTtsSharp`
2.0.7 client now receives HTTP 403 from Microsoft and hangs on a 0-byte MP3, so
it was replaced by `edge-tts-net` 2.0.0. `EdgeSynthesizer` also applies an
8-second per-voice timeout and rejects empty files; `EdgeSpeechEngine` rejects
empty files as a second guard and falls back to Piper.

**Spec:** `docs/superpowers/specs/2026-11-26-csharp-edge-voice-design.md`

## Global Constraints

- Use `C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe` for every dotnet command; `dotnet` on PATH has no SDK.
- Test command: `& 'C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe' test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj`.
- Keep the existing WPF UI layout, theme, and all existing rows unchanged; add only inside the existing `Voice and playback` GroupBox, plus the Voice Library subtitle text change.
- Piper remains the default `TtsEngine` value (`"piper"`); SAPI remains selectable.
- No Python runtime, Python helper, or Python ReadAloud app is used by this feature.
- Existing config keys must remain intact; new keys are backward compatible.
- Network failures are silent. No error dialog, status text, or Activity log entry for offline/Edge failures.
- Pause/Resume remain no-ops, as in Piper. Progress remains start/end only, as in Piper.
- Commit after each task; do not push until the final publish step.

## Review Focus

- **No internet while Edge is selected:** `Speak` must silently delegate to Piper and still raise completion. Test: `EdgeEngine_FallsBackToPiperWhenAllVoicesFail`.
- **First voice in priority fails, second succeeds:** later voices must be tried in order, and only the first success is read. Test: `EdgeEngine_UsesVoicesInPriorityOrder`.
- **Voice Library preview voice:** a tapped card's voice must be tried before the stored priority list without changing stored config. Test: `EdgeEngine_PreviewVoiceTriedFirst`.
- **Legacy config from Python/old C#:** a stored `edge_voice` must seed the priority list when `edge_voice_priority` is missing. Test: `EdgePriority_LegacyVoiceSeedsList`.
- **Startup with no cached catalog:** `GetVoices()` must return the bundled fallback immediately and never block startup on HTTP. Test: `EdgeCatalog_NoCacheReturnsFallback`.

---

### Task 1: Config and settings inputs for Edge voice priority

**Files:**
- Modify: `src-cs/ReadAloud/Models/AppConfig.cs`
- Modify: `src-cs/ReadAloud/Services/ConfigService.cs`
- Modify: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Modify: `src-cs/ReadAloud/Views/SettingsWindow.xaml`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: existing `ConfigService.Load()`/`Save(AppConfig)` and `SettingsViewModel.ApplyTo(AppConfig)`.
- Produces: `AppConfig.EdgeVoicePriority` (`List<string>`, default `["en-US-AriaNeural", "en-US-JennyNeural", "en-US-GuyNeural"]`), JSON key `edge_voice_priority`; `SettingsViewModel.EdgeVoicePriorityText` (comma-separated string); engine list contains `sapi`, `piper`, `edge`.

- [ ] **Step 1: Add failing config tests**

Add `EdgePriority_DefaultAndRoundTrip`: fresh `AppConfig` has the three default voices; after `ConfigService.Save`, a new `ConfigService.Load` returns the same ordered list. Add `EdgePriority_LegacyVoiceSeedsList`: a settings JSON with only `edge_voice = "en-US-AriaNeural"` and no `edge_voice_priority` loads a priority list whose first element is that voice.

- [ ] **Step 2: Run filtered tests and confirm RED**

Run: `& 'C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe' test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj --filter FullyQualifiedName~ConfigTests`
Expected: FAIL with missing `EdgeVoicePriority`.

- [ ] **Step 3: Implement config keys**

In `AppConfig`, add `public List<string> EdgeVoicePriority { get; set; } = new() { "en-US-AriaNeural", "en-US-JennyNeural", "en-US-GuyNeural" };`. In `ConfigService`, read/write `edge_voice_priority` as a JSON array; when absent and `edge_voice` exists, seed the list with that value; when writing, also write the first priority entry to `edge_voice`.

- [ ] **Step 4: Add failing settings tests**

Add `EdgePriority_LoadIntoViewModel` and `EdgePriority_SaveBackToConfig` asserting `EdgeVoicePriorityText` loads as `"en-US-AriaNeural, en-US-JennyNeural"` and saves back to the same ordered list. Keep constructor signatures unchanged.

- [ ] **Step 5: Implement settings binding and UI**

In `SettingsViewModel`, add `EdgeVoicePriorityText` with change notification, initialize from config in the existing load path, write a trimmed/comma-split list in `ApplyTo`, and add `edge` to the engine list. In `SettingsWindow.xaml`, inside the existing `Voice and playback` GroupBox only, add an `edge` ComboBoxItem labelled `Online voice - Microsoft Edge (text is sent online)` and a `Edge voices (priority order)` TextBox bound to `EdgeVoicePriorityText`, matching adjacent row styling.

- [ ] **Step 6: Run filtered and full tests to GREEN**

Run filtered `ConfigTests` and `SettingsTests`, then the full suite. Expected: all pass.

- [ ] **Step 7: Commit**

`git commit -m "feat(config): add ordered Edge voice priority settings"`

---

### Task 2: Edge voice catalog and engine with silent Piper fallback

**Files:**
- Modify: `src-cs/ReadAloud/ReadAloud.csproj`
- Create: `src-cs/ReadAloud/Services/EdgeVoiceCatalog.cs`
- Create: `src-cs/ReadAloud/Services/EdgeSpeechEngine.cs`
- Create: `src-cs/ReadAloud/Services/EdgeSynthesizer.cs`
- Create: `src-cs/ReadAloud/Services/NAudioPlayer.cs`
- Test: `src-cs/ReadAloud.Tests/EdgeEngineTests.cs`

**Interfaces:**
- Consumes: `AppConfig.EdgeVoicePriority`, `VoiceInfo`, `ISpeechEngine`, `VoiceOptions`.
- Produces: `EdgeVoiceCatalog.Parse(string json)` → `IReadOnlyList<VoiceInfo>`; `EdgeVoiceCatalog.GetVoices()` → cached/background-refreshed catalog; `IEdgeSynthesizer.SynthesizeAsync(string text, string voice, string rate, CancellationToken token)` → temp MP3 path; `IAudioPlayer.Play(string file, int volume)` / `Stop()`; `EdgeSpeechEngine(Func<AppConfig>, IEdgeSynthesizer, IAudioPlayer, ISpeechEngine fallback, EdgeVoiceCatalog catalog)`.

- [ ] **Step 1: Add failing catalog tests**

`EdgeCatalog_ParsesMicrosoftJson`: a sample array with `ShortName`, `Locale`, `Gender` maps to `VoiceInfo(Name=ShortName, Description=ShortName, Language=Locale, Gender=Gender, IsDefault=false)`. `EdgeCatalog_NoCacheReturnsFallback`: with a nonexistent cache path, `GetVoices()` returns the bundled short list immediately and does not throw.

- [ ] **Step 2: Run filtered tests and confirm RED**

Run: `& 'C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe' test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj --filter FullyQualifiedName~EdgeEngineTests`
Expected: FAIL with missing types.

- [ ] **Step 3: Implement catalog**

`EdgeVoiceCatalog` reads `%LOCALAPPDATA%\Read Aloud\edge_voices.json` synchronously; on missing/stale cache returns a bundled fallback list of at least `en-US-AriaNeural`, `en-US-JennyNeural`, `en-US-GuyNeural`; starts a background `HttpClient` GET to `https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken=6A5AA1D4EAFF4E9FB37E23D68491D6F4`, parses with `Parse`, and writes the cache. Cache TTL: 24 hours. Expose a constructor accepting the cache path for tests.

- [ ] **Step 4: Add failing engine tests with fakes**

`EdgeEngine_UsesVoicesInPriorityOrder`: fake synthesizer always succeeds; priority `["v1","v2"]`; assert only `v1` called and audio played. `EdgeEngine_FallsBackToPiperWhenAllVoicesFail`: fake synthesizer always throws; fake fallback records `Speak`; assert fallback called once with original text and completion raised once. `EdgeEngine_PreviewVoiceTriedFirst`: `VoiceOptions.VoiceId = "preview"`; assert synthesizer called with `preview` before priority entries and config unchanged.

- [ ] **Step 5: Add packages and implement synthesis/playback**

Add `<PackageReference Include="edge-tts-net" Version="2.0.0" />` and `<PackageReference Include="NAudio" Version="2.2.1" />` to `src-cs/ReadAloud/ReadAloud.csproj`. `EdgeSynthesizer` calls `edge_tts_net.EdgeTTSNet.Save(text, tempPath, option, token)` with the curated voice ID and a `%` rate, enforces an 8-second timeout, and rejects empty output. `NAudioPlayer` uses `AudioFileReader` + `WaveOutEvent`, honors volume, and stops/disposes safely.

- [ ] **Step 6: Implement `EdgeSpeechEngine`**

Set `_isSpeaking`, then try `options.VoiceId` first (if non-empty), then each priority entry. On first synthesizer success, play audio, raise start progress, delay for the audio duration if known, raise end progress, and raise completion once. On total failure, call `fallback.Speak(text, options)`. Subscribe to fallback events once in the constructor and forward them as the engine's own events. `Stop()` cancels playback and the in-flight synthesis; `Pause()`/`Resume()` are no-ops. All exceptions are caught.

- [ ] **Step 7: Run filtered and full tests to GREEN**

Run filtered `EdgeEngineTests`, then the full suite. Expected: all pass.

- [ ] **Step 8: Commit**

`git commit -m "feat(speech): add C# Edge engine with ordered voices and Piper fallback"`

---

### Task 3: Route and compose the Edge engine

**Files:**
- Modify: `src-cs/ReadAloud/Services/SelectableSpeechEngine.cs`
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Test: `src-cs/ReadAloud.Tests/SpeechTests.cs`

**Interfaces:**
- Consumes: `EdgeSpeechEngine` from Task 2.
- Produces: `SelectableSpeechEngine` accepts `(sapi, piper, edge, configProvider)`; `edge` config value routes to Edge; same instance flows to PlaybackService, HomeViewModel, VoiceLibraryViewModel.

- [ ] **Step 1: Add failing routing test**

`SelectableEngine_RoutesEdge`: config `TtsEngine = "edge"`; assert `Speak` reaches the Edge fake and not SAPI/Piper fakes.

- [ ] **Step 2: Run filtered tests and confirm RED**

Run: `& 'C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe' test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj --filter FullyQualifiedName~SpeechTests`
Expected: FAIL with constructor mismatch.

- [ ] **Step 3: Implement routing and composition**

In `SelectableSpeechEngine`, add an `edge` field and a 4-engine constructor; `Active` switches on exact string: `sapi`, `edge`, otherwise `piper`. In `App.xaml.cs`, create `var catalog = new EdgeVoiceCatalog(); var edge = new EdgeSpeechEngine(() => _config!.Load(), new EdgeSynthesizer(), new NAudioPlayer(), piper, catalog);` and pass it to the selectable engine. Preserve all existing wiring.

- [ ] **Step 4: Run filtered and full tests to GREEN**

Expected: all pass.

- [ ] **Step 5: Commit**

`git commit -m "feat(app): compose selectable Edge speech engine"`

---

### Task 4: Per-engine voice selection and Voice Library copy

**Files:**
- Modify: `src-cs/ReadAloud/ViewModels/HomeViewModel.cs`
- Modify: `src-cs/ReadAloud/ViewModels/VoiceLibraryViewModel.cs`
- Modify: `src-cs/ReadAloud/Views/VoiceLibraryView.xaml`
- Test: `src-cs/ReadAloud.Tests/HomeTests.cs`
- Test: `src-cs/ReadAloud.Tests/VoiceLibraryTests.cs`

**Interfaces:**
- Consumes: `AppConfig.TtsEngine`, `AppConfig.EdgeVoicePriority`.
- Produces: `HomeViewModel.SelectedVoice` moves chosen voice to front of `EdgeVoicePriority` when Edge is active; `VoiceLibraryViewModel.IsOnlineEngine` boolean for subtitle binding.

- [ ] **Step 1: Add failing tests**

`SelectedVoiceMovesToFrontOfEdgePriority`: with `TtsEngine = "edge"`, setting `SelectedVoice` to `"en-US-GuyNeural"` makes it first in config and preserves the rest. `VoiceLibrary_OnlineSubtitle`: `IsOnlineEngine` is true for `edge`, false for `piper`/`sapi`.

- [ ] **Step 2: Run filtered tests and confirm RED**

Expected: FAIL with missing behavior/property.

- [ ] **Step 3: Implement**

In `HomeViewModel.SelectedVoice` setter, branch on `config.TtsEngine`: for `edge`, remove the value from `EdgeVoicePriority` and insert it at index 0 before saving; otherwise keep the existing `OfflineVoice` write. In `VoiceLibraryViewModel`, add `IsOnlineEngine` set from config in `Refresh()`, and keep preview audition-only. In `VoiceLibraryView.xaml`, bind the subtitle `Text` to a new `Subtitle` property or use a `Style` DataTrigger on `IsOnlineEngine`; no other layout/card changes.

- [ ] **Step 4: Run filtered and full tests to GREEN**

Expected: all pass.

- [ ] **Step 5: Commit**

`git commit -m "feat(ui): expose Edge voice priority and online voice library copy"`

---

### Task 5: Publish, install, verify, and push

**Files:**
- Modify: `dist-cs/ReadAloud.exe` (tracked binary)
- Install target: `C:\Users\malla\AppData\Local\Programs\ReadAloud\ReadAloud.exe`

- [ ] **Step 1: Full test run**

Run the full test command. Expected: 0 failures.

- [ ] **Step 2: Publish**

Run:
`& 'C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe' publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o dist-cs`
Expected: `PUBLISH_EXIT: 0`.

- [ ] **Step 3: Install**

Copy the published exe and all new managed DLLs from `dist-cs` into `C:\Users\malla\AppData\Local\Programs\ReadAloud\`, preserving existing settings. Update `%APPDATA%\Read Aloud\settings.json` with `tts_engine`, `edge_voice_priority`, and `edge_voice` while keeping all existing keys.

- [ ] **Step 4: Manual verification**

Launch the installed exe. With `edge` selected and internet available, confirm Voice Library lists Edge voices and Listen uses the first priority voice. Then disconnect/block network and confirm speech silently falls back to Piper with no error. Confirm the main window layout and Settings rows are unchanged apart from the new Edge rows.

- [ ] **Step 5: Commit and push**

Commit the rebuilt `dist-cs/ReadAloud.exe`, push `reliability-pass`, and confirm a clean working tree.
