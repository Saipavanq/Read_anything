# Read Aloud — Icon Refresh and Linux Companion Design

## Status

Proposed

## Date

2026-11-22

## Intent

Read Aloud v1.0.0 is a Windows-only WPF app. It has no designed icons: the tray uses the Windows stock `SystemIcons.Application`, no window sets an `Icon`, and every action is a text-only button. The user wants two things:

1. A real, coherent icon system that makes the app look intentionally designed in both Aurora Compact and Orbit Console themes.
2. A Linux version of the application, using approaches generally recommended by web and application developers and the project's available UI/architecture/documentation skills.

This design keeps the shipped Windows app stable while adding a genuine Linux desktop app. It does not rewrite the Windows shell.

## Goals

- Replace placeholder iconography with a single, documented visual language shared by Windows and Linux.
- Provide real window, taskbar, and tray icons on both platforms.
- Keep every icon accessible: icons supplement visible text labels and expose automation names; no icon is the sole carrier of meaning.
- Extract the portable parts of the current C# code into a `net8.0` core library so Windows and Linux share domain behavior and tests.
- Deliver a Linux Avalonia app with: visible window, read/stop hotkeys, selected-text capture, offline speech, history, settings, themes, and a tray menu.
- Keep Windows behavior and the v1.0.0 release untouched except for icon resources and the core-library extraction needed for sharing.
- Preserve offline-first operation: no network calls, telemetry, cloud TTS, or cloud LLM.

## Non-goals

- Replacing WPF on Windows with Avalonia in this pass.
- Reimplementing Windows `RegisterHotKey`, `SetWindowsHookEx`, UI Automation, SAPI, or WinForms clipboard behavior on Linux with Windows semantics.
- Guaranteeing selected-text capture on stock GNOME under Wayland. GNOME does not implement the `ext-data-control` protocol, so another application's selection cannot be read there. X11 and KDE/wlroots Wayland are supported targets.
- Voice commands (`whisper`), edge-tts, local LLM modes, or changing the product name.
- Releasing a new version tag; this work lands after v1.0.0 on `main`.

## Anti-goals

- A generic “AI” icon look: purple/indigo gradients, oversized rounded cards, inconsistent stroke weights, or decorative shadows.
- Icon-only controls with no accessible name.
- A Linux port that silently reuses Windows-only packages or P/Invoke.
- Copying another product's branding or exact visual design.

## Constraints

- Current app: `net8.0-windows`, WPF + WinForms, `System.Speech` 8.0.0, `win-x64` single-file publish.
- Windows-specific services are listed in the 2026-11-22 recon report: tray (`NotifyIcon`), clipboard (`WinFormsClipboard`), key injection (`SendKeys`), hotkeys (`HwndSource`/`RegisterHotKey`), mouse hook (`SetWindowsHookEx`), UI Automation, registry autostart, foreground-window P/Invoke, and SAPI.
- Reusable seams already exist: `ISpeechEngine`, `ISpeechSynthesizer`, `IClipboard`, `IKeyboardSender`, `IUiaSelectionReader`, `IMouseHook`, `IAutoReadTrigger`, `IHotkeyRegistrar`, `IRegistryAccessor`, `IMiniPlayerView`, `IThemeService`, `IClock`, `IHistorySink`.
- Missing seams that must be added: `ITrayIcon`, `IDispatcher`, `IPaths`, `IForegroundWindow`, and a platform-neutral key type for hotkeys.
- Tests must stay hermetic: no real clipboard, registry, speech device, mouse hook, or background hook.
- Avalonia on Linux targets X11 by default and supports a native Wayland backend from 12.1; global shortcuts on Wayland require the XDG GlobalShortcuts portal and compositor approval. Tray icons require `StatusNotifierItem`/AppIndicator support. speech-dispatcher with espeak-ng or Piper is the recommended offline speech stack.
- Linux system packages are acceptable runtime prerequisites. The app detects missing packages at startup, shows a clear diagnostic, and degrades to the features that do not need them. It does not bundle speech engines, X utilities, or a display server. README lists the distro packages.
- Verification environment: the Windows host has the .NET 8 SDK; WSL Ubuntu is available but has no .NET SDK. Linux verification in this pass is cross-publish of `linux-x64` from Windows, portable-core unit tests, and an optional WSL process-start smoke after installing the SDK there. GUI, audio, global-hotkey, and cross-app selection behavior require a real Linux desktop and will be delivered with a manual verification checklist, not claimed as automatically verified.

## Approaches

### A. Windows-first + Avalonia Linux companion (chosen)

Keep `ReadAloud` (WPF) as the Windows app. Move portable models, view models, and domain services into `ReadAloud.Core` targeting `net8.0`. Add `ReadAloud.Linux` as an Avalonia app that references the core and supplies Linux adapters. Keep Windows adapters in the WPF project.

