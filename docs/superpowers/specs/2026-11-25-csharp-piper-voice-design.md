# Design: Add Piper natural voice to the C# UI (2026-11-25)

## Intent
The installed app is the C# WPF build (`dist-cs\ReadAloud.exe`), not the Python build.
The user wants the natural offline Piper voice that was built for the Python app added to
this C# app, with Piper active by default, while keeping the existing C# UI exactly as it
looks today.

## Goals
- Keep every existing view, theme, layout, tray menu and config key unchanged.
- Additively extend **Settings → Voice and playback** with a speech-engine choice and
  Piper-specific fields.
- Make Piper the active engine by default on this PC, with a voice model preinstalled.
- Keep the old SAPI voice working as a selectable fallback.

## Non-goals / anti-goals
- No change to window chrome, colors, fonts, status text or any visible badge.
- No voice-model download UI (models are installed as files for now).
- No native C# ONNX reimplementation; no bundled Python runtime.

## Constraints
- .NET 8 WPF, single-file self-contained publish via `build-cs.bat`.
- Piper runs through a Python helper using the existing project `.venv` (`piper-tts`).
- Speech failures must never reach the UI (matches `SapiSpeechEngine` behavior).
- Existing config files must keep loading; unknown JSON keys are preserved by `ConfigService`.

## Approaches considered
1. **Python helper (chosen)** — C# calls `piper_helper.py` to synthesize a WAV, then plays it.
   Reuses the verified Piper pipeline; quick and low-risk. Requires the `.venv` to stay present.
2. Bundled portable Python — self-contained but ~100 MB+ and a much slower build.
3. Native C# ONNX — no Python at runtime, but reimplementing espeak-ng phonemization is
   high-effort and high-risk.

## Chosen design
- **Engine**: new `PiperSpeechEngine : ISpeechEngine` alongside `SapiSpeechEngine`.
  - `Speak(text, options)` writes text to a UTF-8 temp file, launches the configured Python
    with `piper_helper.py --text-file ... --wav ... --model ... --speaker ... --length-scale ...`,
    waits for exit, then plays the WAV on a background thread and raises `SpeakCompleted`.
  - `Stop()` kills the helper process and stops playback; raises no completion event.
  - `GetVoices()` runs `--list` to enumerate `*.onnx` in the configured model folder and maps
    them to `VoiceInfo`, so the existing Voice Library shows Piper voices.
  - `Pause()`/`Resume()` are no-ops (same as default interface behavior).
- **Composition** in `App.xaml.cs`: create `SapiSpeechEngine` and `PiperSpeechEngine`, then pass
  a `SelectableSpeechEngine` that forwards to the engine named by config. Single instance is
  still shared by `PlaybackService`, `HomeViewModel` and `VoiceLibraryViewModel`.
- **Config** (`AppConfig` + `ConfigService`, backward compatible):
  - `TtsEngine` (default `piper`), `PiperPython`, `PiperModelDir`, `PiperModel`,
    `PiperSpeaker` (default 0), `PiperLengthScale` (default 1.0).
  - JSON keys: `tts_engine`, `piper_python`, `piper_model_dir`, `piper_model`,
    `piper_speaker`, `piper_length_scale`. Added to `KnownKeys` so round-trips are clean.
- **Settings UI**: inside the existing "Voice and playback" group only —
  "Speech engine" combo (System voice / Natural voice - Piper), "Piper model folder",
  "Piper voice", "Python", "Speaker", "Voice rate". No new group box or styling changes.
- **Install on this PC**: copy `en_US-amy-medium.onnx` + `.json` into
  `C:\Users\malla\AppData\Local\Programs\ReadAloud\piper_voices\`, write engine/model/python
  settings into `%AppData%\Read Aloud\settings.json`, rebuild `dist-cs\ReadAloud.exe`, and
  install it to the user's path. Verify by OCR that the UI is unchanged apart from the new
  settings fields and that speech is produced by the Piper model.

## Builder latitude
- Use `System.Media.SoundPlayer` for WAV playback unless a subtle issue requires another API.
- Resolve Python path: configured value → project `.venv\Scripts\python.exe` if present →
  `python` on PATH. Log a clear message when unavailable.
- Add small unit tests for config round-trip and voice listing; no GUI screenshot tests.
