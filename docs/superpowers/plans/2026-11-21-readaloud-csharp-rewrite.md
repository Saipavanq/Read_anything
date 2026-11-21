# Read Aloud C# WPF Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Python/Tkinter app with a C# WPF .NET 8 desktop app that reliably reads selected text, exposes click-to-record hotkeys, and switches between Aurora Compact and Orbit Console themes.

**Architecture:** WPF MVVM shell with `App.xaml` ResourceDictionary theme switching, a dispatcher-owned `ReadCoordinator`, and injectable OS seams for clipboard, UIA, keyboard injection, speech, and hotkey registration. The read worker never touches UI controls; capture results and activity events are surfaced through view models.

**Tech Stack:** Windows 10, C# 12, .NET 8, WPF, `System.Speech.Synthesis`, `System.Windows.Automation`, Win32 `RegisterHotKey`/`SendInput`, xUnit, MSIX-free self-contained `dotnet publish`.

**Spec:** `docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md`

## Global Constraints

- Windows 10 only.
- .NET 8 SDK is not installed; install it before implementation.
- Default configuration must remain fully offline.
- Config is JSON; legacy Python `config/settings.json` migrates once.
- Themes are `aurora` and `orbit`; Aurora is default.
- Capture methods are `ClipboardFirst`, `ClipboardOnly`, `UiaFirst`, `UiaOnly`; legacy `auto` maps to `ClipboardFirst`.
- Newest read wins; Stop cancels speech and clears pending requests.
- No Python runtime required by the delivered app.
- Tests use stdlib-style fakes through interfaces; no test may touch the real clipboard.
- One test command after setup: `dotnet test src-cs/ReadAloud.sln`.

## Review Focus

- **Text selected in an app that exposes no UIA selection** → capture succeeds through clipboard and the activity panel says `clipboard`. Test: `tests/CaptureTests.cs::ClipboardFirst_ReturnsText_WhenUiaHasNothing` in Task 3.
- **Clipboard restore fails for one format** → read still succeeds and the failure is logged. Test: `tests/CaptureTests.cs::Capture_RestoreFailure_IsLogged_ButTextIsReturned` in Task 3.
- **Hotkey already owned by another app or duplicated in config** → app keeps running, skips only that action, shows a clear message. Tests: `tests/HotkeyTests.cs::StartupConflict_SkipsOnlyConflictingAction` and `DuplicateRecord_IsRejected` in Task 2.
- **Corrupt legacy config or legacy `auto` capture value** → app starts with safe defaults and migrates `auto` to `ClipboardFirst`. Tests: `tests/ConfigTests.cs::CorruptLegacyConfig_UsesDefaults` and `AutoCapture_MigratesToClipboardFirst` in Task 1.
- **New read while speaking or Stop during capture** → no stale speech is heard and the newest request wins. Test: `tests/ReadCoordinatorTests.cs::NewestReadWins_AndStopClearsPending` in Task 7.

---

### Task 0: Toolchain and solution scaffold

**Files:**
- Create: `src-cs/ReadAloud.sln`
- Create: `src-cs/ReadAloud/ReadAloud.csproj`
- Create: `src-cs/ReadAloud/App.xaml`, `src-cs/ReadAloud/App.xaml.cs`
- Create: `src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: a building WPF app project and an xUnit test project referenced by the solution.

- [ ] **Step 1: Install the .NET 8 SDK**

Run: `winget install --id Microsoft.DotNet.SDK.8 --exact --accept-source-agreements --accept-package-agreements`

Expected: installer completes. If `winget` is unavailable, install the SDK from `https://dotnet.microsoft.com/download/dotnet/8.0`.

- [ ] **Step 2: Verify the SDK**

Run: `dotnet --version`

Expected: output starts with `8.`.

- [ ] **Step 3: Create solution and projects**

Run:
```powershell
dotnet new sln -n ReadAloud -o src-cs
dotnet new wpf -n ReadAloud -o src-cs/ReadAloud -f net8.0
dotnet new xunit -n ReadAloud.Tests -o src-cs/ReadAloud.Tests -f net8.0
dotnet sln src-cs/ReadAloud.sln add src-cs/ReadAloud/ReadAloud.csproj src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj
dotnet add src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj reference src-cs/ReadAloud/ReadAloud.csproj
```

Expected: three commands complete; solution contains both projects.

- [ ] **Step 4: Enable WPF in the test project**