- Pros: protects the shipped Windows app and its verified behavior; each platform uses native APIs; Linux can be developed and tested independently.
- Cons: two UI XAML dialects; some presentation code must be duplicated; Linux feature set is naturally narrower on Wayland.

### B. Full Avalonia rewrite for both OSes

Replace WPF with Avalonia everywhere and use one UI codebase.

- Pros: one UI, one icon implementation, one theme engine.
- Cons: highest risk and effort; reimplements the Windows shell, hotkey registration, tray, and capture path that were just stabilized; would invalidate the current packaged smoke evidence.

### C. Linux headless companion only

Ship a CLI/tray helper that reads X11 PRIMARY selection and speaks with speech-dispatcher.

- Pros: fastest and smallest change.
- Cons: no Linux window, no icon system, no shared UI; does not meet “application for Linux” with the same product feel.

Web/Electron was considered and rejected: it adds a browser runtime, weakens offline-first guarantees, and does not improve either existing desktop target.

## Decisions

### Architecture and seams

1. Create `src-cs/ReadAloud.Core` (`net8.0`) containing portable models, view models, and domain services that do not require WPF, WinForms, SAPI, or registry P/Invoke.
2. Keep Windows adapters in `src-cs/ReadAloud`: `SapiSpeechEngine`, `Win32HotkeyRegistrar`, `Win32MouseHook`, `WinFormsClipboard`, `KeyboardSender`, `UiaSelectionReader`, `RegistryAccessor`, `TrayIcon`, `OwnWindowForegroundPolicy`, and `MiniPlayerWindow`.
3. Add Linux adapters in `src-cs/ReadAloud.Linux`:
   - `LinuxSpeechSynthesizer` implementing `ISpeechSynthesizer`, speaking through speech-dispatcher (`spd-say`/`spd-send`) with espeak-ng/Piper as the configured backend.
   - `X11SelectionReader` implementing `IUiaSelectionReader`, reading PRIMARY first and CLIPBOARD second via X11 selection APIs or `xclip`/`xsel` helpers.
   - `X11KeyboardSender` implementing `IKeyboardSender`, using XTest where available.
   - `PortalHotkeyRegistrar` / `X11HotkeyRegistrar` implementing `IHotkeyRegistrar`, using XDG GlobalShortcuts on Wayland and `XGrabKey` on X11.
   - `XdgPaths` implementing `IPaths`, storing config/history under `~/.config/Read Aloud` and `~/.local/share/Read Aloud` with `XDG_CONFIG_HOME`/`XDG_DATA_HOME` overrides.
   - `FreedesktopStartup` implementing a new `IStartupService` seam via `~/.config/autostart/read-aloud.desktop`.
   - `AvaloniaTrayIcon` implementing a new `ITrayIcon` seam using `TrayIcon`/`NativeMenu`.
   - `AvaloniaDispatcher` implementing a new `IDispatcher` seam.
   - `LinuxForegroundWindow` implementing `IForegroundWindow` for focus checks; no low-level mouse-event adapter is added in this pass.
4. Introduce a platform-neutral `KeyCode`/`ModifierCode` pair for hotkeys. WPF/Win32 adapters map to and from `System.Windows.Input.Key`/`ModifierKeys`; Avalonia adapters map to/from Avalonia `Key`/`KeyModifiers`. `HotkeyBinding`, `HotkeyService`, and Settings UI use the neutral types.
5. Generalize `IClipboard.Snapshot()` to a platform-neutral `ClipboardSnapshot` object with text plus opaque format payloads, so WinForms numeric format IDs stay inside the Windows adapter.
6. Keep the existing `Action<Action>` dispatcher calls only until `IDispatcher` is introduced; thereafter view models depend on `IDispatcher`.
7. Keep the default capture mode `ClipboardFirst` on Windows. On Linux the equivalent default is `PrimaryFirst`: read PRIMARY without touching CLIPBOARD, then fall back to CLIPBOARD with restore. This is the one intentional platform difference in capture defaults.
8. Keep speech semantics behind `ISpeechEngine`: SAPI rate mapping stays in the Windows adapter; Linux maps app speed to speech-dispatcher rate and voice parameters. Voice descriptions from each backend are stored in the same `OfflineVoice` model.

### Icon system

