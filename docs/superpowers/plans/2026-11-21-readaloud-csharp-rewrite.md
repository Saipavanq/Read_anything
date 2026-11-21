# Read Aloud C# WPF Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Python/Tkinter app with a phased C# WPF .NET 8 Read Aloud app that fixes selection capture, adds click-to-record hotkeys, keeps Aurora Compact and Orbit Console themes, and delivers the approved Vox-style features in testable phases.

**Architecture:** WPF MVVM shell with ResourceDictionary theme switching, a dispatcher-owned `ReadCoordinator`, and injectable OS seams for clipboard, UIA, keyboard injection, speech, hotkeys, playback, history, and auto-read. Workers never touch UI controls; state and activity events are surfaced through view models. Each phase ends with a passing test run and a commit.

**Tech Stack:** Windows 10, C# 12, .NET 8, WPF, `System.Speech.Synthesis`, `System.Windows.Automation`, Win32 `RegisterHotKey`/`SendInput`/`WH_MOUSE_LL`, xUnit, self-contained `dotnet publish`.

**Spec:** `docs/superpowers/specs/2026-11-21-readaloud-csharp-rewrite-design.md`

## Global Constraints

- Windows 10 only.
- .NET 8 SDK is not installed; install it before implementation.
- Product name remains **Read Aloud**.
- Themes are `aurora` and `orbit`; Aurora is default. No Dark/Light/System selector.
- Config is JSON at `%APPDATA%\Read Aloud\settings.json`; legacy Python `config/settings.json` migrates once.
- Legacy `auto` capture maps to `ClipboardFirst` and is not a visible mode.
- Capture methods are `ClipboardFirst`, `ClipboardOnly`, `UiaFirst`, `UiaOnly`.
- Default config is fully offline; no telemetry or network requirement.
- Newest read wins; Stop cancels speech and clears pending requests.
- Hotkey recorders live in Settings and temporarily unregister global hotkeys.
- Tray menu: Show window, Read selected text, Stop speaking, Settings…, Quit. Window X exits.
- Phase 1 uses `sapi_rate`; from Phase 2, `playback_speed` is authoritative and `sapi_rate` is migrated once.
- `offline_voice` stores SAPI `Description`, with `Name` fallback.
- Playback skip is sentence-level and continues to the end.
- History and auto-read are off by default.
- Auto-read uses an opt-in mouse-release hook and never keystrokes or coordinates.
- Tests use injected seams; no test touches the real clipboard, registry, speech device, or mouse hook.
- One test command after setup: `dotnet test src-cs/ReadAloud.sln`.
- Keep the Python build until the C# build passes verification.

## File Structure

```text
src-cs/
  ReadAloud.sln
  ReadAloud/
    ReadAloud.csproj
    App.xaml
    App.xaml.cs
    Themes/
      Aurora.xaml
      Orbit.xaml
    Models/
      AppConfig.cs
      CaptureMethod.cs
      CaptureResult.cs
      HotkeyBinding.cs
      VoiceInfo.cs
      VoiceOptions.cs
      VoiceCard.cs
      PlaybackState.cs
      HistoryEntry.cs
    Services/
      ConfigService.cs
      IThemeService.cs / ThemeService.cs
      HotkeyService.cs / IHotkeyRegistrar.cs
      SelectionService.cs
      IClipboard.cs / IKeyboardSender.cs / IUiaSelectionReader.cs / IClock.cs
      ISpeechEngine.cs / SapiSpeechEngine.cs
      ActivityLog.cs
      ReadCoordinator.cs
      TrayService.cs
      PlaybackService.cs
      IMiniPlayerView.cs / MiniPlayerService.cs
      IHistorySink.cs / HistoryService.cs
      IAutoReadTrigger.cs / MouseUpAutoReadTrigger.cs
      IRegistryAccessor.cs / StartupService.cs
    ViewModels/
      MainViewModel.cs
      HomeViewModel.cs
      SettingsViewModel.cs
      VoiceLibraryViewModel.cs
      HistoryViewModel.cs
      MiniPlayerViewModel.cs
    Views/
      MainWindow.xaml / MainWindow.xaml.cs
      HomeView.xaml
      VoiceLibraryView.xaml
      HistoryView.xaml
      SettingsWindow.xaml / SettingsWindow.xaml.cs
      MiniPlayerWindow.xaml / MiniPlayerWindow.xaml.cs
  ReadAloud.Tests/
    ReadAloud.Tests.csproj
    ConfigTests.cs
    HotkeyTests.cs
    CaptureTests.cs
    SpeechTests.cs
    ThemeTests.cs
    SettingsTests.cs
    ReadCoordinatorTests.cs
    TrayTests.cs
    PlaybackTests.cs
    MiniPlayerTests.cs
    VoiceLibraryTests.cs
    HistoryTests.cs
    AutomationTests.cs
build-cs.bat
```

## Review Focus

