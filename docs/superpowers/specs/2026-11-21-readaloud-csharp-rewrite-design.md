# Read Aloud C# WPF Rewrite Design — Aurora/Orbit with Phased Vox Features

Date: 2026-11-21
Status: awaiting approval
Supersedes: the earlier core-only Read Aloud C# WPF design approved on 2026-11-21

## Intent

The current Python/Tkinter app works, but cross-application selection capture is unreliable: the installed log shows read requests starting, then failing with `no selection after ~1400ms` when UI Automation finds nothing and the Ctrl+C fallback returns nothing. Other requests succeed through the clipboard fallback. Hotkeys are also hard to discover because Settings only offers plain text fields, and the UI still looks like a traditional Tk tool.

The user first chose a full rewrite in **C# + WPF on .NET 8**, with **Read Aloud** as the product name, **Aurora Compact** and **Orbit Console** as the two themes, and a core-first release. The user then supplied a richer Vox-style specification and clarified that those features should be documented and delivered in phases while keeping the earlier decisions. This document is the reconciled design: **Read Aloud**, C# WPF, Aurora/Orbit themes, all prior technical decisions preserved, and the Vox feature set placed into explicit phases.

## Goals

- Ship a Windows 10 desktop app written in C# with WPF, with no Python runtime required.
- Read selected text from other applications reliably, using an explicit and observable capture strategy.
- Provide click-to-record hotkey controls with validation and collision detection.
- Provide both **Aurora Compact** and **Orbit Console** themes, switchable from Settings.
- Keep offline SAPI speech as the default and preserve the existing offline-first behaviour.
- Keep a self-contained packaged build that runs without the .NET SDK installed.
- Document the full Vox-style feature set: sidebar navigation, manual input, playback controls, floating mini player, voice library, history, and expanded settings.
- Deliver those features in testable phases so the first C# release remains focused and verifiable.
- Add focused automated tests around capture, hotkeys, config migration, theme switching, playback state, and history.

## Non-goals

- Renaming the product to Vox. The product remains **Read Aloud**.
- Adding Vox’s Dark/Light/System theme model. The theme toggle is **Aurora Compact / Orbit Console**.
- Whisper voice commands, edge-tts, or local LLM modes in the first C# release. They remain deferred to a later pass.
- AI or Natural voice-quality labels backed by online or local neural voices; SAPI voices are labelled `System`.
- Exact pixel copying of the Vox brief or earlier mockups; they define direction, not final art.
- Any cloud account, telemetry, or network requirement in the default configuration.
- Rewriting historical Python source in place; the Python app stays available until the C# build passes verification.
- Word-level text-to-speech seeking. Skip controls are sentence-level because `System.Speech` does not expose word-level seek.

## Constraints

- Windows 10 target. The visual language is Fluent-inspired and native-feeling, but WinUI 3 is not required.
- .NET 8 SDK is not currently installed; install it before implementation.
- Existing users may have `config/settings.json` from the Python app. The C# app reads it once and migrates known keys.
- The default configuration must stay fully offline and privacy-preserving.
- Config format remains plain JSON and human-editable.
- Tests runnable with one command after setup: `dotnet test src-cs/ReadAloud.sln`.
- The C# project lives beside the existing Python source so nothing is lost during migration.
- No unit test touches the real clipboard, real speech device, or real registry.

## Decisions locked from earlier user choices

These are not open questions:

1. Product name is **Read Aloud**.
2. Stack is **C# + WPF on .NET 8**.
3. Themes are **Aurora Compact** and **Orbit Console**, with a Settings toggle; Aurora is default.
4. Capture default is **ClipboardFirst**; legacy `auto` migrates to it and is not shown as a UI mode.
5. Hotkeys are click-to-record in Settings; recording temporarily suppresses global hotkeys.
6. A new read interrupts current speech and discards pending work; Stop cancels speech and clears pending work.
7. Config lives at `%APPDATA%\Read Aloud\settings.json`; legacy Python config migrates once from the installed folder.
8. Tray and close behaviour match the Python app: X exits, Hide to tray hides, tray menu has Show/Read/Stop/Settings/Quit.
9. Whisper voice commands, edge-tts, and local LLM modes are deferred.
10. The Python build remains until the C# build passes verification.

