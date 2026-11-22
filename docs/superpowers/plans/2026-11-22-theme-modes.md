# Windows-style Color Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Windows-settings-style Color mode selector (Windows default / Dark / Light) alongside the existing Aurora/Orbit theme selector, with brown/dark-black Dark and white/yellowish Light palettes and live Windows theme following.

**Architecture:** `Color mode` selects a base resource dictionary that owns all neutral and option-state brushes; the existing `Theme` selects an accent dictionary that owns only accent brushes and corner radius; a shared `Controls.xaml` owns implicit styles that consume the new option/hover brushes. `ThemeService` composes the three dictionaries, resolves `system` through an injectable `ISystemThemeProvider`, and re-applies on provider change events only while `system` is selected.

**Tech Stack:** C# 12 / .NET 8, WPF (`net8.0-windows`), `Microsoft.Win32.SystemEvents`, `Microsoft.Win32.Registry`, xUnit, MSBuild, `build-cs.bat` single-file `win-x64` publish.

**Spec:** `docs/superpowers/specs/2026-11-22-theme-modes-design.md`

## Global Constraints

- Target `net8.0-windows` with WPF + WinForms and `System.Speech` 8.0.0; keep Windows 10 compatibility.
- Keep the product name **Read Aloud** and the black-mouth-on-white-tile app logo unchanged in every theme.
- Offline-first: no network calls, telemetry, cloud TTS, or cloud LLM.
- Tests must be hermetic: no real clipboard, registry, speech device, mouse hook, or live system-theme listener.
- `schemaVersion` stays `1`; unknown config keys are preserved on save.
- `system` mode reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` only; it never writes to the registry.
- Existing configs without `color_mode` migrate to `system` and keep their existing `theme` value.
- Do not create or move the `v1.0.0` tag.
- Dotnet commands on this host need: `$installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'; $env:PATH = "$installDir;$env:PATH"`.
- Packaged exe: `dist-cs/ReadAloud.exe`; installed exe: `%LOCALAPPDATA%\Programs\ReadAloud\ReadAloud.exe`.

## Review Focus

- Missing or non-numeric `AppsUseLightTheme` value must resolve to light, never crash — pinned by `WindowsSystemThemeProvider_IsDarkFromValue_HandlesMissingAndInvalid` in Task 2.
- `system` mode must follow OS flips: dark provider selects `Themes/Dark.xaml`, light provider selects `Themes/Light.xaml` — pinned by `Apply_System_UsesProviderValue` in Task 2.
- A system-theme change event must be ignored while `ColorMode` is `dark` or `light`, so a manual choice is not overwritten — pinned by `SystemThemeChanged_IsIgnoredWhenModeIsFixed` in Task 2.
- Invalid or missing `color_mode` must normalize to `system` without disturbing `theme` — pinned by `ColorMode_NormalizesToSystem` and `ColorMode_Missing_DefaultsToSystem` in Task 1.
- Both dropdowns must persist independently and apply immediately, including the last setter winning — pinned by `ColorModeToggle_PersistsAndAppliesWithSelectedTheme` in Task 4.

---

### Task 0: Commit approved design and plan

**Files:**
- Commit: `docs/superpowers/specs/2026-11-22-theme-modes-design.md`
- Commit: `docs/superpowers/plans/2026-11-22-theme-modes.md`

**Interfaces:**
- Consumes: none.
- Produces: reviewed spec and plan on `main`.

- [ ] **Step 1: Commit the documents**

```powershell
git add docs/superpowers/specs/2026-11-22-theme-modes-design.md docs/superpowers/plans/2026-11-22-theme-modes.md
git commit -m "docs: approve color mode design and implementation plan"
```

- [ ] **Step 2: Verify commit**

Run: `git log -1 --oneline`

Expected: the `docs: approve color mode design and implementation plan` commit at HEAD.

---

### Task 1: Config schema for color mode

**Files:**
- Modify: `src-cs/ReadAloud/Models/AppConfig.cs`
- Modify: `src-cs/ReadAloud/Services/ConfigService.cs`
- Test: `src-cs/ReadAloud.Tests/ConfigTests.cs`

**Interfaces:**
- Consumes: existing `ConfigService(string appDataConfigPath, string legacyConfigPath)`, `AppConfig.Theme`.
- Produces: `AppConfig.ColorMode` (`string`, default `"system"`); `ConfigService` reads/writes JSON key `color_mode` and normalizes it to `system`, `dark`, or `light`.