- **Text selected in an app with no UIA selection** → clipboard capture succeeds and activity says `clipboard`. Test: `CaptureTests.ClipboardFirst_ReturnsText_WhenUiaHasNothing` in Task 3.
- **Clipboard restore fails for one format** → read still succeeds and the failure is logged. Test: `CaptureTests.Capture_RestoreFailure_IsLogged_ButTextIsReturned` in Task 3.
- **Hotkey duplicated or already owned by another app** → app keeps running, skips only that action, and shows a clear message. Tests: `HotkeyTests.DuplicateRecord_IsRejected` and `StartupConflict_SkipsOnlyConflictingAction` in Task 2.
- **Corrupt legacy config, legacy `auto`, legacy `sapi_rate`, or Python voice description** → safe defaults, `ClipboardFirst`, derived speed, and correct voice identity. Tests: `ConfigTests.CorruptLegacyConfig_UsesDefaults`, `AutoCapture_MigratesToClipboardFirst`, `LegacySapiRate_DerivesPlaybackSpeedOnce`, and `OfflineVoice_MatchesDescriptionThenName` in Task 1.
- **New read while speaking or Stop during capture** → no stale speech and newest read wins. Test: `ReadCoordinatorTests.NewestReadWins_AndStopClearsPending` in Task 7.

---

## Phase 1 — Core reading

### Task 0: Toolchain and solution scaffold

**Files:**
- Create: `src-cs/ReadAloud.sln`
- Create: `src-cs/ReadAloud/ReadAloud.csproj`
- Create: `src-cs/ReadAloud/App.xaml`, `src-cs/ReadAloud/App.xaml.cs`
- Create: `src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj`
- Create: `build-cs.bat`

**Interfaces:**
- Consumes: nothing.
- Produces: a building WPF app and xUnit test project referenced by the solution.

- [ ] **Step 1: Install the .NET 8 SDK**

Run: `winget install --id Microsoft.DotNet.SDK.8 --exact --accept-source-agreements --accept-package-agreements`

Expected: installer completes. If `winget` is unavailable, install from `https://dotnet.microsoft.com/download/dotnet/8.0`.

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

Expected: solution contains both projects.

- [ ] **Step 4: Enable WPF in the test project**

Modify `src-cs/ReadAloud.Tests/ReadAloud.Tests.csproj` main `PropertyGroup` to include `<UseWPF>true</UseWPF>`.

- [ ] **Step 5: Add the build script**

Create `build-cs.bat` with:
```bat
@echo off
dotnet publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist-cs
```

- [ ] **Step 6: Verify empty build**

Run: `dotnet test src-cs/ReadAloud.sln`

Expected: build succeeds; no tests found is acceptable.

- [ ] **Step 7: Commit**

`git add src-cs build-cs.bat && git commit -m "build: scaffold C# WPF solution"`

---

### Task 1: Config model, migration and normalisation

**Files:**
- Create: `src-cs/ReadAloud/Models/AppConfig.cs`
- Create: `src-cs/ReadAloud/Models/CaptureMethod.cs`
- Create: `src-cs/ReadAloud/Models/VoiceInfo.cs`
- Create: `src-cs/ReadAloud/Models/VoiceIdentity.cs`
- Create: `src-cs/ReadAloud/Services/ConfigService.cs`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `enum CaptureMethod { ClipboardFirst, ClipboardOnly, UiaFirst, UiaOnly }`.
- Produces: `AppConfig` with `int SchemaVersion`, `string Theme`, `string HotkeyRead`, `string HotkeyVoice`, `string HotkeyStop`, `CaptureMethod CaptureMethod`, `string OfflineVoice`, `int SapiRate`, `int Volume`, `int MaxChars`, `bool ShowActivity`, `double PlaybackSpeed`, `bool AutoReadSelection`, `bool MiniPlayerEnabled`, `bool MiniPlayerAlwaysOnTop`, `bool MiniPlayerAutoHide`, `bool HistoryEnabled`, `int HistoryRetentionDays`, `bool StartWithWindows`, `bool MinimizeToTray`, `bool ClipboardRestore`.
- Produces: `record VoiceInfo(string Name, string Description, string Language, string Gender, bool IsDefault)`.
- Produces: `static VoiceIdentity.Matches(string configured, VoiceInfo voice)` preferring `Description`, then `Name`, case-insensitive.
- Produces: `ConfigService(string appDataConfigPath, string legacyConfigPath)` with `AppConfig Load()` and `void Save(AppConfig config)`.

- [ ] **Step 1: Write failing tests**

`ConfigTests.cs`:
```csharp
[Fact] public void LegacyKeys_MigrateFromInstalledConfig()
// Legacy JSON with theme="orbit", hotkey_read="Ctrl+Alt+T", capture_method="auto", volume=200.
// Assert Load(): Theme=="orbit", HotkeyRead=="Ctrl+Alt+T", CaptureMethod==ClipboardFirst, Volume==100, SchemaVersion==1.

[Fact] public void AutoCapture_MigratesToClipboardFirst()

[Fact] public void CorruptLegacyConfig_UsesDefaults()

[Fact] public void CSharpConfigWins_WhenBothFilesExist()

[Fact] public void Save_WritesSchemaVersionOne()

[Fact] public void LegacySapiRate_DerivesPlaybackSpeedOnce()
// sapi_rate=-10 and no playback_speed -> PlaybackSpeed==0.5; sapi_rate=10 -> 2.0.

[Fact] public void PlaybackSpeed_Wins_WhenBothPresent()

[Fact] public void OfflineVoice_MatchesDescriptionThenName()
// Helper VoiceIdentity.Matches(configured, VoiceInfo) prefers Description, then Name, case-insensitive.

[Fact] public void BadNumbers_AreClamped()
// volume 200->100, sapi_rate 20->10, max_chars 0->1, retention 500->365, speed 9->3.0.
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter ConfigTests`