Modify `src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj` so its main `PropertyGroup` contains `<UseWPF>true</UseWPF>`.

Expected: file saves.

- [ ] **Step 5: Verify empty build and test run**

Run: `dotnet test src-cs/ReadAloud.sln`

Expected: build succeeds; no tests found is acceptable at this stage.

- [ ] **Step 6: Commit**

`git add src-cs && git commit -m "build: scaffold C# WPF solution"`

---

### Task 1: Config model, migration and normalisation

**Files:**
- Create: `src-cs/ReadAloud/Models/AppConfig.cs`
- Create: `src-cs/ReadAloud/Models/CaptureMethod.cs`
- Create: `src-cs/ReadAloud/Services/ConfigService.cs`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `AppConfig` with `int SchemaVersion`, `string Theme`, `string HotkeyRead`, `string HotkeyVoice`, `string HotkeyStop`, `CaptureMethod CaptureMethod`, `string OfflineVoice`, `int SapiRate`, `int Volume`, `int MaxChars`, `bool ShowActivity`.
- Produces: `ConfigService(string appDataConfigPath, string legacyConfigPath)` with `AppConfig Load()` and `void Save(AppConfig config)`.
- Produces: `CaptureMethod` enum values `ClipboardFirst`, `ClipboardOnly`, `UiaFirst`, `UiaOnly`.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/ConfigTests.cs`:

```csharp
[Fact] public void LegacyKeys_MigrateFromInstalledConfig()
// Write legacy JSON with hotkey_read, capture_method="auto", volume=200.
// Assert Load().Theme == "aurora", CaptureMethod == ClipboardFirst, Volume == 100.

[Fact] public void AutoCapture_MigratesToClipboardFirst()

[Fact] public void CorruptLegacyConfig_UsesDefaults()

[Fact] public void CSharpConfigWins_WhenBothFilesExist()

[Fact] public void Save_WritesSchemaVersionOne()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter ConfigTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement model and service**

Define `AppConfig` and `CaptureMethod`. In `ConfigService.Load()`, read `appDataConfigPath` first; if missing, read `legacyConfigPath`, map known keys, normalise, and set `SchemaVersion = 1`. `Save()` writes `SchemaVersion = 1` and creates parent directories. Clamp `Volume` 0–100, `SapiRate` -10–10, `MaxChars` 1–1,000,000; unknown themes fall back to `aurora`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter ConfigTests`

Expected: all five tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add config migration and normalisation"`

---

### Task 2: Hotkey model, validation and registration

**Files:**
- Create: `src-cs/ReadAloud/Models/HotkeyBinding.cs`
- Create: `src-cs/ReadAloud/Services/HotkeyService.cs`
- Create: `src-cs/ReadAloud/Services/IHotkeyRegistrar.cs`
- Test: `src-cs/ReadAloud.Tests/HotkeyTests.cs`

**Interfaces:**
- Consumes: `AppConfig` from Task 1.
- Produces: `record HotkeyBinding(string Action, string Display, ModifierKeys Modifiers, Key Key)`.
- Produces: `IHotkeyRegistrar` with `event Action<string>? HotkeyPressed`, `bool TryRegister(HotkeyBinding binding)`, `void UnregisterAll()`.
- Produces: `HotkeyService(IHotkeyRegistrar registrar)` with `string Format(ModifierKeys modifiers, Key key)`, `bool TryRecord(ModifierKeys modifiers, Key key, out HotkeyBinding binding, out string error)`, `IReadOnlyList<string> RegisterAll(AppConfig config, ISet<string> ownedByOthers)`.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/HotkeyTests.cs`:

```csharp
[Fact] public void Format_ProducesCtrlAltR()
// Assert Format(Control | Alt, Key.R) == "Ctrl+Alt+R".

[Fact] public void DuplicateRecord_IsRejected()
// Record read as Ctrl+Alt+R, then attempt same for stop; assert false and error contains "already used".

[Fact] public void ModifierOnlyRecord_IsRejected()
// Assert TryRecord(Control | Alt, Key.None, ...) is false.

[Fact] public void Arming_UnregistersGlobalHotkeys()
// Call service.BeginRecording(); assert registrar.UnregisterAll called once.

[Fact] public void StartupConflict_SkipsOnlyConflictingAction()
// ownedByOthers = { "Ctrl+Alt+R" }; assert returned conflicts contains read only and stop binding registered.
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter HotkeyTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement service and fake registrar**