## Approaches considered

### 1. Keep Python/Tkinter and restyle it

Lowest risk and no new toolchain, but both themes and the richer Vox-style layout would be limited by Tk. Rejected because the user chose a full rewrite.

### 2. Keep Python and use CustomTkinter/ttkbootstrap

Better theming with less risk than a rewrite, but still Python and still constrained by the older shell. Rejected because the user chose C# WPF.

### 3. C# WPF core-only rewrite

This was the previously approved plan. It solves capture, hotkeys, themes, speech, config, and packaging. It is now too narrow because the user also wants the Vox feature set documented. Kept as Phase 1 of this design, not as the whole design.

### 4. C# WPF with Aurora/Orbit themes and the Vox feature set, phased — chosen

Native Windows APIs handle hotkeys, clipboard, UIA, and SAPI; XAML ResourceDictionaries handle Aurora/Orbit; the richer UI is split into phases so each phase is testable. This preserves every prior decision while giving the user the full feature map they asked for.

### 5. WinUI 3, Electron, or Tauri

WinUI 3 gives the most modern controls but complicates Windows 10 packaging and self-contained deployment. Electron/Tauri require web or native bridges for SAPI, hotkeys, clipboard, and UIA. Rejected for this offline Windows desktop tool.

## Delivery phases

### Phase 1 — Core reading (first C# release)

Everything needed to replace the Python app: config migration, hotkey recording, capture fix, offline SAPI speech, Aurora/Orbit theme toggle, main window with Home and Settings, tray, activity log, and packaging. This phase is the existing approved plan, updated for the expanded design.

### Phase 2 — Playback and floating mini player

Add a playback state model, Play/Pause/Stop, sentence-level Skip forward/backward, speed control, voice selector, progress bar, estimated remaining time, current-sentence highlight, and a compact floating mini player. The mini player appears for selection-triggered reads by default, supports optional always-on-top, auto-hide after playback, expand, and close.

### Phase 3 — Voice Library and History

Add voice cards for installed SAPI voices, search/filter by language, preview, and quality label `System`. Add optional local-only history with timestamp, duration, Replay, Delete, and retention. History is off by default. Gender presentation is shown when the system provides it, otherwise `Not specified`.

### Phase 4 — Automation and expanded settings

Add auto-read selected text (off by default), Start with Windows, Minimize to tray option, clipboard behavior controls, and privacy settings. Auto-read reuses `SelectionService`; it is opt-in because automatic reading can be noisy.

### Later / not in this design

Whisper voice commands, edge-tts, local LLM modes, Natural/AI voice labels, and word-level seek remain later work. They must not appear as active controls; where discoverability matters, show a disabled placeholder with a “later release” label.

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
      HomeView.xaml
      VoiceLibraryView.xaml
      HistoryView.xaml
      SettingsWindow.xaml
      SettingsWindow.xaml.cs
      MiniPlayerWindow.xaml
      MiniPlayerWindow.xaml.cs
    Themes/
      Aurora.xaml
      Orbit.xaml
    Models/
      AppConfig.cs
      CaptureResult.cs
      CaptureMethod.cs
      HotkeyBinding.cs
      VoiceInfo.cs
      VoiceCard.cs
      VoiceOptions.cs
      PlaybackState.cs
      HistoryEntry.cs
    Services/
      ConfigService.cs
      HotkeyService.cs
      IHotkeyRegistrar.cs
      SelectionService.cs
      IClipboard.cs
      IKeyboardSender.cs
      IUiaSelectionReader.cs
      IClock.cs
      ISpeechEngine.cs
      SapiSpeechEngine.cs
      PlaybackService.cs
      MiniPlayerService.cs
      HistoryService.cs
      ActivityLog.cs
      ReadCoordinator.cs
      TrayService.cs
    ViewModels/
      MainViewModel.cs
      SettingsViewModel.cs
      HomeViewModel.cs
      VoiceLibraryViewModel.cs
      HistoryViewModel.cs
      MiniPlayerViewModel.cs
  ReadAloud.Tests/
    ReadAloud.Tests.csproj
    ConfigTests.cs
    HotkeyTests.cs
    CaptureTests.cs
    SpeechTests.cs
    ThemeTests.cs
    SettingsTests.cs
    ReadCoordinatorTests.cs
    PlaybackTests.cs
    HistoryTests.cs
    TrayTests.cs