Expected: compile errors for missing types.

- [ ] **Step 3: Implement model and service**

`Load()` reads the C# path first; if missing, reads the legacy path, maps known keys, and normalises. `Save()` creates parent directories and writes `schemaVersion: 1`. Derive `PlaybackSpeed` only when absent: `clamp(2^(SapiRate/10), 0.5, 3.0)`. Implement `VoiceIdentity.Matches()` in `Models/VoiceIdentity.cs`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter ConfigTests`

Expected: all nine tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add config migration and normalisation"`

---

### Task 2: Hotkey model, validation and registration

**Files:**
- Create: `src-cs/ReadAloud/Models/HotkeyBinding.cs`
- Create: `src-cs/ReadAloud/Services/IHotkeyRegistrar.cs`
- Create: `src-cs/ReadAloud/Services/HotkeyService.cs`
- Test: `src-cs/ReadAloud.Tests/HotkeyTests.cs`

**Interfaces:**
- Consumes: `AppConfig`.
- Produces: `record HotkeyBinding(string Action, string Display, ModifierKeys Modifiers, Key Key)`.
- Produces: `IHotkeyRegistrar` with `event Action<string>? HotkeyPressed`, `bool TryRegister(HotkeyBinding binding)`, `void UnregisterAll()`.
- Produces: `HotkeyService(IHotkeyRegistrar registrar)` with `string Format(ModifierKeys modifiers, Key key)`, `bool TryRecord(string action, ModifierKeys modifiers, Key key, out HotkeyBinding binding, out string error)`, `void BeginRecording()`, `void EndRecording(AppConfig config)`, `IReadOnlyList<string> RegisterAll(AppConfig config, ISet<string> ownedByOthers)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Format_ProducesCtrlAltR()
// Format(Control | Alt, Key.R) == "Ctrl+Alt+R"; order is Ctrl, Shift, Alt, Win, Key.

[Fact] public void DuplicateRecord_IsRejected()
// Record read Ctrl+Alt+R then stop same combo; false and error contains "already used".

[Fact] public void ModifierOnlyRecord_IsRejected()

[Fact] public void Arming_UnregistersGlobalHotkeys()
// BeginRecording() calls registrar.UnregisterAll once.

[Fact] public void EndRecording_ReRegistersSavedConfig()

[Fact] public void StartupConflict_SkipsOnlyConflictingAction()
// ownedByOthers={"Ctrl+Alt+R"}; read skipped, stop registered, conflict returned.
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter HotkeyTests`

Expected: compile errors.

- [ ] **Step 3: Implement service and fake registrar**

`Format()` orders modifiers as specified. `TryRecord()` rejects `Key.None`, modifier-only, and duplicates. `BeginRecording()` unregisters all. `EndRecording()` re-registers from config. `RegisterAll()` parses config strings, skips `ownedByOthers`, registers the rest, and returns conflict messages. Fake registrar stores registrations and invocation counts.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter HotkeyTests`

Expected: all six tests pass.

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
- Consumes: `CaptureMethod`.
- Produces: `record CaptureResult(string Text, CaptureMethod Method, string Diagnostic)`.
- Produces: `IClipboard` with `Dictionary<int, object?> Snapshot()`, `void Restore(Dictionary<int, object?> snapshot)`, `string? GetText()`, `void SetText(string text)`.
- Produces: `IKeyboardSender` with `void SendCtrlC()`.
- Produces: `IUiaSelectionReader` with `Task<string?> TryReadSelectionAsync(TimeSpan timeout)`.
- Produces: `IClock` with `Task Delay(TimeSpan delay)`.
- Produces: `SelectionService(IClipboard clipboard, IKeyboardSender keyboard, IUiaSelectionReader uia, IClock clock)` with `Task<CaptureResult> CaptureAsync(CaptureMethod method)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task ClipboardFirst_ReturnsText_WhenUiaHasNothing()
// Fake clipboard changes from sentinel to "Selected text"; assert Text and Method==ClipboardFirst; UiaCalls==0.

[Fact] public async Task Capture_WaitsForModifierRelease_BeforeCtrlC()
// Assert clock received 150ms before keyboard.SendCtrlC.

[Fact] public async Task Capture_RestoreFailure_IsLogged_ButTextIsReturned()
// Restore throws; text returned and Diagnostic contains "restore".

[Fact] public async Task UiaFallback_Runs_WhenClipboardHasNoText()
// Uia called once and Method==ClipboardFirst when UIA supplies text.

[Fact] public async Task ClipboardOnly_NeverCallsUia()

[Fact] public async Task UiaFirst_TriesUiaBeforeClipboard()

[Fact] public async Task UiaOnly_NeverTouchesClipboard()