1. Define a single 24×24 icon grid with a 1.75 px stroke, round caps/joins, and no filled backgrounds. Icons are drawn as vector path data, not emoji or font glyphs.
2. Ship a shared icon catalog as C# constants or XAML resource dictionaries containing path data for: `read/listen`, `stop`, `play`, `pause`, `previous`, `next`, `home`, `library`, `history`, `settings`, `hide`, `show`, `expand`, `close`, `delete`, `clear`, `replay`, `record`, `preview`, and `app-mark`.
3. The `app-mark` is a stylized **black mouth** on a white rounded-square tile, based on the user-approved reference and follow-up direction. It replaces the earlier open-book proposal. The white tile keeps the black mouth visible on dark taskbars and light trays, so the same asset works as application logo, window icon, tray icon, taskbar icon, Desktop icon, and Start Menu icon. It is geometric and high-contrast, with no gradient. Theme accent colors apply to action icons, not to the brand mark.
4. Generate real assets from the same vector source:
   - Windows: multi-size `Assets/app-icon.ico` (16/24/32/48/64/128/256) and set `ApplicationIcon`, `Window.Icon`, and `NotifyIcon.Icon`. Copy the same ICO beside the installed exe and point Desktop/Start Menu/taskbar shortcuts at it so every shell surface uses one logo.
   - Linux: `Assets/app-icon.png` (16/24/32/48/128/256) and an SVG source for packaging/desktop entry.
5. Add a small theme brush set for icons: `IconBrush`, `IconMutedBrush`, `IconAccentBrush`, `IconOnAccentBrush`. Aurora and Orbit define these using existing palette values. Icon color never encodes state by itself; text and automation names remain.
6. Icons are paired with the existing text labels in navigation, playback, history, library, settings, and tray. Remove no labels. Add `AutomationProperties.Name` to icon-only buttons in the mini player and tray-equivalent controls.
7. Use consistent sizes: 16 px in dense rows and tray menus, 20 px in navigation and secondary actions, 24 px in primary playback controls. No arbitrary sizes.
8. Accessibility: all interactive icon buttons are keyboard reachable, have visible focus, and expose names. Icon rendering respects high-contrast themes through system brushes where available.

### Linux feature scope

- Must work: window with Home/History/Voice Library/Settings, read selection via hotkey, stop via hotkey and button, voice selection, speed, theme toggle, history, config persistence, tray show/read/stop/settings/quit.
- Should work if the desktop supports it: click-to-record hotkeys (Avalonia key events in the Settings recorder, no X11 input-event adapter), hide-to-tray, and mini player.
- Windows-only in this pass: opt-in auto-read from drag/double-click mouse gestures. It is off by default on Windows and needs a native pointer-event adapter on Linux, so it is explicitly deferred rather than faked.
- Explicitly unavailable: GNOME/Wayland cross-app selection capture; the app shows a clear diagnostic and offers clipboard fallback instead of failing silently.
- Packaging: `build-linux.sh` publishes `linux-x64` self-contained Avalonia app with `Assets/` and a `.desktop` file. No claim of a universal AppImage/Flatpak in this pass.

### Documentation

- Write ADR `docs/adr/0002-cross-platform-ui-and-icon-system.md` matching the existing convention in `docs/adr/0001-main-thread-settings-snapshot.md`: `# ADR 0002 — Title`, then `Date`, `Status`, `Context`, `Decision`, `Alternatives considered`, `Consequences`, `Verification`. Record why WPF was kept on Windows and why Avalonia + Linux adapters were chosen.
- Update `README.md` so it describes the C# Windows app and Linux companion rather than the stale Python instructions. Include quick start, commands, architecture, and Linux limitations.
- Keep this design as the source of scope; the implementation plan will reference it.

## Builder check

A fresh reviewer subagent will read this document before approval and flag only questions that cannot be settled from the conversation or the code. Minor choices are recorded as calls below.

## Calls made by the designer

- Use Avalonia for Linux because it is the mainstream .NET desktop option with X11/Wayland support and a native tray abstraction.
- Use speech-dispatcher rather than calling espeak-ng directly, because it handles Unicode, voice selection, and shared audio behavior better.
- Use PRIMARY-first capture on Linux because it avoids clobbering the clipboard and is the conventional Linux equivalent of Windows selection capture.
- Keep text labels alongside icons rather than replacing them, to avoid accessibility regressions.
- Target `linux-x64` first; ARM64 can follow when there is a device to test on.
- Do not create a GitHub Release or new version tag as part of this work.
- Require Linux runtime packages instead of bundling them; detect and degrade gracefully when absent.
- Keep mouse-gesture auto-read Windows-only in this pass; Linux gets manual read/stop hotkeys.
- User approved the black-mouth mark on 2026-11-22; it is the `app-mark` and is wired into the Windows exe, windows, and tray icon.
- Verify by cross-publishing from Windows plus hermetic tests; treat real desktop behavior as a manual checklist.

## What is left to the builder

- Exact package versions and project file layout, chosen to match the repository's .NET 8 baseline.
- Exact vector path data and asset generation tooling, chosen so no new dependency is added unless the repository already has one.
- Detailed task breakdown, test plan, and verification commands in the implementation plan created after this design is approved.