```

Files for later phases may be introduced by those phases; they do not need to exist for Phase 1 to pass.

### Configuration

`ConfigService` loads `%APPDATA%\Read Aloud\settings.json`. If absent, it reads the legacy `AppContext.BaseDirectory/config/settings.json`, maps known Python keys, and writes the C# config with `schemaVersion: 1`. Later edits to the legacy file are ignored; if both files exist, the C# file wins.

Base config:

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
  "show_activity": true,
  "playback_speed": 1.0,
  "auto_read_selection": false,
  "mini_player_enabled": true,
  "mini_player_always_on_top": false,
  "mini_player_auto_hide": true,
  "history_enabled": false,
  "history_retention_days": 30,
  "start_with_windows": false,
  "minimize_to_tray": false,
  "clipboard_restore": true
}
```

Unknown Python keys are preserved but ignored. Values are normalised on load: theme must be `aurora` or `orbit`; hotkeys must validate; `playback_speed` clamps to 0.5–3.0; volume clamps to 0–100; `sapi_rate` clamps to -10–10; `max_chars` clamps to 1–1,000,000; retention clamps to 1–365. New fields use the defaults above when absent.

`playback_speed` maps to SAPI `Rate` with `round(10 * log2(speed))`, clamped to -10–10. This gives 0.5x → -10, 1.0x → 0, 2.0x → 10, and caps above 2.0x at 10 because SAPI cannot exceed its native range.

Phase 1 uses `sapi_rate` directly. From Phase 2 onward, `playback_speed` is the single authoritative speed value: if it is absent but `sapi_rate` exists, `ConfigService` derives `playback_speed = clamp(2^(sapi_rate / 10), 0.5, 3.0)` once. `sapi_rate` remains in config for backward compatibility but is no longer edited by Settings.

`offline_voice` stores the SAPI voice `Description` string, matching what the Python app saved through COM `GetDescription()`. Voice Library cards, Settings dropdowns, and migration all use `VoiceInfo.Description` as the identity, compared case-insensitively with fallback to `VoiceInfo.Name`, then the system default.

### Theme system

`App.xaml` merges `Themes/Aurora.xaml` by default. `AppConfig.Theme` selects the dictionary. Settings contains a labelled two-state switch:

- **Aurora Compact** — light, high-contrast, friendly; default.
- **Orbit Console** — dark surface, electric-blue accent, status ring, compact hotkey tiles.

Both themes use Segoe UI Variable with Segoe UI fallback, subtle rounded corners, spacious layout, restrained accents, and minimal borders/shadows. No Dark/Light/System selector is added; the user’s Aurora/Orbit decision wins. Theme changes are applied on the UI dispatcher and persisted immediately.

### Navigation shell

The main window has a left navigation rail with **Home**, **Voice Library**, **History**, and **Settings**.

- Phase 1: Home and Settings are active. Voice Library and History are visible but disabled with a clear “later release” tooltip.
- Phase 2: Home gains the playback panel and mini player controls.
- Phase 3: Voice Library and History become active.
- Settings opens as a window/dialog from the rail and footer.

### Home screen

Home contains:

- Greeting: **Ready to listen?**
- Status: **Ready** or **Listening to your selection**.
- Large manual text input with placeholder: **Paste text here, or select text anywhere on your desktop...**
- Character count.
- Primary button **Listen** (accessible name: **Read selection**).
- Secondary button **Clear**.
- Hotkey summary showing read and stop combinations.
- Capture selector with `ClipboardFirst`, `ClipboardOnly`, `UiaFirst`, `UiaOnly`.
- Activity panel with timestamped capture, speech, and status messages.