[Fact] public async Task TotalFailure_ReturnsDiagnostic()
// Diagnostic contains "clipboard unchanged; UIA no selection".
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter CaptureTests`

Expected: compile errors.

- [ ] **Step 3: Implement service**

Clipboard-first: snapshot, set sentinel, delay 150 ms, send Ctrl+C, poll `GetText()` up to 600 ms, restore, then UIA fallback when allowed. UIA timeout is 500 ms. Restore failures append to `Diagnostic` without failing. `UiaFirst` tries UIA before clipboard; `UiaOnly` never touches clipboard.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter CaptureTests`

Expected: all eight tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add reliable selection capture"`

---

### Task 4: Speech engine abstraction and SAPI adapter

**Files:**
- Create: `src-cs/ReadAloud/Models/VoiceOptions.cs`
- Create: `src-cs/ReadAloud/Services/ISpeechEngine.cs`
- Create: `src-cs/ReadAloud/Services/SapiSpeechEngine.cs`
- Test: `src-cs/ReadAloud.Tests/SpeechTests.cs`

**Interfaces:**
- Consumes: `AppConfig`, `VoiceInfo`, and `VoiceIdentity` from Task 1.
- Produces: `record VoiceOptions(string VoiceName, int Rate, int Volume)`.
- Produces: `ISpeechEngine` with `IReadOnlyList<VoiceInfo> GetVoices()`, `void Speak(string text, VoiceOptions options)`, `void Stop()`, `bool IsSpeaking { get; }`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Speak_UsesSavedVoiceRateAndVolume()
// Fake engine receives options with configured values.

[Fact] public void Speak_EmptyText_DoesNothing()

[Fact] public void Stop_SetsIsSpeakingFalse()

[Fact] public void GetVoices_MapsSapiVoices()
// Description preserved; missing gender -> "Not specified".
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter SpeechTests`

Expected: compile errors.

- [ ] **Step 3: Implement adapter**

`SapiSpeechEngine.GetVoices()` maps `InstalledVoices` into `VoiceInfo`. Missing saved voice falls back to default using `VoiceIdentity.Matches`. `Speak()` sets rate/volume, catches COM/SAPI exceptions, and never throws to UI. Fake engine used in tests.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter SpeechTests`

Expected: all four tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add offline speech engine"`

---

### Task 5: Theme service, main shell and sidebar placeholders

**Files:**
- Create: `src-cs/ReadAloud/Themes/Aurora.xaml`, `src-cs/ReadAloud/Themes/Orbit.xaml`
- Create: `src-cs/ReadAloud/Services/IThemeService.cs`, `src-cs/ReadAloud/Services/ThemeService.cs`
- Create: `src-cs/ReadAloud/ViewModels/MainViewModel.cs`
- Create: `src-cs/ReadAloud/Views/MainWindow.xaml`, `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Create: `src-cs/ReadAloud/Views/HomeView.xaml`
- Test: `src-cs/ReadAloud.Tests/ThemeTests.cs`

**Interfaces:**
- Consumes: `AppConfig`.
- Produces: `IThemeService` with `string CurrentTheme { get; }`, `void Apply(string theme)`.
- Produces: `ThemeService(ResourceDictionary applicationResources)` loading `Themes/Aurora.xaml` or `Themes/Orbit.xaml` by `Source` and replacing the first merged dictionary.
- Produces: `MainViewModel(IThemeService themes, IHotkeyRegistrar registrar)` with `string Theme`, `string Status`, `string Greeting`, `IReadOnlyList<string> NavigationItems`, `IReadOnlyDictionary<string,bool> NavigationEnabled`, `void ApplyTheme(string theme)`.

**XAML resource keys:** `AccentBrush`, `BackgroundBrush`, `TextBrush`, `CardBackgroundBrush`, `CornerRadius`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void ApplyTheme_Orbit_SetsCurrentThemeAndMergedDictionary()
// resources.MergedDictionaries contains source ending "Orbit.xaml".

[Fact] public void UnknownTheme_FallsBackToAurora()

[Fact] public void MainViewModel_MarksLaterNavItemsDisabled()
// NavigationItems contains Home, Voice Library, History, Settings; Voice Library and History disabled in Phase 1.
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter ThemeTests`

Expected: compile errors.

- [ ] **Step 3: Implement dictionaries, service and shell**

Aurora is light with teal/cyan accent; Orbit is dark with electric-blue accent and status-ring styling. Both share control tokens. MainWindow has a left rail and content region; disabled nav items show a later-release tooltip. Greeting is `Ready to listen?`, status starts `Ready`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter ThemeTests`

Expected: all three tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add Aurora and Orbit themes with main shell"`

---

### Task 6: Settings window with theme toggle and hotkey recorders

**Files:**
- Create: `src-cs/ReadAloud/Views/SettingsWindow.xaml`, `src-cs/ReadAloud/Views/SettingsWindow.xaml.cs`
- Create: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: `ConfigService`, `HotkeyService`, `IThemeService`, `MainViewModel`.
- Produces: `SettingsViewModel(ConfigService config, HotkeyService hotkeys, IThemeService themes, MainViewModel main)` with writable `Theme`, `CaptureMethod`, `OfflineVoice`, `SapiRate`, `Volume`, `MaxChars`, `ShowActivity`, `MiniPlayerEnabled`, `MiniPlayerAlwaysOnTop`, `MiniPlayerAutoHide`, `HistoryEnabled`, `HistoryRetentionDays`, `AutoReadSelection`, `StartWithWindows`, `MinimizeToTray`, `ClipboardRestore`; `string ValidationMessage`; `void Save()`, `void BeginRecording(string action)`, `void FinishRecording(string action, ModifierKeys modifiers, Key key)`, `void CancelRecording()`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void ThemeToggle_SavesAndAppliesOrbit()