`Format()` orders modifiers as Ctrl, Shift, Alt, Win then key name. `TryRecord()` rejects `Key.None`, modifier-only, and keys already recorded. `BeginRecording()` calls `registrar.UnregisterAll()`. `RegisterAll()` parses config strings, skips actions in `ownedByOthers`, returns conflict messages, and registers the rest. Write `FakeHotkeyRegistrar` in the test file.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter HotkeyTests`

Expected: all five tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add hotkey validation and recording"`

---

### Task 3: Selection capture with injectable OS seams

**Files:**
- Create: `src-cs/ReadAloud/Models/CaptureResult.cs`
- Create: `src-cs/ReadAloud/Services/IClipboard.cs`
- Create: `src-cs/ReadAloud/Services/IKeyboardSender.cs`
- Create: `src-cs/ReadAloud/Services/IUiaSelectionReader.cs`
- Create: `src-cs/ReadAloud/Services/IClock.cs`
- Create: `src-cs/ReadAloud/Services/SelectionService.cs`
- Test: `src-cs/ReadAloud.Tests/CaptureTests.cs`

**Interfaces:**
- Consumes: `AppConfig` and `CaptureMethod` from Task 1.
- Produces: `record CaptureResult(string Text, CaptureMethod Method, string Diagnostic)`.
- Produces: `IClipboard` with `Dictionary<int, object?> Snapshot()`, `void Restore(Dictionary<int, object?> snapshot)`, `string? GetText()`, `void SetText(string text)`.
- Produces: `IKeyboardSender` with `void SendCtrlC()`.
- Produces: `IUiaSelectionReader` with `Task<string?> TryReadSelectionAsync(TimeSpan timeout)`.
- Produces: `IClock` with `Task Delay(TimeSpan delay)`.
- Produces: `SelectionService(IClipboard clipboard, IKeyboardSender keyboard, IUiaSelectionReader uia, IClock clock)` with `Task<CaptureResult> CaptureAsync(CaptureMethod method)`.

**Interfaces (continued):**
- Clipboard-first waits 150 ms via `IClock.Delay` before `SendCtrlC`, then polls `IClipboard.GetText()` up to 600 ms. UIA fallback is 500 ms. Legacy `auto` never reaches this layer.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/CaptureTests.cs`:

```csharp
[Fact] public async Task ClipboardFirst_ReturnsText_WhenUiaHasNothing()
// Fake clipboard changes from sentinel to "Selected text"; assert Text, Method == ClipboardFirst, Uia not called.

[Fact] public async Task Capture_WaitsForModifierRelease_BeforeCtrlC()
// Assert clock.Delay received 150ms before keyboard.SendCtrlC call.

[Fact] public async Task Capture_RestoreFailure_IsLogged_ButTextIsReturned()
// Fake clipboard.Restore throws; assert Text returned and Diagnostic contains "restore".

[Fact] public async Task UiaFallback_Runs_WhenClipboardHasNoText()
// Assert Uia called once and Method == ClipboardFirst when text comes from UIA.

[Fact] public async Task ClipboardOnly_NeverCallsUia()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter CaptureTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement service**

Implement the order from the spec: snapshot clipboard, set sentinel, delay 150 ms, send Ctrl+C, poll up to 600 ms, restore, then UIA fallback when the method allows it. A restore failure is appended to `Diagnostic` but does not fail the capture. Make `UiaFirst` try UIA before clipboard and `UiaOnly` never touch clipboard.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter CaptureTests`

Expected: all five tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add reliable selection capture"`

---

### Task 4: Speech engine abstraction and SAPI adapter

**Files:**
- Create: `src-cs/ReadAloud/Models/VoiceInfo.cs`
- Create: `src-cs/ReadAloud/Models/VoiceOptions.cs`
- Create: `src-cs/ReadAloud/Services/ISpeechEngine.cs`
- Create: `src-cs/ReadAloud/Services/SapiSpeechEngine.cs`
- Test: `src-cs/ReadAloud.Tests/SpeechTests.cs`

**Interfaces:**
- Consumes: `AppConfig` from Task 1.
- Produces: `record VoiceInfo(string Name, string Language, bool IsDefault)`.
- Produces: `record VoiceOptions(string VoiceName, int Rate, int Volume)`.
- Produces: `ISpeechEngine` with `IReadOnlyList<VoiceInfo> GetVoices()`, `void Speak(string text, VoiceOptions options)`, `void Stop()`, `bool IsSpeaking { get; }`.
- Produces: `SapiSpeechEngine` implementing `ISpeechEngine` using `SpeechSynthesizer`.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/SpeechTests.cs`:

```csharp
[Fact] public void Speak_UsesSavedVoiceRateAndVolume()
// Fake engine receives options with configured values.

