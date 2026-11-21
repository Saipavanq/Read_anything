# Plan & Report — Windows "Read Selected Text Aloud" Assistant

**Status:** Draft for review and approval. **No application code has been written yet.**
**Date:** 2026-11-20
**Workspace:** `C:\Users\malla\Downloads\Read anything` (currently empty)

---

## 1. Objective

Build a small Windows desktop application that stays resident in the system tray. When the user selects text in any application (browser, PDF reader, editor, etc.) and "calls the model" — i.e. triggers the app with a global hotkey or voice command — the app captures the selected text and **reads it aloud** using a configurable text-to-speech (TTS) engine. Optional processing by a local LLM (Ollama) or internet-based speech models is supported behind switches.

## 2. Interpretation of the request (please confirm)

| Your words | My interpretation |
|---|---|
| "local application laptop windows" | A Windows desktop app that runs locally on your laptop, no browser required. |
| "if I select and text like call the model" | You highlight text anywhere, then invoke the app by **global hotkey or voice command** (both in the first build, per your approval). Reference app behaviour: CopySpeech (select text → trigger → speak). |
| "should be picking the text loud" | The app speaks the selected text out loud (TTS). |
| "using local LLM models" | Optional local Ollama model can transform text first (summarize, simplify, translate, custom prompt). |
| "or any speech recognizable models using internet" | Optional internet-based neural TTS and/or speech-to-text for voice commands. |

If any row is wrong, tell me and I will revise the plan before implementation.

## 3. Scope

### MVP (Phase 1)
- Global hotkey, default **`Ctrl+Alt+R`** (chosen by you; CopySpeech uses Win+Shift+A), configurable.
- Selection capture from the foreground app:
  1. Try Windows UI Automation (`TextPattern`) to read the selection without touching the clipboard.
  2. Fallback: simulate `Ctrl+C`, read clipboard, then **restore the original clipboard** contents.
- Speak the text aloud with offline Windows SAPI voices — works with no internet.
- **Voice trigger:** press a push-to-talk hotkey (default `Ctrl+Alt+V`, editable in Settings), say "read this" / "stop" / "summarize"; transcription uses the already-cached local `faster-whisper-base` model (no internet).
- Connect to already-installed local LLM models through LM Studio's OpenAI-compatible API (with Ollama-compatible API as an alternative).
- System-tray app: enable/disable, "read now", quit, and a small settings window.
- Clear user feedback: tray tooltip/status ("Reading…", "No text selected", "Unsupported app").
- Logging to a local file for debugging.

### Phase 2
- Higher-quality online neural TTS via `edge-tts` (uses Microsoft Edge speech endpoints, **no API key**, requires internet), with automatic fallback to offline SAPI.
- Voice selection and speech rate/volume settings.

### Phase 3
- Optional local LLM preprocessing via Ollama HTTP API (`http://localhost:11434`):
  - Modes: read verbatim (default), summarize, simplify, translate, custom prompt.
  - Separate hotkey per mode, or a mode selector in the tray menu.
  - Never sends text anywhere when using local Ollama.

### Phase 4 (refinements)
- Wake-word / hands-free continuous listening (current build uses push-to-talk).
- Cloud TTS providers (OpenAI / Azure) behind explicit API-key settings; cloud Whisper fallback if local STT is unavailable.

### Out of scope
- Screen OCR of images / scanned PDFs (possible future addition).
- Editing or rewriting the source application's text.
- Mobile platforms; Windows only.

## 4. Architecture

