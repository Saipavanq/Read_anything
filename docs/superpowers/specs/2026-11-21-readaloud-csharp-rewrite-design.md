# Read Aloud C# WPF Rewrite Design

Date: 2026-11-21
Status: approved

## Intent

The current Python/Tkinter app works, but three problems keep showing up:

1. Cross-application text selection is unreliable. The installed log shows read requests starting correctly, but several end with `no selection after ~1400ms`; the current `auto` capture path spends about that long trying UI Automation before falling back to Ctrl+C. Other requests succeed through the clipboard fallback. This is a capture problem, not a hotkey or speech problem.
2. Hotkeys exist in Settings only as plain text fields. Users cannot discover or record them easily.
3. The interface looks like a traditional Tk tool. The user wants two modern directions — **Aurora Compact** and **Orbit Console** — switchable from Settings.

The user chose a full rewrite in **C# + WPF on .NET 8**, with the first release covering the core reading experience. Whisper voice commands, edge-tts, and local LLM modes are deliberately deferred.

## Goals

- Ship a Windows 10 desktop app written in C# with WPF, with no Python runtime required.
- Read selected text from other applications reliably, using an explicit and observable capture strategy.
- Provide click-to-record hotkey controls with validation and collision detection.
- Provide both **Aurora Compact** and **Orbit Console** themes, switchable from Settings.
- Keep offline SAPI speech as the default and preserve the existing offline-first behaviour.
- Keep a self-contained packaged build that runs without the .NET SDK installed.
- Add focused automated tests around capture, hotkeys, config migration, and theme switching.

## Non-goals

- Whisper voice commands in the first C# release.
- Edge online voice in the first C# release.
- Local LLM modes (summarize/simplify/translate/custom) in the first C# release.
- Exact pixel copying of the mockups; they define direction, not final art.
- Any cloud account, telemetry, or network requirement.
- Rewriting historical Python source in place; the Python app stays available until the C# build passes verification.

## Constraints

- Windows 10 only.
- .NET 8 SDK is not currently installed; install it before implementation.
- Existing users may have `config/settings.json` from the Python app. The C# app should read it once and migrate known keys.
- The default configuration must stay fully offline.
- Config format should remain plain JSON and human-editable.
- Tests should be runnable with one command after the SDK is installed.
- The C# project should live beside the existing Python source so nothing is lost during migration.

## Approaches considered

### 1. Keep Python/Tkinter and restyle it

Lowest risk and no new toolchain, but both visual directions would be limited by Tk's native widget model. Click-to-record hotkeys and capture diagnostics are still possible. Rejected because the user explicitly wants a different language and a higher visual ceiling.

### 2. Keep Python and use CustomTkinter/ttkbootstrap

Better theming with less risk than a rewrite, and all existing modules would keep working. Rejected because the user chose a full rewrite rather than another Python UI layer.

### 3. Full rewrite in C# + WPF, core features first — chosen

Native Windows APIs for global hotkeys, clipboard, UI Automation, and SAPI; XAML ResourceDictionaries make Aurora/Orbit theme switching straightforward. The cost is that voice commands, edge-tts and LLM modes are absent until a later pass. This matches the user's request and keeps the first release testable.

### 4. Pure C# including Whisper, edge-tts and LLM from day one

Removes Python entirely in one step, but requires porting faster-whisper/CTranslate2, Edge TTS WebSocket protocol, and an OpenAI-compatible HTTP client. Much larger and riskier; deferred.

## Architecture

### Project layout

```text
src-cs/
  ReadAloud.sln
  ReadAloud/
    ReadAloud.csproj
    App.xaml
    App.xaml.cs
    Views/
      MainWindow.xaml
      MainWindow.xaml.cs
      SettingsWindow.xaml
      SettingsWindow.xaml.cs
    Themes/
      Aurora.xaml
      Orbit.xaml
    Models/
      AppConfig.cs
      CaptureResult.cs
    Services/
      ConfigService.cs
      HotkeyService.cs
      SelectionService.cs
      SpeechService.cs
      ActivityLog.cs
  ReadAloud.Tests/
    ReadAloud.Tests.csproj
    CaptureTests.cs
    HotkeyTests.cs
    ConfigTests.cs
    ThemeTests.cs
```

### Configuration

`ConfigService` loads `%APPDATA%\Read Aloud\settings.json` (or a portable `config/settings.json` beside the exe when present), migrates known Python keys, and writes back a C# config with these fields:

```json
{
  "schemaVersion": 1,
  "theme": "aurora",
  "hotkey_read": "Ctrl+Alt+R",
  "hotkey_voice": "Ctrl+Alt+V",
  "hotkey_stop": "Ctrl+Alt+X",
  "capture_method": "ClipboardFirst",
  "offline_voice": "",
  "sapi_rate": 0,
  "volume": 100,
  "max_chars": 4000,
  "show_activity": true
}
```

Unused Python keys are preserved as unknown fields but ignored by the C# app. Values are normalised on load: theme must be `aurora` or `orbit`, hotkeys must validate, numeric values clamp to safe ranges.

### Theme system

`App.xaml` merges `Themes/Aurora.xaml` by default. `AppConfig.Theme` selects the dictionary. Settings contains a labelled two-state switch:

- **Aurora Compact** — light, high-contrast, friendly; default.
- **Orbit Console** — dark sidebar, status ring, compact hotkey tiles.

Switching the theme updates the merged ResourceDictionary immediately and persists the choice. The two themes share the same control templates and behaviour; only colours, spacing tokens, and a few layout states differ.

### Hotkey recording

`HotkeyService` owns:

- Display strings (`Ctrl+Alt+R`) and native registration.
- Validation: exactly one non-modifier key, no empty segments, no duplicate actions.
- A recorder mode used by Settings. Each row has a **Change** button; when armed, the next KeyDown captures modifiers + key, shows the value, and validates it before saving.
- Immediate persistence after a successful change, with an error message shown inline for duplicates or malformed combinations.

Hotkeys are registered through the Win32 `RegisterHotKey` API on a hidden WPF message window rather than a global low-level hook. This reduces interference and gives clear failure messages when another app owns the combination.

### Capture strategy

`SelectionService` exposes `CaptureAsync(AppConfig)` and returns `CaptureResult` with text, method, and a short diagnostic message.

Default `ClipboardFirst` order:

1. Snapshot the current clipboard formats.
2. Wait briefly for the triggering hotkey's modifiers to be released, then send a clean Ctrl+C using `SendInput` with only Ctrl held.
3. Poll for a clipboard change with a condition-based wait, maximum 600 ms.
4. Restore the original clipboard formats.
5. If text was captured, return it with method `clipboard`.
6. If no text was captured, try UI Automation through `System.Windows.Automation` with a 500 ms timeout and return method `uia` if a TextPattern selection exists.
7. Otherwise return an empty result with a diagnostic: `clipboard unchanged; UIA no selection`.

Explicit modes are also available:

- `ClipboardFirst` — default; fastest and matches user behaviour.
- `ClipboardOnly` — never tries UIA.
- `UiaFirst` — preserves the old behaviour for apps where UIA works well.
- `UiaOnly` — diagnostic mode for apps that support UIA but not Ctrl+C.

The activity panel shows the method used and the diagnostic on failure. This replaces the old silent ~1.4 s timeout.

### Read flow

1. Hotkey or Read button queues a request on the WPF dispatcher; any pending request is discarded so the newest read wins.
2. Main thread snapshots config; no worker touches UI controls.
3. Capture runs through `SelectionService`.
4. Text is truncated to `max_chars` if needed.
5. Offline SAPI speaks the text through `System.Speech.Synthesis.SpeechSynthesizer` using `offline_voice`, `sapi_rate`, and `volume`.
6. Stop cancels synthesis immediately and clears the queue.

Voice/LLM/edge modes are not wired in the first C# release; Settings should say so plainly rather than offering controls that do nothing.

### UI structure

Both themes use the same logical layout:

- Header: product name, status badge, theme label.
- Primary action: large Read Selection button showing the current read hotkey.
- Capture selector: ClipboardFirst/ClipboardOnly/UiaFirst/UiaOnly.
- Hotkey summary: three read-only rows showing the current combinations, with `Settings…` for changes.
- Activity panel: timestamped capture/speech/status messages.
- Footer: Settings, Hide to tray, Open log.

Aurora Compact uses a light surface, teal accent, rounded cards, and clear typography. Orbit Console uses a dark surface, electric-blue accent, left rail, status ring, and compact tiles. Neither relies on colour alone: status text and icons carry meaning too.

### Packaging

Publish with:

```powershell
dotnet publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output goes to `dist-cs/ReadAloud.exe`. Logs go to a per-user writable directory, not the install folder.

## Decisions settled during builder check

These are calls made by the author from earlier user decisions and existing app behaviour, not new user choices:

1. **`Auto` is a legacy alias.** The Python `auto` value migrates to `ClipboardFirst`. It is not exposed as a fifth UI option; the selector offers `ClipboardFirst`, `ClipboardOnly`, `UiaFirst`, and `UiaOnly`. The config normaliser accepts `auto` only for migration.
2. **A new read interrupts the current one.** This preserves the Python app's established behaviour. A new Read stops current speech, discards pending requests, and processes only the newest request. Stop cancels current speech and clears pending requests. A capture already in progress cannot be interrupted safely, but its result is discarded if superseded.
3. **Migration source and marker are explicit.** On first run, if `%APPDATA%\Read Aloud\settings.json` does not exist, read the legacy file at `AppContext.BaseDirectory/config/settings.json`. Write the C# config to `%APPDATA%\Read Aloud\settings.json` with `"schemaVersion": 1`. Later edits to the legacy file are ignored. If both files exist, the C# file wins.
4. **Hotkey recorders live in Settings.** The main window shows the current hotkeys read-only and has a `Settings…` entry. In Settings v1, the editable controls are: theme toggle, read/stop hotkey recorder rows, capture mode, offline voice, SAPI rate, volume, max characters, and activity visibility. The voice-command row is shown as a disabled placeholder labelled “Voice commands — later release”; its config key is still migrated and persisted.
5. **Recording suppresses global registration.** Arming a recorder temporarily unregisters all global hotkeys so `Ctrl+Alt+R` can be captured instead of firing a read. Hotkeys are re-registered after Save or Cancel using the saved values.
6. **Tests use injected OS seams.** `SelectionService` depends on `IClipboard`, `IKeyboardSender`, `IUiaSelectionReader`, and `IClock`; unit tests use fakes so they never touch the user's clipboard and need no foreground window. Real clipboard/SendInput/UIA behaviour is verified manually in the smoke test only.
7. **Startup hotkey conflicts are non-fatal.** Conflicting actions are skipped, the remaining hotkeys register, and the conflict is shown in the activity panel. The config is not silently changed; the user fixes it in Settings.
8. **Tray and close semantics match the Python app.** Window X exits the app. `Hide to tray` hides the window. The tray menu contains Show window, Read selected text, Stop speaking, Settings…, and Quit; voice/LLM entries are omitted until those features return.

## Verification plan

- Install .NET 8 SDK and confirm `dotnet --version`.
- Unit tests:
  - `CaptureTests`: clipboard-first returns text; restores clipboard; falls back to UIA only when clipboard yields nothing; logs diagnostic on total failure; clean modifier sequence is used.
  - `HotkeyTests`: recorder accepts `Ctrl+Shift+R`; rejects modifier-only; rejects duplicates; display string round-trips; arming unregisters global hotkeys; a startup conflict skips only that action.
  - `ConfigTests`: migrates Python keys from the installed legacy path; writes `schemaVersion: 1`; ignores later legacy edits; clamps bad numbers; defaults theme to aurora; migrates `auto` to `ClipboardFirst`; persists theme toggle.
  - `ThemeTests`: switching config theme changes the merged ResourceDictionary and persists.
- Run `dotnet test src-cs/ReadAloud.sln`.
- Manual smoke test on the installed build: select text in Notepad and a browser, press the recorded hotkey, confirm activity panel shows `clipboard` or `uia`, confirm speech starts, change theme in Settings, change a hotkey, restart app and confirm both persisted.
- Re-run the original failure case from the installed log: a selection in another application that previously produced `no selection after ~1400ms`.

## Risks and mitigations

- **RegisterHotKey conflicts:** show an inline error and leave the previous hotkey until the user picks another.
- **Clipboard restore failures:** log per-format failures but never fail the read because restore failed.
- **UIA permission differences:** keep explicit capture modes and diagnostics so users can switch strategies without reinstalling.
- **SAPI voice availability:** list installed voices and fall back to system default if the saved voice is missing.
- **No .NET SDK yet:** install it after design approval; keep Python build untouched until C# verification passes.
- **Theme switching while dialogs are open:** apply theme only on the main dispatcher and close/reopen Settings after a switch if needed.
