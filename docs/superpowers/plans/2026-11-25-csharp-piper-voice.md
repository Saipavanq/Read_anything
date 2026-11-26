# C# Piper Natural Voice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the offline Piper natural voice to the existing C# WPF app, active by default, without changing its UI style.

**Architecture:** Keep `ISpeechEngine` unchanged and add `PiperSpeechEngine` plus a `SelectableSpeechEngine` that forwards to SAPI or Piper based on config. Piper synthesis runs in a small Python helper (`piper_helper.py`) using the existing `.venv`; C# plays the resulting WAV and raises `SpeakCompleted`. Additive config keys and additive controls in the existing Settings "Voice and playback" group.

**Tech Stack:** C# / .NET 8 WPF, `System.Speech`, `System.Media.SoundPlayer`, Python 3.10 `piper-tts` in `.venv`, xUnit tests, `dotnet publish` single-file build.

**Spec:** `docs/superpowers/specs/2026-11-25-csharp-piper-voice-design.md`

## Global Constraints
- Keep every existing view, theme, layout, tray menu, and config key unchanged.
- `SettingsViewModel` 4-argument constructor must keep working (tests use it).
- Do not add members to `ISpeechEngine` unless they have default implementations.
- Speech failures must never reach the UI; swallow exceptions like `SapiSpeechEngine`.
- Piper is the default engine on this PC; SAPI remains selectable.
- Build with `build-cs.bat`; installed exe path is `C:\Users\malla\AppData\Local\Programs\ReadAloud\ReadAloud.exe`.

## Review Focus
- Missing `.venv` / Python: helper launch failure must not crash the app; SAPI fallback still works.
- Missing or corrupt `.onnx` model: `GetVoices()` returns empty, `Speak` fails quietly, app stays usable.
- Stop during synthesis: helper process must be killed and WAV playback stopped.
- Old config files without new keys: defaults load and normalize cleanly.
- Existing config saved by C# must not lose unknown Python keys (`tts_engine`, whisper, etc.).

---

### Task 1: Config keys for Piper

