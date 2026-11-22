# Read Aloud — Windows-style Color Mode Design

## Status

Proposed

## Date

2026-11-22

## Intent

The user wants the Appearance section to behave like Windows Settings > Personalization > Colors: a **Color mode** choice of **Windows default / Dark / Light**. Dark should read as dark-black surfaces with brown option colors; Light should read as white surfaces with warm yellowish option colors. The user also chose to **keep the existing Aurora / Orbit theme selector**, so this change adds a second selector rather than replacing the first.

## Goals

- Add a `Color mode` dropdown with values `system`, `dark`, `light`, displayed as **Windows default**, **Dark**, **Light**.
- When `system` is selected, follow the Windows app light/dark setting and update live when it changes.
- Give Dark mode dark-black surfaces and brown option/selection colors; give Light mode white surfaces and yellowish option/selection colors.
- Keep the existing `Theme` dropdown (`aurora`, `orbit`) working and visible.
- Persist both choices independently in `settings.json` and apply them immediately.
- Keep unit tests hermetic: no real registry, clipboard, speech device, mouse hook, or live system-theme subscription in tests.

## Non-goals

- Removing or renaming Aurora Compact / Orbit Console.
- Changing the app logo, tray behavior, capture modes, hotkeys, speech, or Linux companion work.
- Adding new accent colors from a design gallery; the requested warm palette is fixed.
- Supporting Windows high-contrast themes as a separate mode.
- Releasing a new version tag; this lands on `main` after v1.0.0.

## Chosen approach: two independent selectors

`Color mode` controls the **surface and option palette**. `Theme` controls the **accent and corner style**. They are layered at runtime:

1. Load a base dictionary from the resolved mode: `Themes/Light.xaml` or `Themes/Dark.xaml`.
2. Load the accent dictionary from the selected theme: `Themes/Aurora.xaml` or `Themes/Orbit.xaml`.
3. Load `Themes/Controls.xaml`, the shared implicit styles that route ComboBoxItem/ListBoxItem selection and hover states through the new brushes.
4. Replace the single merged dictionary with these three, in that order.

The base dictionary owns every neutral and option-state brush: `BackgroundBrush`, `SidebarBrush`, `CardBackgroundBrush`, `TextBrush`, `MutedTextBrush`, `PlaceholderBrush`, `BorderBrush`, `SubtleBrush`, plus new `OptionHighlightBrush`, `OptionForegroundBrush`, and `HoverBrush`. This is what makes Dark read brown/dark-black and Light read white/yellowish regardless of which accent theme is chosen.

The accent dictionary owns only `AccentBrush`, `AccentForegroundBrush`, and `CornerRadius`. Aurora keeps its compact rounded feel; Orbit keeps its tighter radius. Primary action buttons use `AccentBrush`; settings controls, list selection, and option highlights use the mode's `OptionHighlightBrush`.

Rejected alternatives:

- **Replace Aurora/Orbit with one three-item dropdown.** Simpler, but the user explicitly asked to keep both selectors.
- **Mode overrides theme entirely.** Then the theme dropdown would appear to do nothing when a fixed mode is chosen, which is worse UX than layering.
- **Theme overrides mode entirely.** Then the requested brown/yellowish option colors would disappear whenever Orbit or Aurora was selected.

## Palette direction

| Resource | Light base | Dark base |
| --- | --- | --- |
| `BackgroundBrush` | warm off-white `#FFFDF4` | near-black `#14110F` |
| `SidebarBrush` | pale cream `#FFF7DF` | deep brown-black `#1D1815` |
| `CardBackgroundBrush` | white `#FFFFFF` | dark brown `#241D18` |
| `TextBrush` | warm charcoal `#2B2418` | warm off-white `#F3E9D8` |
| `MutedTextBrush` | warm gray `#6B5B3E` | muted tan `#B8A487` |
| `PlaceholderBrush` | warm gray `#8A7958` | muted tan `#98836A` |
| `BorderBrush` | light beige `#EADCB4` | brown `#4A382A` |
| `SubtleBrush` | pale yellow `#FFF4CC` | brown `#2E241D` |
| `OptionHighlightBrush` | yellowish `#F6C445` | brown `#8B5A2B` |
| `OptionForegroundBrush` | dark warm `#2B2418` | near-white `#FFF6E8` |
| `HoverBrush` | pale amber `#FBE9AF` | warm brown `#3A2B21` |

These are the palette direction, not a promise of exact final hex values; small contrast adjustments are allowed during implementation to keep text legible.

## System theme detection

Add `ISystemThemeProvider`:

```csharp
public interface ISystemThemeProvider
{
    bool IsDark { get; }
    event Action? Changed;
}
```

`WindowsSystemThemeProvider` reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (dark when the value is `0`) and raises `Changed` on `Microsoft.Win32.SystemEvents.UserPreferenceChanged` for the `ImmersiveColorSet` category. It never writes to the registry. Tests use a `FakeSystemThemeProvider`; they never construct the Windows implementation.