Aurora Compact uses a light surface, teal/cyan accent, rounded cards, and clear typography. Orbit Console uses a dark surface, electric-blue accent, status ring, and compact tiles. Neither relies on colour alone: status text and icons carry meaning too.

### Read flow

1. Hotkey, tray Read, or Listen queues a request on the WPF dispatcher; pending work is discarded so the newest read wins.
2. The UI thread snapshots config; no worker touches UI controls.
3. Manual text bypasses capture. Otherwise `SelectionService` captures text.
4. Text is truncated to `max_chars` and logged through `ActivityLog`.
5. Offline SAPI speaks through `System.Speech.Synthesis.SpeechSynthesizer` using `offline_voice`, `sapi_rate`, and `volume`.
6. Stop cancels synthesis immediately and clears pending work.
7. From Phase 2, playback state is published to the Home playback panel and the mini player.

### Capture strategy

`SelectionService.CaptureAsync(CaptureMethod)` returns `CaptureResult` with text, method, and a short diagnostic.

Default `ClipboardFirst`:

1. Snapshot clipboard formats.
2. Wait 150 ms for hotkey modifiers to release, then send clean Ctrl+C with `SendInput` using only Ctrl held.
3. Poll `IClipboard.GetText()` up to 600 ms with condition-based waiting.
4. Restore original clipboard formats.
5. If text exists, return method `clipboard`.
6. If no text and the mode allows it, try `IUiaSelectionReader` with a 500 ms timeout and return method `uia`.
7. Otherwise return empty text and diagnostic `clipboard unchanged; UIA no selection`.

Modes:

- `ClipboardFirst` — default.
- `ClipboardOnly` — never tries UIA.
- `UiaFirst` — preserves old behaviour for apps where UIA works well.
- `UiaOnly` — diagnostic mode for apps that support UIA but not Ctrl+C.

The activity panel shows the method and diagnostic, replacing the silent ~1.4 s timeout.

### Hotkey recording

`HotkeyService` owns display strings, validation, registration, and recorder state.

- `Format()` orders modifiers as Ctrl, Shift, Alt, Win, then the key name.
- Recorder accepts exactly one non-modifier key and rejects modifier-only or duplicate combinations.
- Arming a recorder calls `IHotkeyRegistrar.UnregisterAll()` so the next keypress is recorded instead of firing a read.
- Save/Cancel re-registers hotkeys from saved config.
- Registration uses Win32 `RegisterHotKey` on a hidden WPF message window, not a global low-level hook.
- Startup conflicts skip only the conflicting action, keep the other hotkeys registered, and show an activity message. Config is not silently changed.

### Playback model (Phase 2)

`PlaybackService` owns `PlaybackState`: current text, sentences, current sentence index, playing/paused/stopped status, speed, selected voice, elapsed/estimated remaining time, and progress fraction.

- Play/Pause use `SpeechSynthesizer.Pause()` and `Resume()` where supported.
- Stop calls `SpeakAsyncCancelAll()` and clears current playback state.
- Skip forward/backward moves by sentence and restarts synthesis at the target sentence, then continues through the remaining sentences to the end. It does not speak only the target sentence. This is explicitly sentence-level, not word-level.
- Current-sentence highlight is driven by `SpeakProgress` events when available; otherwise the whole current sentence is highlighted.
- Estimated remaining time is based on word count and current speed; it is an estimate, not a guarantee.

### Floating mini player (Phase 2)

`MiniPlayerWindow` is compact, unobtrusive, and optional.

- Shows when selection-triggered reading starts, unless disabled in Settings.
- Controls: Play/Pause, Stop, speed selector, Expand, Close.
- Progress indicator; optional always-on-top.
- Auto-hide after playback by default; Close hides only the mini player, not the app.
- Expand restores the main window Home view with the playback panel.