[Fact] public void Speak_EmptyText_DoesNothing()

[Fact] public void Stop_SetsIsSpeakingFalse()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter SpeechTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement adapter**

`SapiSpeechEngine.GetVoices()` maps `InstalledVoices` to `VoiceInfo`; missing saved voice falls back to system default. `Speak()` sets `Rate` and `Volume`, raises `SpeakCompleted`, and catches COM/SAPI exceptions without throwing to the UI.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter SpeechTests`

Expected: all three tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add offline speech engine"`

---

### Task 5: Theme system and main window shell

**Files:**
- Create: `src-cs/ReadAloud/Themes/Aurora.xaml`
- Create: `src-cs/ReadAloud/Themes/Orbit.xaml`
- Create: `src-cs/ReadAloud/Views/MainWindow.xaml`, `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Create: `src-cs/ReadAloud/ViewModels/MainViewModel.cs`
- Test: `src-cs/ReadAloud.Tests/ThemeTests.cs`

**Interfaces:**
- Consumes: `AppConfig` from Task 1, `IHotkeyRegistrar` from Task 2.
- Produces: `MainViewModel` with `string Theme`, `string Status`, `string TextInput`, `int CharacterCount`, `IReadOnlyList<string> HotkeySummary`, `void ApplyTheme(string theme)`.
- Produces: `Aurora.xaml` and `Orbit.xaml` ResourceDictionaries with shared keys `AccentBrush`, `BackgroundBrush`, `TextBrush`, `CardBackgroundBrush`, `CornerRadius`.

- [ ] **Step 1: Write failing test**

`src-cs/ReadAloud.Tests/ThemeTests.cs`:

```csharp
[Fact] public void ApplyTheme_Orbit_SetsThemeAndMergedDictionary()
// Assert vm.Theme == "orbit" and App.Current.Resources.MergedDictionaries contains Orbit.xaml source.

[Fact] public void UnknownTheme_FallsBackToAurora()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src-cs/ReadAloud.sln --filter ThemeTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement XAML dictionaries and view model**

Create both dictionaries with the same keys; no gradients, heavy shadows, or web-style cards. `ApplyTheme()` replaces the application-level dictionary on the UI dispatcher and ignores unknown values.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src-cs/ReadAloud.sln --filter ThemeTests`

Expected: both tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add Aurora and Orbit themes"`

---

### Task 6: Settings window with theme toggle and hotkey recorders

**Files:**
- Create: `src-cs/ReadAloud/Views/SettingsWindow.xaml`, `src-cs/ReadAloud/Views/SettingsWindow.xaml.cs`
- Create: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: `ConfigService` Task 1, `HotkeyService` Task 2, `MainViewModel.ApplyTheme` Task 5.
- Produces: `SettingsViewModel(ConfigService config, HotkeyService hotkeys, MainViewModel main)` with `string Theme`, `CaptureMethod Capture`, `string OfflineVoice`, `int SapiRate`, `int Volume`, `int MaxChars`, `bool ShowActivity`, `string ValidationMessage`, `void Save()`, `void BeginRecording(string action)`, `void FinishRecording(ModifierKeys modifiers, Key key)`.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/SettingsTests.cs`:

```csharp
[Fact] public void ThemeToggle_SavesAndAppliesOrbit()

[Fact] public void FinishRecording_UpdatesReadHotkey()

[Fact] public void Save_InvalidHotkey_ShowsValidationAndKeepsConfig()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter SettingsTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement settings view model and window**

Settings contains: theme toggle, Read and Stop recorder rows, disabled voice-row placeholder, capture selector, offline voice, SAPI rate, volume, max characters, and activity visibility. `Save()` validates through `HotkeyService`, persists via `ConfigService`, and calls `main.ApplyTheme(Theme)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter SettingsTests`

Expected: all three tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add settings and hotkey recorder UI"`

---

### Task 7: Read coordinator and activity log

**Files:**
- Create: `src-cs/ReadAloud/Services/ActivityLog.cs`
- Create: `src-cs/ReadAloud/Services/ReadCoordinator.cs`
- Test: `src-cs/ReadAloud.Tests/ReadCoordinatorTests.cs`