- [ ] **Step 1: Write failing config tests**

Add to `ConfigTests.cs`:

```csharp
[Theory]
[InlineData("dark", "dark")]
[InlineData("light", "light")]
[InlineData("system", "system")]
[InlineData("orbit", "system")]
public void ColorMode_NormalizesToSystem(string input, string expected)
{
    Write(AppDataPath, $"{{\"color_mode\":\"{input}\",\"theme\":\"orbit\"}}");

    var config = Service().Load();

    Assert.Equal(expected, config.ColorMode);
    Assert.Equal("orbit", config.Theme);
}

[Fact]
public void ColorMode_Missing_DefaultsToSystem()
{
    Write(AppDataPath, """{"theme":"aurora"}""");

    var config = Service().Load();

    Assert.Equal("system", config.ColorMode);
    Assert.Equal("aurora", config.Theme);
}

[Fact]
public void Save_WritesColorMode()
{
    var service = Service();
    var config = service.Load();
    config.ColorMode = "dark";

    service.Save(config);

    using var doc = JsonDocument.Parse(File.ReadAllText(AppDataPath));
    Assert.Equal("dark", doc.RootElement.GetProperty("color_mode").GetString());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore --filter "FullyQualifiedName~ConfigTests"`

Expected: FAIL — `AppConfig` has no `ColorMode` property.

- [ ] **Step 3: Implement config changes**

- In `AppConfig.cs`, add `public string ColorMode { get; set; } = "system";`.
- In `ConfigService.KnownKeys`, add `"color_mode"`.
- In `ConfigService.Apply`, read it with `GetString(root, "color_mode", config.ColorMode)`.
- In `ConfigService.Save`, write it with `writer.WriteString("color_mode", config.ColorMode);` immediately after `theme`.
- In `ConfigService.Normalize`, set `config.ColorMode` to `config.ColorMode.ToLowerInvariant()` when it is `"system"`, `"dark"`, or `"light"`; otherwise `"system"`. Do not change `config.Theme`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore --filter "FullyQualifiedName~ConfigTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Models/AppConfig.cs src-cs/ReadAloud/Services/ConfigService.cs src-cs/ReadAloud.Tests/ConfigTests.cs
git commit -m "feat: persist color mode setting"
```

---

### Task 2: Theme service composition and system theme seam

**Files:**
- Create: `src-cs/ReadAloud/Services/ISystemThemeProvider.cs`
- Create: `src-cs/ReadAloud/Services/WindowsSystemThemeProvider.cs`
- Modify: `src-cs/ReadAloud/Services/IThemeService.cs`
- Modify: `src-cs/ReadAloud/Services/ThemeService.cs`
- Test: `src-cs/ReadAloud.Tests/ThemeTests.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs` (fake only)

**Interfaces:**
- Consumes: nothing from Task 1 (that task only adds config storage).
- Produces:
  - `ISystemThemeProvider { bool IsDark { get; } event Action? Changed; }`
  - `WindowsSystemThemeProvider.IsDarkFromValue(object? value) -> bool` (public static, no registry access)
  - `ThemeService(ResourceDictionary applicationResources, ISystemThemeProvider systemThemeProvider, Func<string, ResourceDictionary>? dictionaryFactory = null)`
  - Transitional `ThemeService(ResourceDictionary applicationResources, Func<string, ResourceDictionary>? dictionaryFactory = null)` that constructs `new WindowsSystemThemeProvider()` and delegates; Task 4 removes it so `App.xaml.cs` keeps compiling until its wiring task.
  - `IThemeService.CurrentMode`, `IThemeService.Apply(string mode, string theme)`
  - Transitional `IThemeService.Apply(string theme)` that calls `Apply("system", theme)`; Task 4 removes it.

- [ ] **Step 1: Write failing service tests**

Replace the two old `ThemeService.Apply` tests in `ThemeTests.cs` with these, and add `FakeSystemThemeProvider` beside `FakeThemeService`:

```csharp
[Theory]
[InlineData("dark", "aurora", "Themes/Dark.xaml", "Themes/Aurora.xaml")]
[InlineData("light", "orbit", "Themes/Light.xaml", "Themes/Orbit.xaml")]
public void Apply_LoadsBaseAccentAndControlsInOrder(
    string mode, string theme, string expectedBase, string expectedAccent)
{
    var resources = new ResourceDictionary();
    var requested = new List<string>();
    var service = new ThemeService(resources, new FakeSystemThemeProvider(), source =>
    {
        requested.Add(source);
        return new ResourceDictionary();
    });

    service.Apply(mode, theme);

    Assert.Equal(new[] { expectedBase, expectedAccent, "Themes/Controls.xaml" }, requested);
    Assert.Equal(3, resources.MergedDictionaries.Count);
    Assert.Equal(mode, service.CurrentMode);
    Assert.Equal(theme, service.CurrentTheme);
}