[Fact] public void FinishRecording_UpdatesReadHotkey()

[Fact] public void Save_InvalidHotkey_ShowsValidationAndKeepsConfig()

[Fact] public void DeferredVoiceRow_IsNotEditable()
// Expose IsVoiceCommandEditable == false.
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter SettingsTests`

Expected: compile errors.

- [ ] **Step 3: Implement settings view model and window**

Sections: Appearance, Hotkeys, Capture, Voice & playback, Playback & mini player, Automation, Privacy. Voice-command row is disabled with “later release”. Recorder uses `BeginRecording`/`FinishRecording` and shows inline validation. `Save()` validates, persists, applies theme, and re-registers hotkeys.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter SettingsTests`

Expected: all four tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add settings and hotkey recorder UI"`

---

### Task 7: Read coordinator and activity log

**Files:**
- Create: `src-cs/ReadAloud/Services/ActivityLog.cs`
- Create: `src-cs/ReadAloud/Services/ReadCoordinator.cs`
- Test: `src-cs/ReadAloud.Tests/ReadCoordinatorTests.cs`

**Interfaces:**
- Consumes: `SelectionService`, `ISpeechEngine`, `AppConfig`, `ActivityLog`.
- Produces: `ActivityLog` with `ObservableCollection<string> Entries`, `void Add(string message)`.
- Produces: `ReadCoordinator(SelectionService selection, ISpeechEngine speech, ActivityLog log, Func<AppConfig> configProvider)` with `Task RequestReadAsync(string? manualText = null)`, `void Stop()`, `int PendingCount { get; }`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task NewestReadWins_AndStopClearsPending()
// Queue three read requests; only third text spoken; Stop clears pending count.

[Fact] public async Task ManualText_BypassesCapture()
// CaptureCalls==0 and manual text spoken.

[Fact] public async Task EmptyCapture_LogsDiagnosticAndDoesNotSpeak()

[Fact] public void Stop_ClearsPendingAndStopsSpeech()

[Fact] public async Task LongText_IsTruncatedToMaxChars()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter ReadCoordinatorTests`

Expected: compile errors.

- [ ] **Step 3: Implement coordinator**

Use a generation counter. Each request increments it and discards older pending work. Capture uses the config’s `CaptureMethod`; text is truncated to `MaxChars`; diagnostics are logged. `Stop()` increments generation, clears pending, and calls `speech.Stop()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter ReadCoordinatorTests`

Expected: all five tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add read coordinator and activity log"`

---

### Task 8: Home actions, tray, app shell, packaging and Phase 1 smoke test

