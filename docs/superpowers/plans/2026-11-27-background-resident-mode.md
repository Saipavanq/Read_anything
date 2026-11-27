# Background Resident Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Read Aloud resident in the tray after the window is closed and let Windows startup launch it without a visible window.

**Architecture:** One config flag, `run_in_background`, drives both close-to-tray and the hidden flag in the existing HKCU Run entry. A pure argument helper recognizes `--hidden`; `App` skips showing the main window for that launch. `MainWindow` cancels close and hides while the flag is on, leaving tray → Quit as the explicit exit.

**Tech Stack:** C# / WPF / .NET 8, xUnit, Windows registry HKCU Run, WinForms `NotifyIcon`.

**Spec:** [2026-11-27-background-resident-mode-design.md](../specs/2026-11-27-background-resident-mode-design.md)

## Global Constraints

- `run_in_background` defaults to `true` when absent from settings.
- `minimize_to_tray` keeps its current minimize-only meaning; do not change it.
- The hidden launch flag is exactly `--hidden` and is compared case-insensitively.
- The HKCU Run value name is exactly `Read Aloud`.
- Manual launch without `--hidden` must still show the main window.
- Tray menu must keep the exact item `Quit`, and it must call `Application.Shutdown()`.
- Spoken voice commands are not part of this plan; do not add Python, Whisper, or microphone code.
- Use `C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe` for all `dotnet` commands.
- Never drop unknown keys from `settings.json` when saving.

## Review Focus

- Installed `settings.json` files have no `run_in_background` key → must default to resident mode. Test: `ConfigTests.RunInBackground_DefaultsTrue_AndRoundTripsFalse` (Task 1).
- Existing Run entry written by the old build has no `--hidden` → app must re-apply the value on next launch and StartupService must rewrite it. Test: `AutomationTests.BackgroundToggle_RewritesExistingRunValue` (Task 3).
- User launching the exe manually must still see the window even while background mode is on. Test: `AutomationTests.ShouldStartHidden_RecognizesOnlyHiddenFlag` (Task 2).
- Turning background mode off must remove `--hidden`, not merely add a second Run entry. Test: `AutomationTests.BackgroundMode_SaveRewritesStartupValue` (Task 5).
- Tray → Quit must remain a real exit while X hides to tray. Test: existing `TrayTests.TrayCommands_DelegateToExpectedActions` (rerun in Task 5).

---

### Task 1: Config flag