[Theory]
[InlineData(true, "Themes/Dark.xaml")]
[InlineData(false, "Themes/Light.xaml")]
public void Apply_System_UsesProviderValue(bool isDark, string expectedBase)
{
    var requested = new List<string>();
    var service = new ThemeService(new ResourceDictionary(), new FakeSystemThemeProvider { IsDark = isDark }, source =>
    {
        requested.Add(source);
        return new ResourceDictionary();
    });

    service.Apply("system", "aurora");

    Assert.Equal(expectedBase, requested[0]);
    Assert.Equal("system", service.CurrentMode);
}

[Fact]
public void SystemThemeChanged_IsIgnoredWhenModeIsFixed()
{
    var provider = new FakeSystemThemeProvider { IsDark = false };
    var requested = new List<string>();
    var service = new ThemeService(new ResourceDictionary(), provider, source =>
    {
        requested.Add(source);
        return new ResourceDictionary();
    });

    service.Apply("dark", "aurora");
    requested.Clear();
    provider.IsDark = true;
    provider.RaiseChanged();

    Assert.Empty(requested);
    Assert.Equal("dark", service.CurrentMode);
}

[Fact]
public void SystemThemeChanged_ReappliesWhenModeIsSystem()
{
    var provider = new FakeSystemThemeProvider { IsDark = false };
    var requested = new List<string>();
    var service = new ThemeService(new ResourceDictionary(), provider, source =>
    {
        requested.Add(source);
        return new ResourceDictionary();
    });

    service.Apply("system", "aurora");
    requested.Clear();
    provider.IsDark = true;
    provider.RaiseChanged();

    Assert.Equal("Themes/Dark.xaml", requested[0]);
}

[Fact]
public void InvalidModeAndTheme_FallBackToSystemAndAurora()
{
    var requested = new List<string>();
    var service = new ThemeService(new ResourceDictionary(), new FakeSystemThemeProvider(), source =>
    {
        requested.Add(source);
        return new ResourceDictionary();
    });

    service.Apply("neon", "neon");

    Assert.Equal("system", service.CurrentMode);
    Assert.Equal("aurora", service.CurrentTheme);
    Assert.Equal("Themes/Aurora.xaml", requested[1]);
}

[Fact]
public void WindowsSystemThemeProvider_IsDarkFromValue_HandlesMissingAndInvalid()
{
    Assert.False(WindowsSystemThemeProvider.IsDarkFromValue(null));
    Assert.False(WindowsSystemThemeProvider.IsDarkFromValue(1));
    Assert.False(WindowsSystemThemeProvider.IsDarkFromValue("0"));
    Assert.True(WindowsSystemThemeProvider.IsDarkFromValue(0));
}
```

Fake:

```csharp
private sealed class FakeSystemThemeProvider : ISystemThemeProvider
{
    public bool IsDark { get; set; }

    public event Action? Changed;

    public void RaiseChanged() => Changed?.Invoke();
}
```

Update `FakeThemeService` in both `ThemeTests.cs` and `SettingsTests.cs` to add `public string CurrentMode { get; private set; } = "system";` and `public void Apply(string mode, string theme) { CurrentMode = mode; CurrentTheme = theme; }`, while keeping the old `Apply(string theme)` for now.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore --filter "FullyQualifiedName~ThemeTests"`

Expected: FAIL — `ISystemThemeProvider` and `CurrentMode` do not exist.

- [ ] **Step 3: Implement the seam and service**