`ThemeService` subscribes to `Changed` in its constructor. When `CurrentMode == "system"` and the event fires, it re-applies using the same selected `Theme`. When a different mode is selected, the event is ignored.

## Service and view-model changes

- `IThemeService` gains `void Apply(string mode, string theme)` and `string CurrentMode { get; }`. The old `Apply(string theme)` is removed so there is one unambiguous call path.
- `ThemeService.Apply(mode, theme)` normalizes mode to `system|dark|light` and theme to `aurora|orbit`. It resolves `system` through `ISystemThemeProvider.IsDark`, then loads base + accent dictionaries through the existing injectable `dictionaryFactory`.
- `MainViewModel.ApplyTheme(string mode, string theme)` replaces the single-string method and forwards to `IThemeService`.
- `SettingsViewModel` gains `ColorMode`; its setter persists `color_mode` immediately and calls `_main.ApplyTheme(ColorMode, Theme)`, exactly as `Theme` does today. `Save()` continues to persist both values.
- `App.xaml.cs` starts with `_themes.Apply(config.ColorMode, config.Theme)` and passes the real `WindowsSystemThemeProvider` into `ThemeService`.

## Config changes

- `AppConfig` gains `public string ColorMode { get; set; } = "system";`.
- `ConfigService.KnownKeys` adds `color_mode`; `Apply` reads it; `Save` writes it; `Normalize` accepts `system|dark|light` and defaults anything else to `system`.
- `schemaVersion` stays `1`. Existing configs have no `color_mode`, so they migrate to `system` on next load and keep their existing `aurora`/`orbit` value. New installs also default to `system`.
- Unknown keys are still preserved on save.

## Settings UI changes

In `Views/SettingsWindow.xaml`, the Appearance group becomes:

- `Color mode` label + combo: items **Windows default**, **Dark**, **Light**, bound to `ColorMode` with values `system`, `dark`, `light`.
- `Theme` label + existing combo: `aurora`, `orbit`.

Both combos use immediate `PropertyChanged` persistence, matching current `Theme` behavior. The labels are friendly; stored values remain lowercase ids.

## Files expected to change

- `src-cs/ReadAloud/Themes/Light.xaml` (new)
- `src-cs/ReadAloud/Themes/Dark.xaml` (new)
- `src-cs/ReadAloud/Themes/Controls.xaml` (new implicit styles for option/hover states)
- `src-cs/ReadAloud/Themes/Aurora.xaml`, `Orbit.xaml` (reduced to accent + radius)
- `src-cs/ReadAloud/Services/IThemeService.cs`, `ThemeService.cs`
- `src-cs/ReadAloud/Services/ISystemThemeProvider.cs` (new), `WindowsSystemThemeProvider.cs` (new)
- `src-cs/ReadAloud/App.xaml`, `App.xaml.cs`
- `src-cs/ReadAloud/Models/AppConfig.cs`
- `src-cs/ReadAloud/Services/ConfigService.cs`
- `src-cs/ReadAloud/ViewModels/MainViewModel.cs`, `SettingsViewModel.cs`
- `src-cs/ReadAloud/Views/SettingsWindow.xaml` and any styles that need the new option/hover brushes
- `src-cs/ReadAloud.Tests/ThemeTests.cs`, config/view-model tests, fake system theme provider

## Tests

- `ThemeService` resolves `system` through the fake provider and loads the expected base + accent dictionaries.
- `system` follows a fake `Changed` event and re-applies only while `CurrentMode` is `system`.
- Invalid mode falls back to `system`; invalid theme falls back to `aurora`.
- `ConfigService` accepts `color_mode=system|dark|light`, defaults missing/invalid values to `system`, and preserves `theme`.
- `SettingsViewModel.ColorMode` persists and applies through `MainViewModel`.
- No test touches the real registry or starts a real listener.

## Verification

1. `dotnet test src-cs/ReadAloud.sln` — all tests pass.
2. Rebuild packaged exe with `build-cs.bat`; install to `%LOCALAPPDATA%\Programs\ReadAloud`.
3. Visually check Settings in all six combinations (3 modes x 2 themes) and confirm dark = brown/dark-black options, light = white/yellowish options.
4. With Color mode = Windows default, flip Windows light/dark setting and confirm the app updates without restart.
5. Confirm the app logo remains black-on-white in every theme.
6. Commit and push; do not retag v1.0.0.

## Risks

- Existing generic WPF control templates may hard-code highlight colors; implementation must route option/hover states through the new brushes where they appear.
- `SystemEvents.UserPreferenceChanged` can fire on unrelated categories; the provider filters to `ImmersiveColorSet`.
- Some machines may have `AppsUseLightTheme` missing; provider should treat missing as light.
- If two selectors prove confusing in practice, a later pass can hide the accent dropdown when a fixed mode is active; that is not part of this design.