**Files:**
- Modify: `src-cs/ReadAloud/Models/AppConfig.cs`
- Modify: `src-cs/ReadAloud/Services/ConfigService.cs`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`

**Interfaces:**
- Produces: `AppConfig.TtsEngine` (string, default `"piper"`), `PiperPython` (string, default `""`), `PiperModelDir` (string, default `""`), `PiperModel` (string, default `""`), `PiperSpeaker` (int, default `0`), `PiperLengthScale` (double, default `1.0`).

- [ ] **Step 1: Write failing tests** in `ConfigTests.cs`:
  - `PiperDefaults_AreApplied` — a fresh `AppConfig` has `TtsEngine == "piper"`, `PiperSpeaker == 0`, `PiperLengthScale == 1.0`, empty Python/model paths.
  - `PiperKeys_RoundTripThroughConfigService` — save a config with all six Piper values, reload, assert all equal.
  - `UnknownPythonKeys_SurviveCSharpSave` — seed legacy JSON with `tts_engine`, `whisper_model`, save via `ConfigService`, assert both keys still present.
  - `BadPiperNumbers_AreClamped` — `piper_speaker = -5` clamps to `0`; `piper_length_scale = 99` clamps to `10`; invalid `tts_engine` value falls back to `"piper"`.
- [ ] **Step 2:** Run `dotnet test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj --filter FullyQualifiedName~ConfigTests` and confirm the new tests fail.
- [ ] **Step 3:** Add the six properties to `AppConfig`; add snake_case keys to `ConfigService.KnownKeys`, `Apply`, `Save`, and `Normalize` (allowed engines `sapi|piper`; speaker `0..32`; length scale `0.1..10`).
- [ ] **Step 4:** Run the same test filter and confirm all pass.
- [ ] **Step 5:** Commit `feat(config): add Piper engine settings with backward-compatible defaults`.

### Task 2: Python helper and Piper speech engine

**Files:**
- Create: `piper_helper.py` (repo root, copied next to installed exe)
- Create: `src-cs/ReadAloud/Services/PiperSpeechEngine.cs`
- Create: `src-cs/ReadAloud/Services/SelectableSpeechEngine.cs`
- Test: `src-cs/ReadAloud.Tests/PiperEngineTests.cs`

**Interfaces:**
- Consumes: `AppConfig` Piper properties from Task 1; `ISpeechEngine`, `VoiceInfo`, `VoiceOptions`.
- Produces: `PiperSpeechEngine(Func<AppConfig> configProvider)` implementing `ISpeechEngine`; static `PiperSpeechEngine.ParseVoiceList(string json) -> IReadOnlyList<VoiceInfo>`; `SelectableSpeechEngine(ISpeechEngine sapi, ISpeechEngine piper, Func<AppConfig> configProvider) : ISpeechEngine`.

- [ ] **Step 1: Write failing tests** in `PiperEngineTests.cs`:
  - `ParseVoiceList_MapsOnnxFiles` — helper JSON `[{"name":"en_US-amy-medium","path":"C:\\v\\en_US-amy-medium.onnx"}]` maps to one `VoiceInfo` with `Name == "en_US-amy-medium"` and `Description == path`.
  - `ParseVoiceList_EmptyAndMalformed_ReturnEmpty` — `""`, `"null"`, `"not-json"` all return empty.
  - `SelectableEngine_RoutesByConfig` — with `TtsEngine == "sapi"` only the SAPI fake receives `Speak`; switching to `"piper"` routes to the Piper fake.
  - `SelectableEngine_ForwardsEventsAndStop` — `SpeakCompleted` and `SpeakProgress` from active engine reach subscribers; `Stop()` reaches active engine only.
- [ ] **Step 2:** Run `dotnet test ... --filter FullyQualifiedName~PiperEngineTests` and confirm failure.
- [ ] **Step 3:** Create `piper_helper.py` with commands: `--list --model-dir DIR` prints a JSON array of `{"name","path"}` for `*.onnx`; `--synthesize --model FILE --config FILE --text-file FILE --wav FILE --speaker N --length-scale X` loads the model with `piper.PiperVoice.load`, synthesizes to WAV, prints `DURATION=<seconds>`. All imports/errors handled so stderr stays clean; exit code 0 only on success.
- [ ] **Step 4:** Implement `PiperSpeechEngine`:
  - `GetVoices()` runs helper `--list`, parses JSON via `ParseVoiceList`, catches all exceptions and returns empty.
  - `Speak()` writes text to a UTF-8 temp file, runs helper `--synthesize`, reads `DURATION`, plays WAV with `SoundPlayer.Play()` on a worker, and raises `SpeakCompleted` after the reported duration; raises `SpeakProgress` once at start/end; all exceptions swallowed.
  - `Stop()` kills the current helper process, calls `SoundPlayer.Stop()`, clears state.
  - Resolve Python: config `PiperPython` → `<repo>\.venv\Scripts\python.exe` if it exists → `python` on PATH. Resolve helper: `Path.Combine(AppContext.BaseDirectory, "piper_helper.py")`.
- [ ] **Step 5:** Implement `SelectableSpeechEngine` forwarding all `ISpeechEngine` members to the engine named by `configProvider().TtsEngine`.
- [ ] **Step 6:** Run `dotnet test ... --filter FullyQualifiedName~PiperEngineTests` and confirm all pass.
- [ ] **Step 7:** Commit `feat(speech): add Piper engine and selectable engine routing`.

### Task 3: Settings UI controls

**Files:**
- Modify: `src-cs/ReadAloud/Views/SettingsWindow.xaml` (only the "Voice and playback" group, ~lines 104–117)
- Modify: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: `AppConfig` Piper properties from Task 1.
- Produces: `SettingsViewModel.TtsEngine`, `PiperPython`, `PiperModelDir`, `PiperModel`, `PiperSpeaker`, `PiperLengthScale` bindable properties; `SpeechEngines` list (`"sapi"`, `"piper"`).

- [ ] **Step 1: Write failing tests** in `SettingsTests.cs`:
  - `PiperSettings_LoadIntoViewModel` — `new SettingsViewModel(...)` reads all six values from config.
  - `PiperSettings_SaveBackToConfig` — set ViewModel properties, call `Save()`, assert config holds them.
- [ ] **Step 2:** Run `dotnet test ... --filter FullyQualifiedName~SettingsTests` and confirm failure.
- [ ] **Step 3:** Add the six properties to `SettingsViewModel` (initialized in its load-from-config method, written in `ApplyTo`), plus `SpeechEngines`.
- [ ] **Step 4:** Add rows inside the existing "Voice and playback" group: "Speech engine" ComboBox bound to `TtsEngine` with `sapi`/`piper` items; TextBoxes for Piper Python, model folder, voice; sliders for speaker and voice rate. Use the same styles/labels as neighboring rows; no new GroupBox.
- [ ] **Step 5:** Run `dotnet test ... --filter FullyQualifiedName~SettingsTests` and confirm all pass (including the existing 4-argument constructor tests).
- [ ] **Step 6:** Commit `feat(settings): expose Piper voice options in existing settings group`.

### Task 4: Compose engine, install and verify

**Files:**
- Modify: `src-cs/ReadAloud/App.xaml.cs` (replace `var speech = new SapiSpeechEngine();` at line 63)
- Test: `src-cs/ReadAloud.Tests/SpeechTests.cs` (add one test)
- Local only: install folder `C:\Users\malla\AppData\Local\Programs\ReadAloud\`

**Interfaces:**
- Consumes: `SelectableSpeechEngine`, `PiperSpeechEngine`, config keys from Tasks 1–3.
- Produces: running app with Piper active by default.

- [ ] **Step 1: Write failing test** `SelectableEngine_DefaultConfigUsesPiper` — with a fresh `AppConfig`, `SelectableSpeechEngine` routes `Speak` to the Piper fake.
- [ ] **Step 2:** Run `dotnet test ... --filter FullyQualifiedName~SpeechTests` and confirm failure.
- [ ] **Step 3:** In `App.OnStartup`, create `sapi`, `piper`, then `ISpeechEngine speech = new SelectableSpeechEngine(sapi, piper, () => _config!.Load());` and pass that instance everywhere the old `speech` variable was used.
- [ ] **Step 4:** Run the full `dotnet test src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj`, confirm all pass.
- [ ] **Step 5:** Commit `feat(app): use selectable speech engine with Piper default`.
- [ ] **Step 6:** Run `build-cs.bat`, confirm exit 0 and `dist-cs\ReadAloud.exe` timestamp updated.
- [ ] **Step 7:** Copy `dist-cs\ReadAloud.exe`, `piper_helper.py`, and `en_US-amy-medium.onnx` + `.json` into the installed folder's `piper_voices\`; write `%APPDATA%\Read Aloud\settings.json` with `tts_engine=piper`, model paths, and Python path to `.venv\Scripts\python.exe`, preserving all existing keys.
- [ ] **Step 8:** Launch the installed exe, OCR its window to confirm the UI layout is unchanged apart from new settings fields, and run a direct helper synthesis check that produces non-empty PCM.
