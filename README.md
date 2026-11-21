# Read Aloud — selected-text speech assistant

A small **Windows desktop application with a visible window**, similar in idea to **CopySpeech**, that reads text you have selected in any application. It starts fully offline and can optionally use local LLM models and local speech recognition.

## Quick start

1. Double-click **`dist\ReadAloud\ReadAloud.exe`** (standalone Windows app). A **Read Aloud** window opens on your desktop. Alternatively, `run.bat` launches the same app from the Python environment.
2. Select text anywhere (browser, Word, PDF, etc.).
3. Press **`Ctrl+Alt+R`**, or click **Read selected text** in the window — the text is read aloud.

The window has buttons for reading, voice commands and stop, plus engine/mode/volume controls and an activity log. **Hide to tray** removes the window but keeps hotkeys working; the tray icon can restore it. Closing the window with **X** exits the app.

> CopySpeech uses `Win+Shift+A` (and a double `Ctrl+C` trigger). This app uses **`Ctrl+Alt+R`** as you requested. All hotkeys are editable in Settings.

Other hotkeys:

| Hotkey | Action |
|---|---|
| `Ctrl+Alt+R` | Read the current selection aloud |
| `Ctrl+Alt+V` | Voice command (push-to-talk) — editable in Settings |
| `Ctrl+Alt+X` | Stop speaking immediately |

A new read interrupts the current one.

## Voice commands

Press the voice hotkey, then say one of:

- **"read this"** — read the selection verbatim
- **"summarize"** — summarize it with the local LLM
- **"simplify"** — rewrite it plainly
- **"translate"** — translate it (target language in Settings)
- **"stop"** — stop speaking

Transcription runs locally with the cached `Systran/faster-whisper-base` model found in `C:\Users\malla\voicebox models`. No audio is uploaded.

## Speech engines

- **Offline Windows voice** (default): Microsoft David / Zira via SAPI. No internet.
- **Online natural voice**: `edge-tts` via Microsoft Edge speech endpoints. Better quality; the selected text is sent online. Falls back to the offline voice if the network fails.

Switch in the tray menu **Speech engine**, or in **Settings**.

## Local LLM (optional)

The app can process text before speaking it. It talks to an OpenAI-compatible local API:

- **LM Studio**: enable its local server (default `http://localhost:1234/v1`) and load a model.
- **Ollama**: if installed, `http://localhost:11434/v1` is detected too.

Steps:

1. Tray → **Settings** → tick **Use local LLM**.
2. Click **Detect local LLM** to fill the base URL and model automatically.
3. Choose a **Reading mode** in the tray menu or Settings: summarize / simplify / translate / custom.
4. Leave mode on **Verbatim** to skip the LLM entirely.

If no server is running, the app reads verbatim and shows a notification.

## Settings

Click **Settings…** in the window (or the tray menu) to change hotkeys, voices, rate, volume, capture method, LLM endpoint/model/mode, and voice-command timing. Settings are saved to `config/settings.json`.

Selection capture order: UI Automation first (does not touch the clipboard), then a `Ctrl+C` fallback that restores your original clipboard contents.

## Troubleshooting

- **Nothing happens on the hotkey:** make sure another app is not using the same combination; change it in Settings. Check `logs/app.log`.
- **"No text selected":** highlight text first, or switch capture method to `clipboard` in Settings (some PDF viewers do not expose UI Automation).
- **LLM mode not applied:** start LM Studio's local server and click **Detect local LLM** in Settings.
- **Voice command fails:** confirm a microphone is available; the first run loads the Whisper model and can take a few seconds.
- **Online voice fails:** the app falls back to the offline Windows voice.

## Diagnostics

Every read request is tagged with a short `request_id` in `logs/app.log`, for example:

```
2026-11-21 18:51:57,123 INFO     [read-worker] request_id=9f3a2c10 start mode=default
2026-11-21 18:51:58,010 INFO     [read-worker] request_id=9f3a2c10 completed in 887ms chars=412 mode=verbatim
```

Use the ID to correlate the log lines belonging to one request, including capture, LLM processing, speech and failures. The activity panel shows user-facing status messages with timestamps, but does not include request IDs.

## Files

- `dist\ReadAloud\ReadAloud.exe` — standalone Windows application
- `PLAN.md` — approved plan and report
- `src/` — application modules
- `config/settings.json` — created on first Settings save (when using `run.bat`)
- `logs/app.log` — diagnostics
- `requirements.txt` / `.venv` — Python environment
- `build.bat` — rebuilds the standalone `.exe` into `dist\ReadAloud`

When running the packaged `.exe`, its settings and log are created beside it in `dist\ReadAloud\config` and `dist\ReadAloud\logs`.

Run tests with:

```
.venv\Scripts\python.exe -m unittest discover -s tests
```