**Files:**
- Modify: `src-cs/ReadAloud/Models/AppConfig.cs`
- Modify: `src-cs/ReadAloud/Services/ConfigService.cs`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`

**Interfaces:**
- Consumes: existing `ConfigService.Load()` / `Save(AppConfig)`.
- Produces: `AppConfig.RunInBackground` (`bool`, default `true`), persisted as `run_in_background`.

- [ ] **Step 1: Write the failing test in `ConfigTests.cs`**

```csharp
[Fact]
public void RunInBackground_DefaultsTrue_AndRoundTripsFalse()
{
    Assert.True(new AppConfig().RunInBackground);
    Assert.True(Service().Load().RunInBackground);

    Write(AppDataPath, """{"run_in_background": false}""");
    Assert.False(Service().Load().RunInBackground);

    var service = Service();
    var config = service.Load();
    config.RunInBackground = false;
    service.Save(config);

    using var doc = JsonDocument.Parse(File.ReadAllText(AppDataPath));
    Assert.False(doc.RootElement.GetProperty("run_in_background").GetBoolean());
    var reloaded = new ConfigService(AppDataPath, LegacyPath).Load();
    Assert.False(reloaded.RunInBackground);
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter RunInBackground_DefaultsTrue_AndRoundTripsFalse`

Expected: FAIL because `AppConfig` has no `RunInBackground`.

- [ ] **Step 3: Add the config property and persistence**

In `AppConfig.cs`, add `public bool RunInBackground { get; set; } = true;` near `StartWithWindows`.

In `ConfigService.cs`:
- add `"run_in_background"` to `KnownKeys`;
- add `writer.WriteBoolean("run_in_background", config.RunInBackground);` immediately after the `start_with_windows` write;
- add `config.RunInBackground = GetBool(root, "run_in_background", config.RunInBackground);` immediately after the `StartWithWindows` apply line.

Do not add normalization for this boolean.

- [ ] **Step 4: Run the test and confirm it passes**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter RunInBackground_DefaultsTrue_AndRoundTripsFalse`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Models/AppConfig.cs src-cs/ReadAloud/Services/ConfigService.cs src-cs/ReadAloud.Tests/ConfigTests.cs
git commit -m "feat(config): add run-in-background setting"
```

---

### Task 2: Hidden launch argument helper

**Files:**
- Create: `src-cs/ReadAloud/Services/StartupArguments.cs`
- Test: `src-cs/ReadAloud.Tests/AutomationTests.cs`

**Interfaces:**
- Consumes: WPF `StartupEventArgs.Args` (`IEnumerable<string>`).
- Produces: `static bool StartupArguments.ShouldStartHidden(IEnumerable<string> args)`.

- [ ] **Step 1: Write the failing tests in `AutomationTests.cs`**

```csharp
[Theory]
[InlineData(new[] { "--hidden" }, true)]
[InlineData(new[] { "--HIDDEN" }, true)]
[InlineData(new[] { "other", "--hidden" }, true)]
[InlineData(new string[0], false)]
[InlineData(new[] { "--visible" }, false)]
public void ShouldStartHidden_RecognizesOnlyHiddenFlag(string[] args, bool expected)
{
    Assert.Equal(expected, StartupArguments.ShouldStartHidden(args));
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter ShouldStartHidden_RecognizesOnlyHiddenFlag`

Expected: FAIL because `StartupArguments` does not exist.

- [ ] **Step 3: Implement the helper**

Create `StartupArguments.cs` in namespace `ReadAloud.Services` with:

```csharp
public static bool ShouldStartHidden(IEnumerable<string> args)
    => args.Any(arg => string.Equals(arg, "--hidden", StringComparison.OrdinalIgnoreCase));
```

Add `using System.Linq;` and guard `args` against `null` by returning `false`.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter ShouldStartHidden_RecognizesOnlyHiddenFlag`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Services/StartupArguments.cs src-cs/ReadAloud.Tests/AutomationTests.cs
git commit -m "feat(startup): add hidden launch flag parser"
```

---

### Task 3: Startup registration in background mode

**Files:**
- Modify: `src-cs/ReadAloud/Services/StartupService.cs`
- Test: `src-cs/ReadAloud.Tests/AutomationTests.cs`

**Interfaces:**
- Consumes: `StartupArguments` flag name from Task 2 (string value only, no method call needed).
- Produces: `void StartupService.SetStartWithWindows(bool enabled, bool runInBackground = false)` and `bool IsStartWithWindowsEnabled`.

- [ ] **Step 1: Replace/extend the startup tests in `AutomationTests.cs`**

Replace `StartWithWindows_WritesRunValue` with these three tests:

```csharp
[Fact]
public void StartWithWindows_Background_WritesHiddenRunValue()
{
    var registry = new FakeRegistryAccessor();
    var startup = new StartupService(registry);

    startup.SetStartWithWindows(true, runInBackground: true);

    Assert.Contains("--hidden", registry.Value);
    Assert.True(startup.IsStartWithWindowsEnabled);
}

[Fact]
public void StartWithWindows_NoBackground_WritesPlainRunValue()
{
    var registry = new FakeRegistryAccessor();
    var startup = new StartupService(registry);

    startup.SetStartWithWindows(true, runInBackground: false);

    Assert.DoesNotContain("--hidden", registry.Value!);
    Assert.True(startup.IsStartWithWindowsEnabled);
}

[Fact]
public void BackgroundToggle_RewritesExistingRunValue()
{
    var registry = new FakeRegistryAccessor();
    var startup = new StartupService(registry);
    startup.SetStartWithWindows(true, runInBackground: false);

    startup.SetStartWithWindows(true, runInBackground: true);
    Assert.Contains("--hidden", registry.Value);

    startup.SetStartWithWindows(true, runInBackground: false);
    Assert.DoesNotContain("--hidden", registry.Value!);
}
```

Keep `DisablingStartWithWindows_DeletesRunValue`; it still compiles because `runInBackground` is optional.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter "StartWithWindows_Background_WritesHiddenRunValue|StartWithWindows_NoBackground_WritesPlainRunValue|BackgroundToggle_RewritesExistingRunValue"`

Expected: FAIL because the overload does not exist.

- [ ] **Step 3: Implement `StartupService`**

Change `RunCommand` to `private static string RunCommand(bool runInBackground)`:
- build the quoted executable path exactly as today;
- append `" --hidden"` only when `runInBackground` is `true`.

Change `IsStartWithWindowsEnabled` to return `true` when the registry value equals either `RunCommand(false)` or `RunCommand(true)` using `StringComparison.OrdinalIgnoreCase`.

Change `SetStartWithWindows` to `SetStartWithWindows(bool enabled, bool runInBackground = false)`; when enabled, write `RunCommand(runInBackground)`, otherwise delete the value.

- [ ] **Step 4: Run the full automation test file and confirm it passes**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter FullyQualifiedName~AutomationTests`

Expected: PASS, including the unchanged `DisablingStartWithWindows_DeletesRunValue`.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Services/StartupService.cs src-cs/ReadAloud.Tests/AutomationTests.cs
git commit -m "feat(startup): write hidden flag when background mode is on"
```

---

### Task 4: Close-to-tray window behavior

**Files:**
- Modify: `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Test: `src-cs/ReadAloud.Tests/AutomationTests.cs`

**Interfaces:**
- Consumes: `AppConfig.RunInBackground` from Task 1.
- Produces: `MainWindow(MainViewModel main, Action openSettings, Func<bool> minimizeToTray, Func<bool> runInBackground)` and `static bool MainWindow.ShouldExitOnClose(bool runInBackground)`.

- [ ] **Step 1: Update the window preference test in `AutomationTests.cs`**

Replace the close assertion inside `MinimizeToTray_ChangesMinimizeOnly` with a separate test:

```csharp
[Fact]
public void MinimizeToTray_ChangesMinimizeOnly()
{
    Assert.True(MainWindow.ShouldHideOnMinimize(WindowState.Minimized, minimizeToTray: true));
    Assert.False(MainWindow.ShouldHideOnMinimize(WindowState.Normal, minimizeToTray: true));
}

[Fact]
public void RunInBackground_ChangesCloseOnly()
{
    Assert.False(MainWindow.ShouldExitOnClose(runInBackground: true));
    Assert.True(MainWindow.ShouldExitOnClose(runInBackground: false));
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter "MinimizeToTray_ChangesMinimizeOnly|RunInBackground_ChangesCloseOnly"`

Expected: FAIL because `ShouldExitOnClose` still takes `minimizeToTray` and returns `true`.

- [ ] **Step 3: Change `MainWindow`**

Add `private readonly Func<bool> _runInBackground;`.

Change the constructor signature to the one in Interfaces and assign both callbacks.

Change `ShouldExitOnClose(bool runInBackground)` to `return !runInBackground;`.

Change `OnClosing` to call `ShouldExitOnClose(_runInBackground())`. Leave the cancel-and-`Hide()` body and `ShouldHideOnMinimize` untouched.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter "MinimizeToTray_ChangesMinimizeOnly|RunInBackground_ChangesCloseOnly"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Views/MainWindow.xaml.cs src-cs/ReadAloud.Tests/AutomationTests.cs
git commit -m "feat(window): hide to tray instead of exiting when background mode is on"
```

---

### Task 5: Settings, app wiring, and documentation

**Files:**
- Modify: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Modify: `src-cs/ReadAloud/Views/SettingsWindow.xaml`
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Modify: `src-cs/ReadAloud.Tests/AutomationTests.cs`
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md`
- Modify: `docs/superpowers/specs/2026-11-27-background-resident-mode-design.md` (Status: Accepted)

**Interfaces:**
- Consumes: all interfaces produced in Tasks 1–4.
- Produces: `SettingsViewModel.RunInBackground` (`bool`); hidden startup behavior in `App.OnStartup`.

- [ ] **Step 1: Write the failing settings test in `AutomationTests.cs`**

```csharp
[Fact]
public void BackgroundMode_SaveRewritesStartupValue()
{
    using var h = new SettingsHarness();

    Assert.True(h.ViewModel.RunInBackground);

    h.ViewModel.StartWithWindows = true;
    h.ViewModel.RunInBackground = false;
    h.ViewModel.Save();
    Assert.DoesNotContain("--hidden", h.Registry.Value!);

    h.ViewModel.RunInBackground = true;
    h.ViewModel.Save();
    Assert.Contains("--hidden", h.Registry.Value!);
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter BackgroundMode_SaveRewritesStartupValue`

Expected: FAIL because `SettingsViewModel` has no `RunInBackground`.

- [ ] **Step 3: Wire `SettingsViewModel`**

In `SettingsViewModel.cs`:
- add field `private bool _runInBackground = true;`;
- load `_runInBackground = current.RunInBackground;` in the constructor;
- add a simple get/set `public bool RunInBackground` property next to `StartWithWindows`;
- set `config.RunInBackground = RunInBackground;` in `ApplyTo`;
- change `ApplyStartupSettings()` to `_startup?.SetStartWithWindows(StartWithWindows, RunInBackground);`.

Do not change theme or hotkey save behavior.

- [ ] **Step 4: Add the settings checkbox**

In `SettingsWindow.xaml`, inside the Automation `StackPanel`, insert immediately before the `Start with Windows` CheckBox:

```xml
<CheckBox Content="Run in background (keep hotkeys active when closed)"
          IsChecked="{Binding RunInBackground}"
          ToolTip="Closing the window hides it to the tray instead of exiting. With Start with Windows, the app launches silently. Use tray → Quit to exit." />
```

Keep the existing `Start with Windows` and `Minimize to tray` rows unchanged.

- [ ] **Step 5: Wire `App` startup and close**

In `App.xaml.cs`:
- change the startup registration call to `_startup.SetStartWithWindows(config.StartWithWindows, config.RunInBackground);`;
- compute `var startHidden = StartupArguments.ShouldStartHidden(e.Args);` after config load;
- pass a fourth constructor argument `() => _config!.Load().RunInBackground` to `MainWindow`;
- replace `_window.Show();` with `if (!startHidden) { ShowMainWindow(); }`.

Leave tray creation, hotkey registration, and `Quit()` unchanged.

- [ ] **Step 6: Run focused tests and confirm they pass**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj --filter "BackgroundMode_SaveRewritesStartupValue|TrayCommands_DelegateToExpectedActions"`

Expected: PASS. The tray test confirms `Quit` is still present and delegated.

- [ ] **Step 7: Update documentation**

In `README.md`, replace the sentence saying X exits the app with:

`Closing the window hides the app to the tray when Run in background is enabled (the default); use tray → Quit to exit.`

In `docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md`, update the window-behavior and Phase 4 test lines that say X exits so they say X hides to tray while `run_in_background` is on, and that `tray → Quit` exits.

In `docs/superpowers/specs/2026-11-27-background-resident-mode-design.md`, change `Status: Proposed` to `Status: Accepted`.

- [ ] **Step 8: Run the full suite**

Run: `& "C:\Users\malla\AppData\Local\Microsoft\dotnet\dotnet.exe" test src-cs\ReadAloud.Tests\ReadAloud.Tests.csproj`

Expected: all tests PASS, with zero failures.

- [ ] **Step 9: Commit**

```powershell
git add src-cs/ReadAloud/ViewModels/SettingsViewModel.cs src-cs/ReadAloud/Views/SettingsWindow.xaml src-cs/ReadAloud/App.xaml.cs src-cs/ReadAloud.Tests/AutomationTests.cs README.md docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md docs/superpowers/specs/2026-11-27-background-resident-mode-design.md
git commit -m "feat(app): keep Read Aloud resident in the tray"
```

---

## Final verification (after all tasks)

1. Run the full suite with the pinned dotnet path and confirm zero failures.
2. Publish and install the rebuilt `ReadAloud.exe`.
3. Open Settings → Automation and confirm the new checkbox is checked by default.
4. Enable **Start with Windows**, sign out/in, and confirm no main window appears while the tray icon is visible.
5. Select text in Notepad, press `Ctrl+Alt+R`, confirm speech; press `Ctrl+Alt+X`, confirm stop.
6. Press X on the main window, repeat the hotkey test, then use tray → Quit and confirm the process exits.
