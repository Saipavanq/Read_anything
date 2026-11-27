# Background resident mode design

Date: 2026-11-27
Status: Accepted

## Problem

The X button always shuts Read Aloud down: `MainWindow.ShouldExitOnClose()` returns `true` unconditionally. `Minimize to tray` only changes what the minimize button does, so closing the window still kills the global hotkeys and tray actions. `Start with Windows` writes a Run entry, but the command has no hidden flag, so it pops the full window up at every sign-in.

The user selected **Option A**: make the existing keyboard/tray shell resident in the background. This does not add microphone or spoken-command recognition; the `Ctrl+Alt+V` voice-command placeholder remains a later-release feature.

## Goals

1. Closing the main window keeps the process alive, with the tray icon and global read/stop hotkeys still working.
2. `Start with Windows` can launch the app with no visible window.
3. There is still an obvious way to exit: **tray → Quit**.
4. The change is opt-out through one clear setting and does not silently alter the meaning of `Minimize to tray`.

## Non-goals

- Microphone capture, Whisper, or spoken commands such as “read this”.
- Running while Windows is shut down, rebooting, or the user is signed out. This is a per-user desktop app, not a Windows service.
- Changing the existing auto-read behavior or the mini player.

## Decision

Add one config flag:

```json
"run_in_background": true
```

Default: `true`. This makes the installed app behave as requested without requiring the user to discover a hidden option; setting it to `false` restores the old close-to-exit behavior.

### Behavior

| Action | `run_in_background: true` | `run_in_background: false` |
| --- | --- | --- |
| X / window close | Cancel close, hide window, keep tray + hotkeys | Exit app (legacy behavior) |
| Minimize button | Controlled by existing `Minimize to tray` | Controlled by existing `Minimize to tray` |
| Tray → Quit | Always exits | Always exits |
| `Start with Windows` enabled | Run value includes `--hidden` | Run value has no flag |
| Manual double-click launch | Shows the window normally | Shows the window normally |

Only the Run entry uses `--hidden`. That way the user still sees the main window when they deliberately open the app, but Windows startup does not pop it up.

### Config and settings

- `AppConfig.RunInBackground` persisted as `run_in_background`.
- `ConfigService` adds the key to `KnownKeys`, `Save`, `Apply`, and a test that a missing key defaults to `true`.
- Settings → Automation gains one checkbox above `Start with Windows`:

  `Run in background (keep hotkeys active when closed)`

  Tooltip: `Closing the window hides it to the tray instead of exiting. With Start with Windows, the app launches silently. Use tray → Quit to exit.`

- `Minimize to tray` keeps its current label and minimize-only semantics.

### Startup registration

`StartupService` gains the background mode in its API:

```csharp
void SetStartWithWindows(bool enabled, bool runInBackground);
bool IsStartWithWindowsEnabled { get; }
```

- Enabled + background: `"<exe path>" --hidden`
- Enabled + no background: `"<exe path>"`
- Disabled: delete the HKCU Run value.
- `IsStartWithWindowsEnabled` accepts either command form so toggling background mode does not look like startup was disabled.
- `App.OnStartup` re-applies the value from config on every launch, which also refreshes the path if the executable moved.

### App startup and close

- Add a small pure helper `StartupArguments.ShouldStartHidden(IEnumerable<string> args)` so hidden-launch parsing can be unit tested.
- `App.OnStartup` checks `e.Args` with that helper. When true, it assigns `MainWindow` but does **not** call `_window.Show()`; the tray icon is already created before this point.
- `MainWindow` takes `Func<bool> runInBackground` instead of the current `Func<bool> minimizeToTray` for close decisions, while keeping the separate minimize callback.
- `ShouldExitOnClose(bool runInBackground) => !runInBackground`.
- `ShowMainWindow()` remains the way to restore the window from the tray or mini player.

### State flow

```text
Windows sign-in
  -> Run entry: ReadAloud.exe --hidden
  -> App registers hotkeys + tray icon, creates hidden MainWindow
  -> Ctrl+Alt+R / tray reads selection; Ctrl+Alt+X / tray stops

User presses X
  -> OnClosing sees run_in_background
  -> e.Cancel = true; Hide()
  -> same resident state as hidden startup

User chooses tray -> Quit
  -> Application.Shutdown()
  -> hooks, hotkeys, and tray icon disposed
```

## Files expected to change

- `src-cs/ReadAloud/Models/AppConfig.cs`
- `src-cs/ReadAloud/Services/ConfigService.cs`
- `src-cs/ReadAloud/Services/StartupService.cs`
- `src-cs/ReadAloud/Services/StartupArguments.cs` (new)
- `src-cs/ReadAloud/App.xaml.cs`
- `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- `src-cs/ReadAloud/Views/SettingsWindow.xaml`
- `src-cs/ReadAloud.Tests/ConfigTests.cs`
- `src-cs/ReadAloud.Tests/AutomationTests.cs`
- `src-cs/ReadAloud.Tests/SettingsTests.cs`
- `README.md` and `docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md` (supersede old “X exits” wording)

## Tests

- Config: default `run_in_background` is `true`; round-trip persists `false`; unknown keys survive.
- `StartupArguments`: `--hidden` and `--HIDDEN` are recognized; ordinary args are not.
- `StartupService`: background enabled writes the `--hidden` Run value; background disabled writes plain path; disabling deletes it; `IsStartWithWindowsEnabled` is true in both enabled forms.
- `MainWindow`: `ShouldExitOnClose(true)` is false, `ShouldExitOnClose(false)` is true; `ShouldHideOnMinimize` behavior unchanged.
- Settings: `RunInBackground` is loaded, saved, and passed to `StartupService`.
- Full C# test suite must remain green.

## Verification

1. Run `dotnet test` with the installed SDK path used in this repo.
2. Publish and install the rebuilt executable.
3. Enable **Start with Windows** and **Run in background**, sign out/in or run the Run command manually, and confirm no visible window while the tray icon and hotkeys work.
4. Select text in Notepad, press `Ctrl+Alt+R`, confirm speech; press `Ctrl+Alt+X`, confirm stop.
5. Press X on the main window, repeat the hotkey test, then use tray → Quit and confirm the process actually exits.