### Voice Library (Phase 3)

Voice cards show:

- Name from `SpeechSynthesizer.GetInstalledVoices()`.
- Language from `VoiceInfo.Culture`.
- Gender presentation from `VoiceInfo.Gender`, or `Not specified` when the voice does not report one.
- Preview button that speaks a short sample using the current speed/volume.
- Quality label `System` for all SAPI voices. `Natural` and `AI` remain deferred until non-SAPI voices are added.
- Search box and language filter.

### History (Phase 3)

`HistoryService` stores local-only entries in `%APPDATA%\Read Aloud\history.json` when `history_enabled` is true.

An entry is created when a read is accepted for speech, including manual-input reads, captured reads, and Replay. It stores the exact `max_chars`-truncated text actually spoken, not the original untruncated selection. `duration` is the measured elapsed speech time from speech start to natural completion, Stop, interruption, or skip; it is `0` if speech never started. Each entry has a status of `completed`, `stopped`, or `interrupted`. Replay creates a new entry with source `replay` rather than updating the original.

Each entry contains text, timestamp, duration, status, voice, speed, capture method, and source. The History view shows recently read text with Replay and Delete, plus Delete all. Retention uses `history_retention_days`. History is off by default to preserve privacy; enabling it is an explicit user choice.

### Settings

Settings sections:

- **Appearance** — Aurora Compact / Orbit Console toggle.
- **Hotkeys** — Read and Stop recorder rows; Voice command row shown as disabled “later release”; config key still migrated.
- **Capture** — capture mode and clipboard restore.
- **Voice & playback** — offline voice, SAPI rate, volume, playback speed, max characters.
- **Playback & mini player** — mini player enabled, always-on-top, auto-hide.
- **Automation** — auto-read selected text, Start with Windows, Minimize to tray.
- **Privacy** — history opt-in and retention, activity visibility, log location.

Controls for deferred features are disabled with explanatory text, not hidden behind non-functional toggles.

### Tray and close semantics

Window X exits the app. `Hide to tray` hides the window. Tray menu contains Show window, Read selected text, Stop speaking, Settings…, and Quit. Voice/LLM entries are omitted until those features return. From Phase 4, Minimize to tray changes only the minimize button; X still exits.

### Packaging

Publish with:

```powershell
dotnet publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output goes to `dist-cs/ReadAloud.exe`. Logs and config go to per-user writable directories, not the install folder.

## Author calls made to reconcile the briefs

These are implementation decisions made by the author from prior user choices; the user can correct them while reviewing:

1. **Aurora/Orbit replaces Dark/Light/System.** No third theme mode is added.
2. **Read Aloud keeps its name.** The Vox brief supplies features, not a rename.
3. **Vox features are phased, not abandoned.** All are documented; Phase 1 remains verifiable and small.
4. **Primary Home button is labelled Listen** with accessible name `Read selection`, preserving the Vox copy without losing the established action name.
5. **Playback seeking is sentence-level.** `System.Speech` cannot seek by word.
6. **Mini player is enabled by default for selection reads**, but always-on-top is off and auto-hide is on.
7. **History is off by default** because it stores user text locally.
8. **Auto-read is off by default** because automatic reading can be noisy and was not part of the original trigger model.
9. **SAPI voices are labelled System.** Natural/AI labels wait for real non-SAPI voices.
10. **Start with Windows and Minimize to tray are Phase 4 settings**, with X still exiting.
11. **Skip continues to the end.** Skipping selects a target sentence and then speaks the remaining text; it does not speak only that sentence.
12. **History entries are written when a read is accepted for speech**, store the spoken/truncated text, and record measured duration with a `completed`/`stopped`/`interrupted` status. Replay creates a new entry.
13. **`playback_speed` becomes authoritative in Phase 2.** `sapi_rate` is migrated once into it and kept only for compatibility.
14. **`offline_voice` stores the SAPI `Description`.** This matches the Python COM value and is used consistently across migration, Settings, and Voice Library.

## Verification plan

### Phase 1

- Install .NET 8 SDK and confirm `dotnet --version`.
- `ConfigTests`: migrates Python keys from the installed legacy path; writes `schemaVersion: 1`; ignores later legacy edits; clamps bad numbers; defaults theme to aurora; migrates `auto` to `ClipboardFirst`; persists theme toggle.
- `HotkeyTests`: formats `Ctrl+Alt+R`; accepts `Ctrl+Shift+R`; rejects modifier-only and duplicates; arming unregisters global hotkeys; startup conflict skips only that action.
- `CaptureTests`: clipboard-first returns text when UIA has none; waits 150 ms before Ctrl+C; restores clipboard; logs restore failure without failing capture; UIA fallback runs only when allowed; ClipboardOnly never calls UIA.
- `SpeechTests`: speaks with saved voice/rate/volume; empty text does nothing; Stop clears speaking state.
- `ThemeTests`: switching config theme changes the merged ResourceDictionary and persists; unknown theme falls back to Aurora.
- `ReadCoordinatorTests`: newest read wins; Stop clears pending; manual text bypasses capture; empty capture logs diagnostic and does not speak.
- `TrayTests`: tray commands delegate to Show/Read/Stop/Settings/Quit.
- Manual smoke: select text in Notepad and a browser, press the read hotkey, confirm activity shows `clipboard` or `uia`, confirm speech starts, switch theme, change hotkey, restart and confirm persistence. Re-run the original `no selection after ~1400ms` case.

### Phase 2

- `PlaybackTests`: Play/Pause/Stop transitions; Skip moves by sentence and continues to the end; speed maps to SAPI rate; progress and remaining time update; current-sentence index is published.
- `ConfigTests`: legacy `sapi_rate` derives `playback_speed` once; `playback_speed` wins when both are present; migrated `offline_voice` matches by `Description` with `Name` fallback.
- `MiniPlayerTests`: mini player shows for selection reads; Auto-hide hides after completion; always-on-top setting maps to window topmost; Close hides only the mini player.
- Manual smoke: read a multi-sentence selection, pause/resume, skip forward/back, change speed, expand mini player, stop.

### Phase 3

- `VoiceLibraryTests`: installed voices map to cards; language filter works; preview uses selected voice; gender comes from `VoiceInfo.Gender`; missing culture/gender shows `Not specified`; quality label is `System`; `offline_voice` matches by `Description`.
- `HistoryTests`: history disabled by default; enabled mode stores entries when a read is accepted; stored text is truncated; status/duration are recorded for completed, stopped, and interrupted reads; Replay creates a new entry; delete, delete-all, and retention work; corrupted history file falls back to empty.
- Manual smoke: enable history, read text, replay from History, delete an entry, disable history.

### Phase 4

- `AutomationTests`: auto-read disabled by default; enabled mode reuses capture; Start with Windows setting writes/removes the HKCU Run value; Minimize to tray changes minimize only; X exits.
- Manual smoke: enable auto-read in one app, confirm no read when disabled, toggle Start with Windows and verify registry entry, minimize to tray and quit from tray.

## Risks and mitigations

- **RegisterHotKey conflicts:** show an inline error and keep the previous hotkey.
- **Clipboard restore failures:** log per-format failures but never fail the read.
- **UIA permission differences:** keep explicit capture modes and diagnostics.
- **SAPI voice availability:** fall back to system default if the saved voice is missing.
- **SAPI seek limits:** sentence-level skip and estimate-based progress are explicit in the UI.
- **Auto-read noise:** off by default and can be disabled at any time.
- **History privacy:** off by default, local-only, deletable, retention-limited.
- **Windows 10 vs Fluent:** use Fluent-inspired XAML with native WPF controls; do not require WinUI 3.
- **No .NET SDK yet:** install it after design approval; keep the Python build untouched until C# verification passes.
- **Theme switching while dialogs are open:** apply only on the main dispatcher and close/reopen Settings after a switch if needed.