```
┌───────────────────────────────────────────────────────────┐
│ User selects text in any Windows app                      │
└───────────────────────┬───────────────────────────────────┘
                        │  global hotkey (or voice command)
                        ▼
┌───────────────────────────────────────────────────────────┐
│ Hotkey listener (pynput / Win32 RegisterHotKey)            │
└───────────────────────┬───────────────────────────────────┘
                        ▼
┌───────────────────────────────────────────────────────────┐
│ Selection capture                                          │
│   1) UI Automation TextPattern (no clipboard change)       │
│   2) fallback: Ctrl+C → clipboard → restore clipboard      │
└───────────────────────┬───────────────────────────────────┘
                        ▼  plain text
┌───────────────────────────────────────────────────────────┐
│ Optional LLM processor (toggle)                            │
│   local Ollama | (later) cloud LLM                         │
└───────────────────────┬───────────────────────────────────┘
                        ▼
┌───────────────────────────────────────────────────────────┐
│ TTS router                                                 │
│   offline SAPI (default) | edge-tts online | API providers │
└───────────────────────┬───────────────────────────────────┘
                        ▼
┌───────────────────────────────────────────────────────────┐
│ Audio output + tray status + log file                      │
└───────────────────────────────────────────────────────────┘
```

## 5. Technology options

| Concern | Option A (recommended) | Option B | Reasoning |
|---|---|---|---|
| Language/runtime | Python 3.11+ | C# / .NET WPF | Python is fastest to build and iterate; C# gives a smaller native .exe but more boilerplate. |
| Global hotkey | `pynput` (user-level) | Win32 `RegisterHotKey` / AutoHotkey | `pynput` avoids most admin-rights issues; Win32 is more robust for locked screens. |
| Selection capture | UI Automation first, clipboard fallback | Clipboard only | UIA avoids clobbering the clipboard; fallback covers apps that don't expose UIA. |
| Offline TTS | `pyttsx3` (SAPI5) | Windows Media SpeechSynthesis (WinRT) | `pyttsx3` is simple and dependency-light; natural voices need WinRT integration. |
| Online TTS | `edge-tts` (no key) | OpenAI / Azure (API key) | `edge-tts` gives natural-sounding voices at no cost, internet required. |
| Local LLM | Ollama HTTP API | llama.cpp direct | Ollama is one install and one endpoint; easy model switching. |
| Speech input | `faster-whisper` local (cached `Systran/faster-whisper-base`) | OpenAI Whisper API | Local keeps audio private and the model is already on this machine; API is a fallback. |
| Local LLM API | LM Studio OpenAI-compatible endpoint | Ollama endpoint | LM Studio is already installed with models here; support both via one configurable base URL. |
| Tray UI | `pystray` + Pillow | Win32 shell notify icon | `pystray` is portable and small. |
| Packaging | PyInstaller one-folder/one-file | plain `.bat` launcher | PyInstaller produces a normal Windows app; launcher is simplest for testing. |

## 6. Privacy and security

- Selected text is **never sent anywhere** in the default configuration (offline TTS, LLM disabled).
- Enabling `edge-tts` sends the selected text to Microsoft's Edge speech endpoint; this must be an explicit user choice shown in settings.
- Enabling cloud LLM/STT/TTS sends text/audio to the configured provider; API keys stored in a local config file, never logged.
- Clipboard contents are restored after capture; a failed capture leaves the clipboard untouched.
- No telemetry.

## 7. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Some apps don't support `Ctrl+C` on selection | UIA first; show clear "unsupported app" message; document workarounds. |
| Clipboard restore fails (complex formats) | Preserve multiple clipboard formats via `win32clipboard`; best-effort restore with warning. |
| Hotkey conflicts with other apps | Configurable hotkey; detect registration failure at startup. |
| `edge-tts` endpoint changes or is blocked | Offline SAPI fallback always available; clear error message. |
| Ollama not running | Local mode stays verbatim; settings show connection status. |
| Long selections produce very long speech | Configurable character limit with truncation warning and "read chunk N" behavior. |

## 8. Milestones and acceptance criteria

1. **M1 — Capture + offline speech:** Hotkey captures selected text from Notepad and Chrome and reads it aloud with an offline voice. Clipboard is restored. ✅ testable manually.
2. **M2 — Tray + settings:** App starts minimized to tray, hotkey works from any app, voice/rate configurable, logs written.
3. **M3 — Online voice:** `edge-tts` switch works and falls back offline when the network is unavailable.
4. **M4 — LLM modes:** With Ollama running, "summarize" mode speaks a summary; with Ollama off, behavior is verbatim.
5. **M5 — Voice commands (optional):** Saying "read this" triggers capture; "stop" cancels speech.
6. **M6 — Package:** Produce a portable Windows build plus a short README with setup steps.