- Create `ISystemThemeProvider.cs` with exactly the interface in the Interfaces block.
- Create `WindowsSystemThemeProvider.cs`:
  - `public static bool IsDarkFromValue(object? value) => value is int number && number == 0;`
  - Constructor caches `IsDark` and subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged`.
  - `IsDark` reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` via `Registry.CurrentUser.OpenSubKey(...)?.GetValue("AppsUseLightTheme")`, returns `IsDarkFromValue(value)`, and treats exceptions/missing key as light.
  - On `UserPreferenceChanged`, re-read `IsDark`; if it differs from the cached value, update the cache and invoke `Changed?.Invoke()`. Do not write to the registry.
- Modify `IThemeService.cs`: add `string CurrentMode { get; }` and `void Apply(string mode, string theme);`; keep `void Apply(string theme);`.
- Modify `ThemeService.cs`:
  - Add constants `LightSource = "Themes/Light.xaml"`, `DarkSource = "Themes/Dark.xaml"`, `AuroraSource = "Themes/Aurora.xaml"`, `OrbitSource = "Themes/Orbit.xaml"`, `ControlsSource = "Themes/Controls.xaml"`.
  - Add the `ISystemThemeProvider` constructor parameter and subscribe to `Changed`.
  - Keep the old constructor `ThemeService(ResourceDictionary, Func<string, ResourceDictionary>?)` as a transitional overload that calls the new one with `new WindowsSystemThemeProvider()`. Task 4 removes it.
  - `Apply(mode, theme)` normalizes mode (`dark`, `light`, else `system`) and theme (`orbit`, else `aurora`); resolves `system` through `IsDark`; clears `MergedDictionaries`; adds base, accent, then controls using `_dictionaryFactory`; sets `CurrentMode` and `CurrentTheme`.
  - `Apply(theme)` calls `Apply("system", theme)`.
  - On `Changed`, call `Apply(CurrentMode, CurrentTheme)` only when `CurrentMode == "system"`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore`

Expected: PASS — including all existing tests, because the old overload remains.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Services/ISystemThemeProvider.cs src-cs/ReadAloud/Services/WindowsSystemThemeProvider.cs src-cs/ReadAloud/Services/IThemeService.cs src-cs/ReadAloud/Services/ThemeService.cs src-cs/ReadAloud.Tests/ThemeTests.cs src-cs/ReadAloud.Tests/SettingsTests.cs
git commit -m "feat: compose color mode and theme dictionaries"
```

---

### Task 3: Palette dictionaries and option styles

**Files:**
- Create: `src-cs/ReadAloud/Themes/Light.xaml`
- Create: `src-cs/ReadAloud/Themes/Dark.xaml`
- Create: `src-cs/ReadAloud/Themes/Controls.xaml`
- Modify: `src-cs/ReadAloud/Themes/Aurora.xaml`
- Modify: `src-cs/ReadAloud/Themes/Orbit.xaml`
- Modify: `src-cs/ReadAloud/App.xaml`

**Interfaces:**
- Consumes: dictionary source names from Task 2.
- Produces: resource keys `BackgroundBrush`, `SidebarBrush`, `CardBackgroundBrush`, `TextBrush`, `MutedTextBrush`, `PlaceholderBrush`, `BorderBrush`, `SubtleBrush`, `OptionHighlightBrush`, `OptionForegroundBrush`, `HoverBrush`, plus `AccentBrush`, `AccentForegroundBrush`, `CornerRadius` from the accent dictionaries.

- [ ] **Step 1: Write the base and accent dictionaries**

- `Light.xaml` defines the eleven base brushes with the warm white/yellowish values from the spec table.
- `Dark.xaml` defines the same eleven keys with the dark-black/brown values from the spec table.
- Reduce `Aurora.xaml` to `AccentBrush`, `AccentForegroundBrush`, and `CornerRadius=14`.
- Reduce `Orbit.xaml` to `AccentBrush`, `AccentForegroundBrush`, and `CornerRadius=10`.
- Keep every existing key name; do not introduce a theme-specific copy of the base brushes.

- [ ] **Step 2: Write `Controls.xaml`**

Create implicit styles for `ComboBoxItem` and `ListBoxItem`:
- Default `Background="Transparent"`, `Foreground="{DynamicResource TextBrush}"`.
- `IsSelected=True` sets `Background="{DynamicResource OptionHighlightBrush}"` and `Foreground="{DynamicResource OptionForegroundBrush}"`.
- `IsMouseOver=True` sets `Background="{DynamicResource HoverBrush}"` only when not selected.
- Do not replace control templates; only set properties and triggers.