**Files:**
- Create: `src-cs/ReadAloud/ViewModels/HomeViewModel.cs`
- Create: `src-cs/ReadAloud/Services/TrayService.cs`
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Modify: `src-cs/ReadAloud/Views/MainWindow.xaml`, `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Test: `src-cs/ReadAloud.Tests/TrayTests.cs`

**Interfaces:**
- Consumes: all Phase 1 services.
- Produces: `HomeViewModel(ReadCoordinator coordinator, ActivityLog log, HotkeyService hotkeys, Func<AppConfig> configProvider)` with `string TextInput`, `int CharacterCount`, `string PrimaryButtonText == "Listen"`, `string PrimaryButtonAccessibleName == "Read selection"`, `ICommand ListenCommand`, `ICommand ClearCommand`, `IReadOnlyList<string> HotkeySummary`, `ObservableCollection<string> Activity`.
- Produces: `TrayService(Action showWindow, Action readSelection, Action stop, Action openSettings, Action quit)` with `IReadOnlyList<string> MenuItems`, `void ShowWindow()`, `void ReadSelection()`, `void Stop()`, `void OpenSettings()`, `void Quit()`.

- [ ] **Step 1: Write failing tray test**

```csharp
[Fact] public void TrayCommands_DelegateToExpectedActions()
// Each action called once; MenuItems equals Show window, Read selected text, Stop speaking, Settings…, Quit.
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src-cs/ReadAloud.sln --filter TrayTests`

Expected: compile error for missing `TrayService`.

- [ ] **Step 3: Wire Home, app shell and tray**

Home shows greeting/status, manual input with the exact placeholder, character count, Listen/Clear, capture selector, hotkey summary, and activity panel. App startup loads config, applies theme, registers hotkeys, creates tray, and shows MainWindow. X exits; Hide to tray hides; tray menu delegates through `TrayService`.

- [ ] **Step 4: Build self-contained exe**

Run: `.\\build-cs.bat`

Expected: `dist-cs\\ReadAloud.exe` exists.

- [ ] **Step 5: Run full suite**

Run: `dotnet test src-cs/ReadAloud.sln`

Expected: all Phase 1 tests pass.

- [ ] **Step 6: Smoke test packaged app**

Run:
```powershell
Start-Process -FilePath .\dist-cs\ReadAloud.exe -PassThru
Start-Sleep -Seconds 5
Get-Process ReadAloud
```
Expected: process stays alive. Select text in Notepad and a browser, press the read hotkey, confirm activity shows `clipboard` or `uia`, confirm speech starts, switch theme, change a hotkey, restart and confirm persistence. Re-run the original `no selection after ~1400ms` case.

- [ ] **Step 7: Commit**

`git add src-cs build-cs.bat && git commit -m "feat: complete Phase 1 C# core rewrite"`

---

## Phase 2 — Playback and floating mini player

### Task 9: Playback model and controls

**Files:**
- Create: `src-cs/ReadAloud/Models/PlaybackState.cs`
- Create: `src-cs/ReadAloud/Services/PlaybackService.cs`
- Modify: `src-cs/ReadAloud/Services/ReadCoordinator.cs`
- Test: `src-cs/ReadAloud.Tests/PlaybackTests.cs`

**Interfaces:**
- Consumes: `ISpeechEngine`, `Func<AppConfig>`, `VoiceIdentity` from Task 1.
- Produces: `enum PlaybackStatus { Stopped, Playing, Paused, Completed }`.
- Produces: `PlaybackState` with `string Text`, `IReadOnlyList<string> Sentences`, `int CurrentSentenceIndex`, `PlaybackStatus Status`, `double Speed`, `string VoiceName`, `TimeSpan Elapsed`, `TimeSpan EstimatedRemaining`, `double Progress`.
- Produces: `PlaybackService(ISpeechEngine speech, Func<AppConfig> configProvider)` with `PlaybackState State`, `Task PlayAsync(string text, VoiceOptions options)`, `void Pause()`, `void Resume()`, `void Stop()`, `void SkipNext()`, `void SkipPrevious()`, `void SetSpeed(double speed)`, `event Action<PlaybackState>? StateChanged`.
- Modifies: `ReadCoordinator` constructor to `ReadCoordinator(SelectionService selection, PlaybackService playback, ActivityLog log, Func<AppConfig> configProvider)` and speak through `playback.PlayAsync`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task PlayAsync_SetsPlayingAndSpeaksText()

[Fact] public void PauseAndResume_UpdateStatus()

[Fact] public void Stop_SetsStoppedAndClearsProgress()

[Fact] public void SkipNext_MovesOneSentenceAndContinuesToEnd()
// After skip from sentence 0, State.Text equals joined sentences[1..]; Status==Playing.

[Fact] public void SkipPrevious_AtFirstSentence_StaysAtFirst()

[Fact] public void SetSpeed_MapsSpeedToSapiRate()
// 0.5->-10, 1.0->0, 2.0->10, 3.0->10.

[Fact] public void ProgressAndRemaining_ArePublished()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter PlaybackTests`

Expected: compile errors.

- [ ] **Step 3: Implement playback service**

Split text on sentence boundaries with a regex. Skip creates a new concatenated text from the target sentence to the end, restarts synthesis, and preserves status. Speed mapping is `round(10 * log2(speed))`, clamped to -10–10. Remaining time is word-count based and updated from `SpeakProgress` when available.

- [ ] **Step 4: Update ReadCoordinator and existing tests**

Change constructor to use `PlaybackService`; update `ReadCoordinatorTests` fakes. Run: `dotnet test src-cs/ReadAloud.sln --filter ReadCoordinatorTests` and expect all five tests pass.

- [ ] **Step 5: Run playback suite**

Run: `dotnet test src-cs/ReadAloud.sln --filter PlaybackTests`

Expected: all seven tests pass.

- [ ] **Step 6: Commit**

`git add src-cs && git commit -m "feat: add playback state and controls"`

---

### Task 10: Floating mini player

**Files:**
- Create: `src-cs/ReadAloud/Services/IMiniPlayerView.cs`
- Create: `src-cs/ReadAloud/Services/MiniPlayerService.cs`
- Create: `src-cs/ReadAloud/ViewModels/MiniPlayerViewModel.cs`
- Create: `src-cs/ReadAloud/Views/MiniPlayerWindow.xaml`, `src-cs/ReadAloud/Views/MiniPlayerWindow.xaml.cs`
- Modify: `src-cs/ReadAloud/Views/HomeView.xaml`
- Test: `src-cs/ReadAloud.Tests/MiniPlayerTests.cs`

**Interfaces:**
- Consumes: `PlaybackService`, `AppConfig`.
- Produces: `IMiniPlayerView` with `void Show()`, `void Hide()`, `bool Topmost { get; set; }`, `event Action? ExpandRequested`, `event Action? CloseRequested`, `void Update(PlaybackState state)`.
- Produces: `MiniPlayerService(IMiniPlayerView view, Func<AppConfig> configProvider)` with `bool IsVisible`, `void ShowForSelectionRead()`, `void Hide()`, `void SetAlwaysOnTop(bool value)`, `void UpdateState(PlaybackState state)`, `event Action? ExpandRequested`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void ShowForSelectionRead_ShowsWhenEnabled()

[Fact] public void ShowForSelectionRead_DoesNothingWhenDisabled()

[Fact] public void SetAlwaysOnTop_SetsViewTopmost()

[Fact] public void UpdateState_ViewsPlaybackState()