**Interfaces:**
- Consumes: `SelectionService` Task 3, `ISpeechEngine` Task 4, `AppConfig` Task 1, `ActivityLog`.
- Produces: `ActivityLog` with `ObservableCollection<string> Entries`, `void Add(string message)`.
- Produces: `ReadCoordinator(SelectionService selection, ISpeechEngine speech, ActivityLog log)` with `Task RequestReadAsync(string? manualText = null)`, `void Stop()`, `int PendingCount { get; }`.

- [ ] **Step 1: Write failing tests**

`src-cs/ReadAloud.Tests/ReadCoordinatorTests.cs`:

```csharp
[Fact] public async Task NewestReadWins_AndStopClearsPending()
// Queue three reads, run, assert only third text spoken.

[Fact] public async Task ManualText_BypassesCapture()

[Fact] public async Task EmptyCapture_LogsDiagnosticAndDoesNotSpeak()

[Fact] public void Stop_ClearsPendingAndStopsSpeech()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter ReadCoordinatorTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement coordinator**

`RequestReadAsync()` increments a generation counter, discards older pending work, calls `selection.CaptureAsync` unless manual text is supplied, truncates to `MaxChars`, and speaks through `ISpeechEngine`. `Stop()` increments the generation, clears pending count, and calls `speech.Stop()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter ReadCoordinatorTests`

Expected: all four tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add read coordinator and activity log"`

---

### Task 8: App shell, tray, packaging and smoke test

**Files:**
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Modify: `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Create: `src-cs/ReadAloud/Services/TrayService.cs`
- Create: `build-cs.bat`

**Interfaces:**
- Consumes: all services from Tasks 1–7.
- Produces: `TrayService` with `void ShowWindow()`, `void ReadSelection()`, `void Stop()`, `void OpenSettings()`, `void Quit()`.
- Produces: `build-cs.bat` publishing a self-contained exe to `dist-cs/ReadAloud.exe`.

- [ ] **Step 1: Write failing tray test**

`src-cs/ReadAloud.Tests/TrayTests.cs`:

```csharp
[Fact] public void TrayCommands_DelegateToExpectedActions()
// Fake each callback; assert ShowWindow, ReadSelection, Stop, OpenSettings, Quit are invoked once.
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src-cs/ReadAloud.sln --filter TrayTests`

Expected: compile error for missing `TrayService`.

- [ ] **Step 3: Wire app shell**

On startup: load config, apply theme, register hotkeys, create tray, create main window. Window X exits. `Hide to tray` hides. Tray menu contains Show window, Read selected text, Stop speaking, Settings…, Quit. Voice/LLM entries are omitted.

- [ ] **Step 4: Add build script**

`build-cs.bat` runs `dotnet publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist-cs`.

Expected: running `.\build-cs.bat` completes and creates `dist-cs\ReadAloud.exe`.

- [ ] **Step 5: Run full suite**

Run: `dotnet test src-cs/ReadAloud.sln`

Expected: all tests pass.

- [ ] **Step 6: Smoke test packaged app**

Run:
```powershell
Start-Process -FilePath .\dist-cs\ReadAloud.exe -PassThru
Start-Sleep -Seconds 5
Get-Process ReadAloud
```
Expected: process stays alive. Then select text in Notepad, press the read hotkey, confirm activity panel shows `clipboard` or `uia` and speech starts; switch theme in Settings; change a hotkey; restart and confirm persistence.

- [ ] **Step 7: Commit**

`git add src-cs build-cs.bat && git commit -m "feat: complete C# rewrite shell and build"`

---

## Self-review

- **Spec coverage:** config migration, theme toggle, hotkey recorder, clipboard-first capture, SAPI speech, newest-read semantics, tray behaviour and packaging each have an owning task. Deferred Python features are explicitly out of scope and have no task.
- **Step scan:** every step names one command, one file, or one signature; no TBD lines remain.
- **Type consistency:** `CaptureMethod`, `CaptureResult`, `AppConfig`, `HotkeyBinding`, `IHotkeyRegistrar`, `ISpeechEngine`, and `ReadCoordinator` names and signatures are used consistently across tasks.
- **Review Focus:** each of the five risky conditions has a named failing test in the owning task.
- **Proportion:** the plan is mostly signatures, test names, assertions and commands; program bodies are left to the implementer.