- [ ] **Step 3: Update `App.xaml` fallback merge**

Merge `Themes/Light.xaml`, `Themes/Aurora.xaml`, and `Themes/Controls.xaml` in that order, replacing the current single `Themes/Aurora.xaml` merge.

- [ ] **Step 4: Verify XAML and build**

Run: `dotnet build src-cs/ReadAloud/ReadAloud.csproj --no-restore`

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src-cs/ReadAloud/Themes/Light.xaml src-cs/ReadAloud/Themes/Dark.xaml src-cs/ReadAloud/Themes/Controls.xaml src-cs/ReadAloud/Themes/Aurora.xaml src-cs/ReadAloud/Themes/Orbit.xaml src-cs/ReadAloud/App.xaml
git commit -m "feat: add warm light and brown dark palettes"
```

---

### Task 4: Wire both selectors into the UI and startup

**Files:**
- Modify: `src-cs/ReadAloud/Services/IThemeService.cs`
- Modify: `src-cs/ReadAloud/Services/ThemeService.cs`
- Modify: `src-cs/ReadAloud/ViewModels/MainViewModel.cs`
- Modify: `src-cs/ReadAloud/ViewModels/SettingsViewModel.cs`
- Modify: `src-cs/ReadAloud/Views/SettingsWindow.xaml`
- Modify: `src-cs/ReadAloud/App.xaml.cs`
- Test: `src-cs/ReadAloud.Tests/ThemeTests.cs`
- Test: `src-cs/ReadAloud.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: `AppConfig.ColorMode`, `IThemeService.Apply(mode, theme)`, `ISystemThemeProvider`.
- Produces: `MainViewModel.ColorMode`, `MainViewModel.ApplyTheme(string mode, string theme)`, `SettingsViewModel.ColorMode`, and the final `IThemeService` without the transitional `Apply(string theme)` overload.

- [ ] **Step 1: Write failing UI/view-model tests**

Add to `SettingsTests.cs`:

```csharp
[Fact]
public void ColorModeToggle_PersistsAndAppliesWithSelectedTheme()
{
    var h = new Harness(_root);

    h.ViewModel.ColorMode = "dark";
    h.ViewModel.Theme = "orbit";

    Assert.Equal("dark", h.Config.Load().ColorMode);
    Assert.Equal("orbit", h.Config.Load().Theme);
    Assert.Equal("dark", h.Themes.CurrentMode);
    Assert.Equal("orbit", h.Themes.CurrentTheme);
    Assert.Equal("dark", h.Main.ColorMode);
    Assert.Equal("orbit", h.Main.Theme);
}

[Fact]
public void ColorMode_AppliesImmediatelyWithoutSave()
{
    var h = new Harness(_root);

    h.ViewModel.ColorMode = "light";

    Assert.Equal("light", h.Config.Load().ColorMode);
    Assert.Equal("light", h.Themes.CurrentMode);
    Assert.Equal("light", h.Main.ColorMode);
}
```

Update `MainViewModel_MarksAllNavItemsEnabled` in `ThemeTests.cs` to call `model.ApplyTheme("dark", "orbit")` and assert `model.ColorMode == "dark"` and `model.Theme == "orbit"`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore --filter "FullyQualifiedName~SettingsTests|FullyQualifiedName~ThemeTests"`

Expected: FAIL — `ColorMode` properties and two-argument `ApplyTheme` do not exist.

- [ ] **Step 3: Implement view models and remove the transitional overload**

- `MainViewModel.cs`: add backing field `_colorMode = "system"` and public `ColorMode` property with private setter; change `ApplyTheme(string theme)` to `ApplyTheme(string mode, string theme)` calling `_themes.Apply(mode, theme)` then setting `ColorMode = _themes.CurrentMode` and `Theme = _themes.CurrentTheme`.
- `SettingsViewModel.cs`: add `_colorMode` initialized from `current.ColorMode`; add public `ColorMode` property whose setter mirrors `Theme`: persist `color_mode` to config immediately, then call `_main.ApplyTheme(ColorMode, Theme)` on dispatcher or inline. In `ApplyTo`, set `config.ColorMode = ColorMode;`. In `Save`, replace `_main.ApplyTheme(config.Theme)` with `_main.ApplyTheme(config.ColorMode, config.Theme)`.
- `IThemeService.cs` and `ThemeService.cs`: remove the one-argument `Apply(string theme)` overload and the transitional `ThemeService(ResourceDictionary, Func<string, ResourceDictionary>?)` constructor.
- `ThemeTests.cs` and `SettingsTests.cs`: remove the one-argument methods from fake theme services.

- [ ] **Step 4: Wire the Settings UI and startup**

- In `SettingsWindow.xaml`, add above the existing Theme row:

```xml
<TextBlock Text="Color mode" Foreground="{DynamicResource TextBrush}" Margin="0,8,0,0" />
<ComboBox Width="240"
          Margin="0,4,0,0"
          SelectedValue="{Binding ColorMode, UpdateSourceTrigger=PropertyChanged}"
          SelectedValuePath="Tag">
    <ComboBoxItem Content="Windows default" Tag="system" />
    <ComboBoxItem Content="Dark" Tag="dark" />
    <ComboBoxItem Content="Light" Tag="light" />
