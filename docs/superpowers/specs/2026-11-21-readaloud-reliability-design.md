# Design — Read Aloud reliability, quality and light UI polish

**Date:** 2026-11-21
**Status:** Approved direction (user selected "A + light C" on 2026-11-21)
**Scope:** Bounded improvement pass on the existing Python/Tkinter desktop app.

## Intent and why

The app already works end to end (global hotkey → selection capture → optional local LLM → TTS → tray/window). The user asked to "continue development and make any improvements if needed" and to build according to a curated set of high-star GitHub engineering skills. Those skills (superpowers, Matt Pocock, AddyOSmani, Anthropic) centre on test-driven development, systematic debugging, code review, security hardening, observability, documentation and verification before claiming success.

The purpose of this pass is to make the existing app **trustworthy and maintainable**, not to add new product capabilities, and to make small UI changes that are free while we are in the code.

## Goals

1. Eliminate the concrete defects found by code review (list below).
2. Raise automated test coverage from 9 narrow tests to a suite that covers config, hotkeys, capture, TTS routing, LLM discovery and logging, written test-first.
3. Add structured observability (per-request IDs, timings, outcomes) to `logs/app.log`.
4. Harden config loading/validation and the local-LLM integration against malformed or hostile values.
5. Synchronise settings between the main window, tray menu, and `config/settings.json`.
6. Light UI polish: status/progress feedback, tooltips/accessible labels, window icon, timestamped activity log.
7. Rebuild `dist\ReadAloud\ReadAloud.exe` and verify it launches.

## Non-goals

- New TTS providers, OCR, wake word, speech queue, per-mode hotkeys.
- Cloud APIs, telemetry, or any network egress beyond the already-documented optional `edge-tts` / local LLM endpoints.
- Rewriting the app in another language or framework.
- Redesigning the whole window (only light polish).

## Anti-goals (what would make this a failure)

- Claiming "tests pass" without running them.
- Committing changes that make the packaged `.exe` fail to start.
- Silently lowering the quality bar to get a green result (e.g. deleting a failing test instead of fixing the bug).

## Constraints

- Python 3.10 in `.venv`; Windows 10; existing `.venv` and PyInstaller build must keep working.
- Existing settings file format must stay backward compatible.
- Must remain fully offline in the default configuration.
- No `gh` CLI installed; skills were obtained by `git clone --depth 1` from public GitHub repos and copied into `.opencode/skills/` with attribution in `SKILLS.md`.

## Approaches considered

| Approach | Trade-off | Verdict |
|---|---|---|
| **A. Reliability + quality pass (chosen)** | Highest durability; directly matches installed skills; low feature risk. | **Chosen (with light C folded in).** |
| B. Feature expansion | Visible, but adds untested surface and risks the packaged build. | Rejected for this pass. |
| C. UI/UX only | Looks better, leaves the thread-safety and error-handling defects in place. | Rejected as the core; folded in as light polish. |

## Concrete defects to fix

1. **Thread-safety:** `App.handle_request()` runs in the `read-worker` thread and calls Tk `StringVar.get()` / `IntVar.get()`. Snapshot settings on the main thread in `request_read()` and pass a plain dict through the queue.
2. **Settings window on a worker thread:** `settings_ui.open_settings()` creates a second `tk.Tk()` in a daemon thread. Dispatch it through the main-loop event queue instead.
3. **Hotkey collisions / invalid combos:** `HotkeyManager` builds a dict from configured strings; duplicate or malformed combos silently drop actions or raise. Validate uniqueness and syntax, notify the user, and fall back safely.
4. **Config persistence:** engine/mode/volume changes in the window are not saved until a read occurs. Persist on every change.
5. **Log viewer crash:** `open_log()` calls `os.startfile()` on a possibly missing file. Check/create/catch.
6. **TTS cancellation and robustness:** stop requests during `edge-tts` network synthesis are ignored until playback; pygame mixer init and temp-file cleanup need guards; `comtypes` must be an explicit dependency.
7. **Clipboard restore:** best-effort multi-format restore must warn on unsupported formats instead of failing silently.
8. **LLM discovery fallback:** when a configured endpoint is down, `discover()` never tries LM Studio/Ollama candidates. Validate base URL scheme and try candidates after the configured one.
9. **Logging in frozen builds:** a console `StreamHandler` can raise when PyInstaller runs with `--windowed` (no stdout). Guard its creation.
10. **Config validation:** clamp/coerce numeric fields on load; ignore unknown keys; recover from corrupt JSON without losing defaults.
11. **Voice listener race:** double-checked lock is wrong (outer check outside the lock); fix it.
12. **Docs drift:** `PLAN.md` header still says "No application code has been written yet" while later sections say approved/implemented. Reconcile.

## Test-first acceptance criteria

- `unittest` suite passes with `.venv\Scripts\python.exe -m unittest discover -s tests` and includes new tests for:
  - config round-trip + coercion of invalid numbers + corrupt JSON recovery;
  - hotkey validation (duplicates, malformed combos, canonical output);
  - settings snapshot helper returning plain values (no Tk objects cross threads);
  - TTS router selecting offline/edge and falling back, with stop-event behaviour;
  - LLM discovery preferring configured endpoint then candidates, and prompt building;
  - logging setup producing a rotating file handler without a console.
- `build.bat` rebuilds the one-folder exe successfully.
- `dist\ReadAloud\ReadAloud.exe` starts and creates its config/log directories.

## What is left to the builder (decisions already made)

- Keep the existing architecture (one `App` class, Keras-less plain Python, Tkinter + pystray, PyInstaller one-folder).
- Use only the standard library `unittest` + `unittest.mock` (no new test dependency).
- Put new tests in `tests/`, grouped by module.
- Write ADR-style notes for the thread-snapshot decision in `docs/adr/`.
- Size: medium (multi-file, but bounded). If it grows, stop and re-plan.