[Fact] public void Close_HidesOnlyMiniPlayer()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter MiniPlayerTests`

Expected: compile errors.

- [ ] **Step 3: Implement mini player**

MiniPlayerWindow uses native WPF controls with no web-style chrome. Home playback panel and mini player share `MiniPlayerViewModel`. Auto-hide uses `PlaybackStatus.Completed`. Expand raises `ExpandRequested`; the app shell focuses MainWindow.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter MiniPlayerTests`

Expected: all five tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add floating mini player"`

---

## Phase 3 — Voice Library and History

### Task 11: Voice Library

**Files:**
- Create: `src-cs/ReadAloud/Models/VoiceCard.cs`
- Create: `src-cs/ReadAloud/ViewModels/VoiceLibraryViewModel.cs`
- Create: `src-cs/ReadAloud/Views/VoiceLibraryView.xaml`
- Modify: `src-cs/ReadAloud/ViewModels/MainViewModel.cs` to enable Voice Library navigation.
- Test: `src-cs/ReadAloud.Tests/VoiceLibraryTests.cs`

**Interfaces:**
- Consumes: `ISpeechEngine`, `ConfigService`, `VoiceInfo`.
- Produces: `record VoiceCard(string Name, string Description, string Language, string Gender, string QualityLabel, bool IsDefault)`.
- Produces: `VoiceLibraryViewModel(ISpeechEngine speech, ConfigService config)` with `IReadOnlyList<VoiceCard> Voices`, `string SearchText`, `string? LanguageFilter`, `IReadOnlyList<VoiceCard> FilteredVoices`, `void Refresh()`, `void Preview(VoiceCard card)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Refresh_MapsInstalledVoicesToCards()
// QualityLabel=="System"; Gender from VoiceInfo.Gender or "Not specified".

[Fact] public void FilteredVoices_FiltersBySearchAndLanguage()

[Fact] public void Preview_UsesSelectedVoiceAndCurrentSpeed()

[Fact] public void OfflineVoice_MatchesByDescription()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter VoiceLibraryTests`

Expected: compile errors.

- [ ] **Step 3: Implement view and view model**

Voice cards show name, language, gender presentation, preview button, and `System` quality label. Search filters by name/description; language filter uses `Language`. Preview speaks a short sample using saved speed/volume. Enable the rail item.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter VoiceLibraryTests`

Expected: all four tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add voice library"`

---

### Task 12: Local history

**Files:**
- Create: `src-cs/ReadAloud/Models/HistoryEntry.cs`
- Create: `src-cs/ReadAloud/Services/IHistorySink.cs`
- Create: `src-cs/ReadAloud/Services/HistoryService.cs`
- Create: `src-cs/ReadAloud/ViewModels/HistoryViewModel.cs`
- Create: `src-cs/ReadAloud/Views/HistoryView.xaml`
- Modify: `src-cs/ReadAloud/Services/PlaybackService.cs` — constructor becomes `PlaybackService(ISpeechEngine speech, Func<AppConfig> configProvider, IHistorySink? history = null)`; it records one entry on accepted speech and updates its status/duration.
- Modify: `src-cs/ReadAloud/ViewModels/MainViewModel.cs` to enable History navigation.
- Test: `src-cs/ReadAloud.Tests/HistoryTests.cs`

**Interfaces:**
- Consumes: `AppConfig`, `PlaybackState`.
- Produces: `record HistoryEntry(string Id, string Text, DateTimeOffset Timestamp, TimeSpan Duration, string Status, string Voice, double Speed, string CaptureMethod, string Source)`.
- Produces: `IHistorySink` with `void Record(HistoryEntry entry)`.
- Produces: `HistoryService(string path)` implementing `IHistorySink` with `bool IsEnabled`, `IReadOnlyList<HistoryEntry> Entries`, `void Add(HistoryEntry entry)`, `void Delete(string id)`, `void DeleteAll()`, `void ApplyRetention(int days)`.
- Produces: `HistoryViewModel(HistoryService history, ReadCoordinator coordinator)` with `IReadOnlyList<HistoryEntry> Entries`, `ICommand ReplayCommand`, `ICommand DeleteCommand`, `ICommand DeleteAllCommand`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void DisabledHistory_RecordsNothing()

[Fact] public void EnabledHistory_StoresTruncatedSpokenText()
// Entry.Text equals text passed to PlaybackService, not original long selection.

[Fact] public void Duration_RecordedForCompletedStoppedInterrupted()

[Fact] public void Replay_CreatesNewEntryWithSourceReplay()

[Fact] public void DeleteDeleteAllAndRetention_Work()

[Fact] public void CorruptHistoryFile_FallsBackToEmpty()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter HistoryTests`

Expected: compile errors.

- [ ] **Step 3: Implement history**

`HistoryService` uses a temp path in tests. Entry is created when a read is accepted for speech; status/duration update on completion, Stop, interruption, or skip. Replay calls `ReadCoordinator.RequestReadAsync(entry.Text)` and records a new `replay` entry. Corrupt JSON loads as empty. Enable the rail item.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter HistoryTests`

Expected: all six tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add local read history"`

---

## Phase 4 — Automation and expanded settings

### Task 13: Opt-in auto-read trigger