## 9. Proposed file layout

```
Read anything/
├─ PLAN.md                  ← this document
├─ README.md                ← setup & usage (added with M1)
├─ src/
│   ├─ main.py              ← tray app, hotkey wiring
│   ├─ selection.py         ← UIA + clipboard capture
│   ├─ tts.py               ← TTS router (offline/online)
│   ├─ llm.py               ← optional Ollama processing
│   ├─ voice.py             ← optional STT commands (Phase 4)
│   ├─ config.py            ← settings load/save
│   └─ logging_setup.py
├─ config/settings.json     ← user settings (created on first run)
├─ tests/                   ← unit tests for capture/router logic
└─ build/                   ← PyInstaller spec and output
```

## 10. Open questions for your approval

1. ~~Is "call the model" a hotkey or voice command?~~ **Decided: both in the first build.**
2. ~~Default voice?~~ **Decided: offline Windows voice by default, online natural voice as a settings option.**
3. ~~Local LLM in first build?~~ **Decided: yes — reuse the models already installed on this machine.**
4. Should speech **stop immediately** when you press a stop hotkey, and should reading queue or interrupt if triggered again? **Working assumption: stop immediately; a new read interrupts the current one. Change if you prefer queuing.**
5. Which hotkey reads the selection? **Decided: `Ctrl+Alt+R`** (different from CopySpeech's `Win+Shift+A`). Voice-command hotkey retained but editable later (default `Ctrl+Alt+V`).

---

## 11. Change request — visible Windows window (2026-11-20)

**Request:** "Make it into a Windows application where it should have the physical window."

**Implemented change:** the primary interface is now a real Tkinter desktop window titled **Read Aloud — Selected Text Speech**, containing:
- Read / Voice command / Stop buttons
- Speech engine and reading-mode selectors
- Volume slider
- Activity log with status
- Settings, Hide to tray, and Open log buttons

The global hotkeys (`Ctrl+Alt+R`, `Ctrl+Alt+V`, `Ctrl+Alt+X`) continue to work while the window is open or hidden. The tray icon is now optional (available through **Hide to tray**) and can restore the window or quit the app.

The app was also packaged into a standalone Windows executable at `dist\ReadAloud\ReadAloud.exe` using PyInstaller (`build.bat` rebuilds it).

---

## 12. Approved decisions & detected environment (2026-11-20)

**Your approval:** "Both in first build" triggers, offline-default voice with online option, include local LLM using existing models, plan approved to start coding.

**Detected on this laptop:**
- Windows 10 Home, 64-bit.
- Python 3.10 at `C:\Users\malla\AppData\Local\Programs\Python\Python310\python.exe` (will create a dedicated project venv).
- Windows SAPI voices installed: **Microsoft David Desktop** and **Microsoft Zira Desktop**.
- **LM Studio** installed with local GGUF models including `gemma-4-E2B-it-GGUF`, `gemma-4-E4B-it-GGUF`, `LFM2.5-1.2B-Instruct-GGUF`, `Qwen3.5-9B-GGUF`, `gpt-oss-20b-GGUF`, and others.
- **Local Whisper cache:** `C:\Users\malla\voicebox models\models--Systran--faster-whisper-base` (138 MB `model.bin` present) and `openai/whisper-medium` also cached.
- Neither LM Studio's server (`localhost:1234`) nor Ollama (`localhost:11434`) is currently running; the app will detect them at runtime and tell you how to start them if a model mode is selected.
- No `ollama.exe` found in standard locations — LM Studio is the primary local LLM backend here; Ollama remains supported if installed later.

**Implementation consequence:** the first build uses a dedicated `.venv`, offline SAPI for speech, cached faster-whisper for spoken commands, and an auto-detected local OpenAI-compatible LLM endpoint (LM Studio first, Ollama fallback).

---

**Status:** approved — implementation is underway.