</ComboBox>
```

- In `App.xaml.cs`, replace `_themes = new ThemeService(Resources); _themes.Apply(config.Theme);` with:

```csharp
_themes = new ThemeService(Resources, new WindowsSystemThemeProvider());
_themes.Apply(config.ColorMode, config.Theme);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src-cs/ReadAloud.sln --no-restore`

Expected: PASS — full suite green.

- [ ] **Step 6: Commit**

```powershell
git add src-cs/ReadAloud/Services/IThemeService.cs src-cs/ReadAloud/Services/ThemeService.cs src-cs/ReadAloud/ViewModels/MainViewModel.cs src-cs/ReadAloud/ViewModels/SettingsViewModel.cs src-cs/ReadAloud/Views/SettingsWindow.xaml src-cs/ReadAloud/App.xaml.cs src-cs/ReadAloud.Tests/ThemeTests.cs src-cs/ReadAloud.Tests/SettingsTests.cs
git commit -m "feat: add color mode selector to settings"
```

---

### Task 5: Full verification, packaged exe, and push

**Files:**
- Build: `build-cs.bat`
- Binary: `dist-cs/ReadAloud.exe`
- Install: `%LOCALAPPDATA%\Programs\ReadAloud\ReadAloud.exe`

**Interfaces:**
- Consumes: all prior tasks.
- Produces: rebuilt packaged exe, installed app, pushed `main`.

- [ ] **Step 1: Run the full suite**

```powershell
$installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$installDir;$env:PATH"
dotnet test src-cs/ReadAloud.sln --no-restore
```

Expected: all tests pass, 0 failed.

- [ ] **Step 2: Rebuild the packaged exe**

Run: `.\\build-cs.bat`

Expected: exit code 0 and a newer `dist-cs/ReadAloud.exe`.

- [ ] **Step 3: Install and smoke-test**

```powershell
Get-Process ReadAloud -ErrorAction SilentlyContinue | Stop-Process -Force
$exe = 'C:\Users\malla\Downloads\Read anything\dist-cs\ReadAloud.exe'
$install = Join-Path $env:LOCALAPPDATA 'Programs\ReadAloud\ReadAloud.exe'
Copy-Item $exe $install -Force
$p = Start-Process -FilePath $install -PassThru
Start-Sleep -Seconds 5
$r = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
if ($r) { "RUNNING: '$($r.MainWindowTitle)' Responding=$($r.Responding)" } else { "NOT RUNNING exit=$($p.ExitCode)" }
```

Expected: `RUNNING` with title `Read Aloud` and `Responding=True`.

- [ ] **Step 4: Manual visual checklist**

Open Settings and confirm:
- Color mode dropdown shows `Windows default`, `Dark`, `Light`.
- `Dark` + either theme gives dark-black surfaces with brown option/selection colors.
- `Light` + either theme gives white surfaces with yellowish option/selection colors.
- The app logo remains black-on-white in the title bar and taskbar.
- With `Windows default`, flipping Windows light/dark changes the app without restart.

- [ ] **Step 5: Commit and push**

```powershell
git add dist-cs/ReadAloud.exe
git commit -m "build: package color mode release"
git push origin HEAD:main
```

Expected: push succeeds; `v1.0.0` tag is unchanged.