**Files:**
- Create: `src-cs/ReadAloud/Services/IAutoReadTrigger.cs`
- Create: `src-cs/ReadAloud/Services/MouseUpAutoReadTrigger.cs`
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Test: `src-cs/ReadAloud.Tests/AutomationTests.cs` (auto-read portion)

**Interfaces:**
- Consumes: `AppConfig`, `ReadCoordinator`.
- Produces: `IAutoReadTrigger` with `event Action? SelectionCompleted`, `void Start()`, `void Stop()`.
- Produces: `MouseUpAutoReadTrigger(Func<bool> isForegroundOwnWindow)` implementing `IAutoReadTrigger`; observes `WM_LBUTTONUP`, debounces 120 ms, ignores events when `isForegroundOwnWindow()` is true.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void AutoReadDisabled_NoTriggerStarted()

[Fact] public void AutoReadEnabled_TriggerStarted()

[Fact] public void FakeMouseUp_CallsCoordinatorOnceAfterDebounce()
// Use FakeAutoReadTrigger with injectable clock; assert one call after 120ms.

[Fact] public void OwnWindowForeground_EventIgnored()

[Fact] public void DisablingAutoRead_StopsTrigger()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter AutomationTests`

Expected: compile errors.

- [ ] **Step 3: Implement trigger seam and real hook**

Install `WH_MOUSE_LL` only while `auto_read_selection` is true. Observe only `WM_LBUTTONUP`; no keystrokes or coordinates are recorded. Debounce 120 ms. Ignore events when a Read Aloud window is foreground. On trigger, call `ReadCoordinator.RequestReadAsync()` on the dispatcher. Tests use a fake trigger and clock, never a real hook.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --filter AutomationTests`

Expected: all five auto-read tests pass.

- [ ] **Step 5: Commit**

`git add src-cs && git commit -m "feat: add opt-in auto-read trigger"`

---

### Task 14: Startup, tray preference, privacy and final verification

**Files:**
- Create: `src-cs/ReadAloud/Services/IRegistryAccessor.cs`
- Create: `src-cs/ReadAloud/Services/StartupService.cs`
- Modify: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Modify: `src-cs/ReadAloud/Views/MainWindow.xaml.cs`
- Modify: `src-cs/ReadAloud.Tests/AutomationTests.cs`

**Interfaces:**
- Consumes: `AppConfig`, `IRegistryAccessor`.
- Produces: `IRegistryAccessor` with `string? GetValue(string name)`, `void SetValue(string name, string value)`, `void DeleteValue(string name)`.
- Produces: `StartupService(IRegistryAccessor registry)` with `bool IsStartWithWindowsEnabled { get; }`, `void SetStartWithWindows(bool enabled)`. Uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Extends: `SettingsViewModel` with `void ApplyStartupSettings()` and disclosure text for auto-read/history/privacy.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void StartWithWindows_WritesRunValue()

[Fact] public void DisablingStartWithWindows_DeletesRunValue()

[Fact] public void MinimizeToTray_ChangesMinimizeOnly()
// Assert X exit still registered separately.

[Fact] public void PrivacyDisclosure_ListsAutoReadAndHistory()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --filter AutomationTests`

Expected: compile errors.

- [ ] **Step 3: Implement startup service and wiring**

Use fake registry in tests. `ApplyStartupSettings()` writes/removes the Run value. MainWindow minimize button hides when `MinimizeToTray` is true; X still exits. Settings shows clear privacy text.

- [ ] **Step 4: Run full suite**

Run: `dotnet test src-cs/ReadAloud.sln`

Expected: all tests across all phases pass.

- [ ] **Step 5: Final packaged smoke test**

Run `.\\build-cs.bat`, then start `dist-cs\\ReadAloud.exe`. Verify: selection reading in Notepad and browser; hotkey recording persists; theme toggle persists; mini player appears/hides; skip continues to end; voice library lists voices; history opt-in/replay/delete; auto-read drag selection works and keyboard-only selection uses hotkey; Start with Windows toggles registry; X exits.

- [ ] **Step 6: Commit and push**

`git add src-cs build-cs.bat docs && git commit -m "feat: complete phased Read Aloud C# rewrite" && git push origin reliability-pass:main`

---

## Self-review

- **Spec coverage:** config migration, themes, sidebar, home input, hotkeys, capture, speech, playback, mini player, voice library, history, auto-read, startup, tray, privacy, packaging, and smoke tests each have an owning task. Deferred Whisper/edge/LLM features have no task and are shown as disabled placeholders.
- **Step scan:** every step names one command, one file, one signature, or one test assertion. No TBD lines remain.
- **Type consistency:** `AppConfig`, `CaptureMethod`, `CaptureResult`, `HotkeyBinding`, `IHotkeyRegistrar`, `SelectionService`, `ISpeechEngine`, `PlaybackService`, `MiniPlayerService`, `HistoryService`, `IAutoReadTrigger`, and `StartupService` names and signatures are used consistently across tasks.
- **Review Focus:** each chosen failure mode has a named test in the owning task; additional privacy and phase-specific tests are included in Tasks 10–14.
- **Proportion:** the plan records decisions, signatures, test names, assertions, commands, and exact values; implementation bodies are left to the implementer except where the spec fixes the algorithm.
